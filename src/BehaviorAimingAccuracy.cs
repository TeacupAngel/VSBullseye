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

        private ArcheryRangedWeaponStats weaponStats; // Archery

        public EntityBehaviorAimingAccuracy(Entity entity) : base(entity)
        {
            EntityAgent agent = entity as EntityAgent;

            modifiers.Add(new BaseAimingAccuracy(agent));
            modifiers.Add(new MovingAimingAccuracy(agent));
            modifiers.Add(new SprintAimingAccuracy(agent));
            modifiers.Add(new OnHurtAimingAccuracy(agent));

            entity.Attributes.RegisterModifiedListener("archeryAiming", OnAimingChanged); // Archery

            Rand = new Random((int)(entity.EntityId + entity.World.ElapsedMilliseconds));
        }

        // Archery
        public void SetRangedWeaponStats(ArcheryRangedWeaponStats weaponStats)
        {
            this.weaponStats = weaponStats;
        }
        // /Archery

        private void OnAimingChanged()
        {
            bool beforeAiming = IsAiming;
            IsAiming = entity.Attributes.GetInt("archeryAiming") > 0; // Archery

            if (beforeAiming == IsAiming) return;

            ArcheryCore.aiming = IsAiming;
            //ArcheryCore.aimX = 0;
            //ArcheryCore.aimY = 0;
            ArcheryCore.aimOffsetX = 0;
            ArcheryCore.aimOffsetY = 0;
            ClientMainPatch.twitchX = 0;
            ClientMainPatch.twitchY = 0;

            if (IsAiming && entity.World is IServerWorldAccessor)
            {
                // Archery
                //double rndpitch = Rand.NextDouble() - 0.5;
                //double rndyaw = Rand.NextDouble() - 0.5;
                double rndpitch = Rand.NextDouble();
                double rndyaw = Rand.NextDouble();
                // /Archery
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
                entity.Attributes.SetInt("archeryAiming", 0); // Archery
            }

            //float accuracy = 0; // Archery

            for (int i = 0; i < modifiers.Count; i++)
            {
                //modifiers[i].Update(deltaTime, ref accuracy); // Archery
                modifiers[i].Update(deltaTime, weaponStats);
            }

            // Archery
            //entity.Attributes.SetFloat("aimingAccuracy", accuracy);
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

        //public virtual void Update(float dt, ref float accuracy) // Archery
        public virtual void Update(float dt, ArcheryRangedWeaponStats weaponStats)
        {

        }
    }


    public class BaseAimingAccuracy : AccuracyModifier
    {
        public BaseAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        //public override void Update(float dt, ref float accuracy) // Archery
        public override void Update(float dt, ArcheryRangedWeaponStats weaponStats)
        {
            float rangedAcc = entity.Stats.GetBlended("rangedWeaponsAcc");
            float modspeed = entity.Stats.GetBlended("rangedWeaponsSpeed");

            /*float modacc = 0.93f * rangedAcc;
            if (rangedAcc >= 1)
            {
                // Asymptomatically reach of 100% accuracy
                modacc = 0.93f + ((1 - 1 / (1 + 3 * rangedAcc)) - 0.5f) * 0.07f;
            }

            accuracy = GameMath.Clamp((float)Math.Pow(SecondsSinceAimStart * modspeed * 1.1, 1.5), 0, modacc);
            accuracy *= Math.Max(0.1f, modacc);

            accuracy -= GameMath.Clamp((SecondsSinceAimStart - 3f) / 3, 0, 0.25f);

            if (SecondsSinceAimStart >= 0.75f)
            {
                accuracy += GameMath.Sin(SecondsSinceAimStart * 8) / 80f;
            }*/

            // Archery
            float archeryAccuracyMod = Math.Max(1f - (rangedAcc - 1f), 0.1f);

            float archeryAccuracy = GameMath.Max((weaponStats.accuracyStartTime - SecondsSinceAimStart) / weaponStats.accuracyStartTime, 0f) * 2.5f; // Loss of accuracy from draw
            archeryAccuracy += GameMath.Clamp((SecondsSinceAimStart - weaponStats.accuracyOvertimeStart) / weaponStats.accuracyOvertimeTime, 0f, 1f); // Loss of accuracy from holding too long

            ClientMainPatch.driftMultiplier = archeryAccuracyMod + archeryAccuracy;
            ClientMainPatch.twitchMultiplier = archeryAccuracyMod + (archeryAccuracy * 3f);
            // /Archery
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

        //public override void Update(float dt, ref float accuracy) // Archery
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

            //accuracy -= accuracyPenalty; // Archery

            // Archery
            ClientMainPatch.driftMultiplier += accuracyPenalty * 5f;
            ClientMainPatch.twitchMultiplier += accuracyPenalty * 3f;
            // /Archery
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

        //public override void Update(float dt, ref float accuracy) // Archery
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

            //accuracy -= accuracyPenalty; // Archery

            // Archery
            ClientMainPatch.driftMultiplier += accuracyPenalty * 5f;
            ClientMainPatch.twitchMultiplier += accuracyPenalty * 3f;
            // /Archery
        }
    }

    public class OnHurtAimingAccuracy : AccuracyModifier
    {
        float accuracyPenalty;

        public OnHurtAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        //public override void Update(float dt, ref float accuracy) // Archery
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
