using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Vintagestory;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

namespace Archery
{
    public class ArcheryEntityBehaviorAimingAccuracy : EntityBehavior
    {
        public Random Rand;
        public bool IsAiming;

        long mouseDriftLastChange;
        Vec2f mouseDrift;

        List<AccuracyModifier> modifiers = new List<AccuracyModifier>();

        ITreeAttribute aimStatsTree;

        public ArcheryEntityBehaviorAimingAccuracy(Entity entity) : base(entity)
        {
            EntityAgent agent = entity as EntityAgent;

            modifiers.Add(new BaseAimingAccuracy(agent));
            modifiers.Add(new MovingAimingAccuracy(agent));
            //modifiers.Add(new SprintAimingAccuracy(agent));
            modifiers.Add(new OnHurtAimingAccuracy(agent));

            entity.Attributes.RegisterModifiedListener("aiming", OnAimingChanged);

            Rand = new Random((int)(entity.EntityId + entity.World.ElapsedMilliseconds));
        }

        private void OnAimingChanged()
        {
            bool beforeAiming = IsAiming;
            IsAiming = entity.Attributes.GetInt("aiming") > 0;

            if (beforeAiming == IsAiming) return;

            if (IsAiming && entity.World is Vintagestory.API.Server.IServerWorldAccessor)
            {
                double rndpitch = Rand.NextDouble() - 0.5;
                double rndyaw = Rand.NextDouble() - 0.5;
                entity.WatchedAttributes.SetDouble("aimingRandPitch", rndpitch);
                entity.WatchedAttributes.SetDouble("aimingRandYaw", rndyaw);
            }

            if (IsAiming)
            {
                entity.Attributes.SetFloat("aimingAccuracy", 0);

                aimStatsTree = entity.Attributes.GetTreeAttribute("aimStats");
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (IsAiming)
                {
                    modifiers[i].BeginAim(aimStatsTree);
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
            float drift = 0;

            for (int i = 0; i < modifiers.Count; i++)
            {
                modifiers[i].UpdateAccuracy(deltaTime, ref accuracy);
                modifiers[i].UpdateDrift(deltaTime, ref drift);
            }

            entity.Attributes.SetFloat("aimingAccuracy", accuracy);

            ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;

            if (capi != null)
            {
                if (entity.World.ElapsedMilliseconds - mouseDriftLastChange > 250)
                {
                    mouseDriftLastChange = entity.World.ElapsedMilliseconds;

                    float driftMagnitudeYaw = (aimStatsTree?.GetFloat("driftYaw", 0f) ?? 0f) * drift;
                    float driftMagnitudePitch = (aimStatsTree?.GetFloat("driftPitch", 0f) ?? 0f) * drift;

                    mouseDrift = new Vec2f((float)(Rand.NextDouble() - 0.5) * driftMagnitudeYaw, (float)(Rand.NextDouble() - 0.5) * driftMagnitudePitch);
                }

                float driftDecay = (aimStatsTree?.GetFloat("driftDecay", 0.8f)) ?? 0.8f;

                capi.Input.MouseYaw += mouseDrift.X;
                capi.Input.MousePitch += mouseDrift.Y;
                mouseDrift.X *= driftDecay;
                mouseDrift.Y *= driftDecay;

                SyncedEntityPos syncedPos = entity.Pos;
                syncedPos.Pitch = syncedPos.Pitch + mouseDrift.Y;
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

        ITreeAttribute aimStatTree;

        public float SecondsSinceAimStart
        {
            get { return (entity.World.ElapsedMilliseconds - aimStartMs) / 1000f; }
        }

        public float GetAimStat(string key, float defaultValue)
        {
            return aimStatTree?.GetFloat(key, defaultValue) ?? defaultValue;
        }

        public AccuracyModifier(EntityAgent entity)
        {
            this.entity = entity;
        }

        public virtual void BeginAim(ITreeAttribute aimStatTree)
        {
            aimStartMs = entity.World.ElapsedMilliseconds;
            this.aimStatTree = aimStatTree;
        }

        public virtual void EndAim() { }
        public virtual void OnHurt(float damage) { }
        public virtual void UpdateAccuracy(float dt, ref float accuracy) { }
        public virtual void UpdateDrift(float dt, ref float accuracy) { }
    }

    public class BaseAimingAccuracy : AccuracyModifier
    {
        public BaseAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void UpdateAccuracy(float dt, ref float accuracy)
        {
            float modacc = entity.Stats.GetBlended("rangedWeaponsAcc") - 1;
            float modspeed = entity.Stats.GetBlended("rangedWeaponsSpeed");

            // Initial accuracy increase
            accuracy = GameMath.Clamp((float)Math.Pow(SecondsSinceAimStart * modspeed * GetAimStat("accuracySpeed", 1.1f), 1.5), 0, GetAimStat("accuracyMax", 0.93f) - modacc);

            // Delayed accuracy loss
            accuracy -= GameMath.Clamp((SecondsSinceAimStart - GetAimStat("accuracyLossDelay", 1.75f)) * GetAimStat("accuracyLossSpeed", 0.3f), 0, GetAimStat("accuracyLossMax", 0.3f));

            // Small accuracy wobble
            if (SecondsSinceAimStart >= 0.75f)
            {
                accuracy += GameMath.Sin(SecondsSinceAimStart * GetAimStat("accuracyWobbleFrequency", 8f)) * GetAimStat("accuracyWobbleMagnitude", 0.012f);
            }
        }

        public override void UpdateDrift(float dt, ref float drift)
        {
            float modspeed = entity.Stats.GetBlended("rangedWeaponsSpeed");

            // Initial drift
            drift = GameMath.Clamp((float)Math.Pow(SecondsSinceAimStart * modspeed * GetAimStat("driftSpeed", 1f), 1.5), 0, 1);

            // Gradual increase in drift as accuracy loss mounts
            drift += GameMath.Clamp((SecondsSinceAimStart - GetAimStat("accuracyLossDelay", 1.75f)) * GetAimStat("driftLossIncreaseSpeed", 0.1f), 0, GetAimStat("driftLossIncreaseMax", 1f));
        }
    }

    /// <summary>
    /// Moving around decreases accuracy by 20% in 0.75 secconds
    /// </summary>
    public class MovingAimingAccuracy : AccuracyModifier
    {
        float moveAccuracyPenalty;
        float sprintAccuracyPenalty;

        public MovingAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void UpdateAccuracy(float dt, ref float accuracy)
        {
            float moveAccuracyStep = (entity.Controls.TriesToMove ? dt / GetAimStat("accuracyMoveLossTime", 0.15f) : -dt / GetAimStat("accuracyMoveRegainTime", 0.3f)) * GetAimStat("accuracyMoveLoss", 0.2f);
            float sprintAccuracyStep = (entity.Controls.TriesToMove && entity.Controls.Sprint ? dt / GetAimStat("accuracySprintLossTime", 0.225f) : -dt / GetAimStat("accuracySprintRegainTime", 0.45f)) * GetAimStat("accuracySprintLoss", 0.3f);

            moveAccuracyPenalty = GameMath.Clamp(moveAccuracyPenalty + moveAccuracyStep, 0, GetAimStat("accuracyMoveLoss", 0.2f));
            sprintAccuracyPenalty = GameMath.Clamp(sprintAccuracyPenalty + sprintAccuracyStep, 0, GetAimStat("accuracySprintLoss", 0.3f));

            accuracy -= (moveAccuracyPenalty + sprintAccuracyPenalty);
        }

        public override void EndAim()
        {
            moveAccuracyPenalty = 0f;
            sprintAccuracyPenalty = 0f;
        }
    }

    public class OnHurtAimingAccuracy : AccuracyModifier
    {
        float accuracyPenalty;

        public OnHurtAimingAccuracy(EntityAgent entity) : base(entity)
        {
        }

        public override void UpdateAccuracy(float dt, ref float accuracy)
        {
            accuracyPenalty = GameMath.Clamp(accuracyPenalty - dt / 3, 0, 0.4f);

            accuracy -= accuracyPenalty;
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
