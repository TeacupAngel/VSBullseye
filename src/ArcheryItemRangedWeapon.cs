using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Archery
{
    public class ArcheryItemAimAccuracy
    {
        public float AccuracySpeed = 1.1f;
        public float AccuracyMax = 0.93f;
        public float AccuracyLossDelay = 1.75f;
        public float AccuracyLossSpeed = 0.3f;
        public float AccuracyLossMax = 0.3f;
        public float AccuracyWobbleFrequency = 8f;
        public float AccuracyWobbleMagnitude = 0.012f;

        public float AccuracyMoveLoss = 0.2f;
        public float AccuracyMoveLossTime = 0.15f;
        public float AccuracyMoveRegainTime = 0.3f;
        public float AccuracySprintLoss = 0.3f;
        public float AccuracySprintLossTime = 0.225f;
        public float AccuracySprintRegainTime = 0.45f;

        public float DriftSpeed = 1f;
        public float DriftYaw = 0f;
        public float DriftPitch = 0f;
        public float DriftLossIncreaseSpeed = 0.1f;
        public float DriftLossIncreaseMax = 1f;
        public float DriftDecay = 0.8f;
    }

    public class ArcheryItemProjectile
    {
        public float Velocity = 30f;
        public float Spread = 0.75f;
        public float Zeroing = 0f;
        public float AccuracyBonus = 0f;
        public float LaunchVelocityBonus = 0f;
    }

    public class ArcheryItemRangedWeapon : Item
    {
        long lastShotTime;

        ItemSlot GetNextArrow(EntityAgent byEntity)
        {
            ItemSlot slot = null;
            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible.Code.Path.StartsWith("arrow-"))
                {
                    slot = invslot;
                    return false;
                }

                return true;
            });

            return slot;
        }

        public virtual bool CanShoot(EntityAgent entity)
        {
            return true;
        }

        public virtual void StartAim(ItemSlot slot, EntityAgent entity) {}

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // Fix - cooldown is per item, makes it broken entirely for spears
            if (byEntity.World.ElapsedMilliseconds - lastShotTime > 1500) // Parametrize - 1500 ms cooldown
            {
                if (!CanShoot(byEntity)) return;

                StartAim(slot, byEntity);

                ArcheryItemAimAccuracy aimStats = slot.Itemstack.Collectible.Attributes?["aimAccuracy"].AsObject<ArcheryItemAimAccuracy>();

                // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
                if (aimStats != null)
                {
                    ITreeAttribute aimStatTree = new TreeAttribute();

                    aimStatTree.SetFloat("accuracySpeed", aimStats.AccuracySpeed);
                    aimStatTree.SetFloat("accuracyMax", aimStats.AccuracyMax);
                    aimStatTree.SetFloat("accuracyLossDelay", aimStats.AccuracyLossDelay);
                    aimStatTree.SetFloat("accuracyLossSpeed", aimStats.AccuracyLossSpeed);
                    aimStatTree.SetFloat("accuracyLossMax", aimStats.AccuracyLossMax);
                    aimStatTree.SetFloat("accuracyWobbleFrequency", aimStats.AccuracyWobbleFrequency);
                    aimStatTree.SetFloat("accuracyWobbleMagnitude", aimStats.AccuracyWobbleMagnitude);

                    aimStatTree.SetFloat("accuracyMoveLoss", aimStats.AccuracyMoveLoss);
                    aimStatTree.SetFloat("accuracyMoveLossTime", aimStats.AccuracyMoveLossTime);
                    aimStatTree.SetFloat("accuracyMoveLossRegain", aimStats.AccuracyMoveRegainTime);
                    aimStatTree.SetFloat("accuracySprintLoss", aimStats.AccuracySprintLoss);
                    aimStatTree.SetFloat("accuracySprintLossTime", aimStats.AccuracySprintLossTime);
                    aimStatTree.SetFloat("accuracySprintLossRegain", aimStats.AccuracySprintRegainTime);

                    aimStatTree.SetFloat("driftYaw", aimStats.DriftYaw);
                    aimStatTree.SetFloat("driftPitch", aimStats.DriftPitch);
                    aimStatTree.SetFloat("driftSpeed", aimStats.DriftSpeed);
                    aimStatTree.SetFloat("driftLossIncreaseSpeed", aimStats.DriftLossIncreaseSpeed);
                    aimStatTree.SetFloat("driftLossIncreaseMax", aimStats.DriftLossIncreaseMax);
                    aimStatTree.SetFloat("driftDecay", aimStats.DriftDecay);

                    byEntity.Attributes.SetAttribute("aimStats", aimStatTree);
                }

                byEntity.Attributes.SetInt("aiming", 1);
            }

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aiming") == 0)
            {
                return false;
            }

            return true;
        }

        public virtual void CancelAim(ItemSlot slot, EntityAgent entity) {}

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aiming", 0);
                CancelAim(slot, byEntity);
            }

            return true;
        }

        public virtual ItemStack GetProjectileItemStack(ItemSlot slot, EntityAgent byEntity)
        {
            return null;
        }

        public virtual string GetProjectileEntityName(ItemSlot slot, EntityAgent byEntity)
        {
            throw new NotImplementedException("Projectile definition missing");
        }

        public virtual float GetProjectileDamage(ItemSlot slot, EntityAgent byEntity)
        {
            return 1f;
        }

        public virtual float GetProjectileBreakChance(ItemSlot slot, EntityAgent byEntity)
        {
            return -1f;
        }

        public virtual float GetProjectileWeight(ItemSlot slot, EntityAgent byEntity)
        {
            return 0.1f;
        }

        public virtual void BeforeShot(ItemSlot slot, EntityAgent byEntity) {}

        public virtual void AfterShot(ItemSlot slot, EntityAgent byEntity) {}

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.World.Logger.Notification("OnHeldInteractStop - BeforeShot");

            BeforeShot(slot, byEntity);

            if (byEntity.Attributes.GetInt("aiming") == 0) return;

            byEntity.Attributes.SetInt("aiming", 0);

            if (secondsUsed < 0.35f) return;
            //if (byEntity.Attributes.GetFloat("aimingAccuracy", 0) < 0.5f) return;

            // Restore this check, just in case something went wrong with the shooting conditions somehow
            //ItemSlot arrowSlot = GetNextArrow(byEntity);
            //if (arrowSlot == null) return;

            ArcheryItemProjectile projectileStats = null;

            // Turn into a virtual function like the rest of the stats?
            if (slot.Itemstack.Collectible.Attributes != null)
            {
                projectileStats = slot.Itemstack.Collectible.Attributes["projectile"].AsObject<ArcheryItemProjectile>();
            }

            ItemStack projectileItemStack = GetProjectileItemStack(slot, byEntity);

            float damage = GetProjectileDamage(slot, byEntity);

            float breakChance = GetProjectileBreakChance(slot, byEntity);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(GetProjectileEntityName(slot, byEntity)));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityProjectile)entity).FiredBy = byEntity;
            ((EntityProjectile)entity).Damage = damage;
            ((EntityProjectile)entity).ProjectileStack = projectileItemStack;
            ((EntityProjectile)entity).DropOnImpactChance = 1 - breakChance;
            ((EntityProjectile)entity).Weight = GetProjectileWeight(slot, byEntity);

            float acc = (1 - GameMath.Clamp((byEntity.Attributes.GetFloat("aimingAccuracy", 0) + (projectileStats?.AccuracyBonus ?? 0f)), 0, 1)); // parametrise - 0.03 accuracy bonus
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * (projectileStats?.Spread ?? 0.75f);
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * (projectileStats?.Spread ?? 0.75f);
            
            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.SidedPos.Pitch + rndpitch - (projectileStats?.Zeroing ?? 0f), byEntity.SidedPos.Yaw + rndyaw);  // parametrise - 0.03 zeroing angle
            // Default projectile velocity is 28.5 m/s for bows, 19.5 m/s for spears
            Vec3d velocity = (aheadPos - pos) * (projectileStats?.Velocity ?? 30f) * GlobalConstants.PhysicsFrameTime;

            float byEntityVelMultiplier = (projectileStats?.LaunchVelocityBonus ?? 0f);

            if (byEntityVelMultiplier > 0)
            {
                velocity = velocity + (byEntity.ServerPos.Motion * byEntityVelMultiplier);
            }

            entity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0));
            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            ((EntityProjectile)entity).SetRotation();

            byEntity.World.Logger.Notification("Projectile vel: {0}, player vel: {1}", entity.ServerPos.Motion.Length() / GlobalConstants.PhysicsFrameTime, byEntity.ServerPos.Motion.Length() / GlobalConstants.PhysicsFrameTime);

            byEntity.World.SpawnEntity(entity);

            AfterShot(slot, byEntity);

            lastShotTime = entity.World.ElapsedMilliseconds;

            byEntity.Attributes.RemoveAttribute("aimStats");

            byEntity.World.Logger.Notification("OnHeldInteractStop - AfterShot");
        }

        /*public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            capi.Input.MousePitch += 0.001f;
        }*/
    }
}
