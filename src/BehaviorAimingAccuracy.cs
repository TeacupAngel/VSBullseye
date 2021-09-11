using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Archery
{
    public class EntityBehaviorAimingAccuracy : EntityBehavior
    {
        public Random Rand;
        public bool IsAiming;

        List<AccuracyModifier> modifiers = new List<AccuracyModifier>();

        private ArcheryRangedWeaponStats weaponStats;

        protected ArcheryRangedWeaponSystem rangedWeaponSystem;

        public EntityBehaviorAimingAccuracy(Entity entity) : base(entity)
        {
            EntityAgent agent = entity as EntityAgent;

            modifiers.Add(new BaseAimingAccuracy(agent));
            modifiers.Add(new MovingAimingAccuracy(agent));
            modifiers.Add(new SprintAimingAccuracy(agent));
            modifiers.Add(new OnHurtAimingAccuracy(agent));

            entity.Attributes.RegisterModifiedListener("archeryAiming", OnAimingChanged);

            rangedWeaponSystem = entity.Api.ModLoader.GetModSystem<ArcheryRangedWeaponSystem>();

            Rand = new Random((int)(entity.EntityId + entity.World.ElapsedMilliseconds));
        }

        public void SetRangedWeaponStats(ArcheryRangedWeaponStats weaponStats)
        {
            this.weaponStats = weaponStats;
        }

        private void OnAimingChanged()
        {
            bool beforeAiming = IsAiming;
            IsAiming = entity.Attributes.GetInt("archeryAiming") > 0;

            if (beforeAiming == IsAiming) return;

            ArcheryCore.aiming = IsAiming;
            ArcheryCore.aimOffsetX = 0;
            ArcheryCore.aimOffsetY = 0;
            ClientMainPatch.twitchX = 0;
            ClientMainPatch.twitchY = 0;

            if (rangedWeaponSystem.GetEntityCooldownTime(entity.EntityId) > 15f)
            {
                ArcheryCore.aimX = 0f;
                ArcheryCore.aimY = 0f;
            }

            if (IsAiming && entity.World is IServerWorldAccessor)
            {
                double rndpitch = Rand.NextDouble();
                double rndyaw = Rand.NextDouble();
                entity.WatchedAttributes.SetDouble("aimingRandPitch", rndpitch);
                entity.WatchedAttributes.SetDouble("aimingRandYaw", rndyaw);
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (IsAiming)
                {
                    modifiers[i].BeginAim();
                }
                else
                {
                    modifiers[i].EndAim();
                }
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            if (!IsAiming) return;

            if (!entity.Alive)
            {
                entity.Attributes.SetInt("archeryAiming", 0);
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                modifiers[i].Update(deltaTime, weaponStats);
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            base.OnEntityReceiveDamage(damageSource, damage);

            if (damageSource.Type == EnumDamageType.Heal) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                modifiers[i].OnHurt(damage);
            }
        }

        public override string PropertyName()
        {
            return "archery.aimingaccuracy";
        }
    }


    public class AccuracyModifier
    {
        internal EntityAgent entity;
        internal long aimStartMs;

        public float SecondsSinceAimStart
        {
            get { return (entity.World.ElapsedMilliseconds - aimStartMs) / 1000f; }
        }

        public AccuracyModifier(EntityAgent entity)
        {
            this.entity = entity;
        }

        public virtual void BeginAim()
        {
            aimStartMs = entity.World.ElapsedMilliseconds;
        }

        public virtual void EndAim()
        {

        }

        public virtual void OnHurt(float damage) { }

        public virtual void Update(float dt, ArcheryRangedWeaponStats weaponStats)
        {

        }
    }


    public class BaseAimingAccuracy : AccuracyModifier
    {
        public BaseAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void Update(float dt, ArcheryRangedWeaponStats weaponStats)
        {
            float rangedAcc = entity.Stats.GetBlended("rangedWeaponsAcc");
            float modspeed = entity.Stats.GetBlended("rangedWeaponsSpeed");

            float archeryAccuracyMod = Math.Max(1f - (rangedAcc - 1f), 0.1f);

            float archeryAccuracy = GameMath.Max((weaponStats.accuracyStartTime - SecondsSinceAimStart * modspeed) / weaponStats.accuracyStartTime, 0f) * 2.5f; // Loss of accuracy from draw
            archeryAccuracy += GameMath.Clamp((SecondsSinceAimStart - weaponStats.accuracyOvertimeStart - weaponStats.accuracyStartTime) / weaponStats.accuracyOvertimeTime, 0f, 1f) * weaponStats.accuracyOvertime; // Loss of accuracy from holding too long

            ClientMainPatch.driftMultiplier = archeryAccuracyMod + archeryAccuracy;
            ClientMainPatch.twitchMultiplier = archeryAccuracyMod + (archeryAccuracy * 3f);
        }
    }

    /// <summary>
    /// Moving around decreases accuracy by 20% in 0.75 secconds
    /// </summary>
    public class MovingAimingAccuracy : AccuracyModifier
    {
        float accuracyPenalty;

        public MovingAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void Update(float dt, ArcheryRangedWeaponStats weaponStats)
        {
            bool sprint = entity.Controls.Sprint;

            if (entity.Controls.TriesToMove)
            {
                accuracyPenalty = GameMath.Clamp(accuracyPenalty + dt / 0.75f, 0, 0.2f);
            } else
            {
                accuracyPenalty = GameMath.Clamp(accuracyPenalty - dt / 2f, 0, 0.2f);
            }

            ClientMainPatch.driftMultiplier += accuracyPenalty * 5f * weaponStats.accuracyMovePenalty;
            ClientMainPatch.twitchMultiplier += accuracyPenalty * 3f * weaponStats.accuracyMovePenalty;
        }
    }


    /// <summary>
    /// Sprinting around decreases accuracy by 30% in 0.75 secconds
    /// </summary>
    public class SprintAimingAccuracy : AccuracyModifier
    {
        float accuracyPenalty;

        public SprintAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void Update(float dt, ArcheryRangedWeaponStats weaponStats)
        {
            bool sprint = entity.Controls.Sprint;

            if (entity.Controls.TriesToMove && entity.Controls.Sprint)
            {
                accuracyPenalty = GameMath.Clamp(accuracyPenalty + dt / 0.75f, 0, 0.3f);
            }
            else
            {
                accuracyPenalty = GameMath.Clamp(accuracyPenalty - dt / 2f, 0, 0.3f);
            }

            ClientMainPatch.driftMultiplier += accuracyPenalty * 5f * weaponStats.accuracyMovePenalty;
            ClientMainPatch.twitchMultiplier += accuracyPenalty * 3f * weaponStats.accuracyMovePenalty;
        }
    }

    public class OnHurtAimingAccuracy : AccuracyModifier
    {
        float accuracyPenalty;

        public OnHurtAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void Update(float dt, ArcheryRangedWeaponStats weaponStats)
        {
            accuracyPenalty = GameMath.Clamp(accuracyPenalty - dt / 3, 0, 0.4f);

            //accuracy -= accuracyPenalty;
        }

        public override void OnHurt(float damage)
        {
            if (damage > 3)
            {
                accuracyPenalty = -0.4f;
            }
        }
    }
    
}
