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

        public EntityBehaviorAimingAccuracy(Entity entity) : base(entity)
        {
            EntityAgent agent = entity as EntityAgent;

            modifiers.Add(new BaseAimingAccuracy(agent));
            modifiers.Add(new MovingAimingAccuracy(agent));
            modifiers.Add(new SprintAimingAccuracy(agent));
            modifiers.Add(new OnHurtAimingAccuracy(agent));

            entity.Attributes.RegisterModifiedListener("aiming", OnAimingChanged);

            Rand = new Random((int)(entity.EntityId + entity.World.ElapsedMilliseconds));
        }

        private void OnAimingChanged()
        {
            bool beforeAiming = IsAiming;
            IsAiming = entity.Attributes.GetInt("aiming") > 0;

            if (beforeAiming == IsAiming) return;

            ArcheryCore.aiming = IsAiming;
            //ArcheryCore.aimX = 0;
            //ArcheryCore.aimY = 0;
            ArcheryCore.aimOffsetX = 0;
            ArcheryCore.aimOffsetY = 0;
            ClientMainPatch.twitchX = 0;
            ClientMainPatch.twitchY = 0;
            //ArcheryCore.aimLastTwitchMilliseconds = entity.World.ElapsedMilliseconds;

            if (IsAiming && entity.World is IServerWorldAccessor)
            {
                double rndpitch = Rand.NextDouble() - 0.5;
                double rndyaw = Rand.NextDouble() - 0.5;
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
                entity.Attributes.SetInt("aiming", 0);
            }

            float accuracy = 0;

            for (int i = 0; i < modifiers.Count; i++)
            {
                modifiers[i].Update(deltaTime, ref accuracy);
            }

            entity.Attributes.SetFloat("aimingAccuracy", accuracy);
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
            return "aimingaccuracy";
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

        public virtual void Update(float dt, ref float accuracy)
        {

        }
    }


    public class BaseAimingAccuracy : AccuracyModifier
    {
        public BaseAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void Update(float dt, ref float accuracy)
        {
            float rangedAcc = entity.Stats.GetBlended("rangedWeaponsAcc");
            float modspeed = entity.Stats.GetBlended("rangedWeaponsSpeed");

            float modacc = 0.93f * rangedAcc;
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
            }

            // Archery
            float archeryAccuracyMod = Math.Max(1f - ((entity.Stats.GetBlended("rangedWeaponsAcc") - 1f)), 0.1f);

            float archeryAccuracy = GameMath.Max(1f - SecondsSinceAimStart, 0f) * 2.5f; // Loss of accuracy from draw
            archeryAccuracy += GameMath.Clamp((SecondsSinceAimStart - 6f) / 12f, 0f, 1f); // Loss of accuracy from holding too long

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

        public override void Update(float dt, ref float accuracy)
        {
            bool sprint = entity.Controls.Sprint;

            if (entity.Controls.TriesToMove)
            {
                accuracyPenalty = GameMath.Clamp(accuracyPenalty + dt / 0.75f, 0, 0.2f);
            } else
            {
                accuracyPenalty = GameMath.Clamp(accuracyPenalty - dt / 2f, 0, 0.2f);
            }

            accuracy -= accuracyPenalty;

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

        public override void Update(float dt, ref float accuracy)
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

            accuracy -= accuracyPenalty;

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

        public override void Update(float dt, ref float accuracy)
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
