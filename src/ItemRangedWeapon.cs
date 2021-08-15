using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using System.Globalization;

namespace Archery
{
    public class ItemRangedWeapon : Item
    {
        //WorldInteraction[] interactions;

        private ArcheryRangedWeaponSystem rangedWeaponSystem;
        ArcheryRangedWeaponStats weaponStats;

        ModelTransform defaultFpHandTransform;

        public override void OnLoaded(ICoreAPI api)
        {
            // Archery
            rangedWeaponSystem = api.ModLoader.GetModSystem<ArcheryRangedWeaponSystem>();

            defaultFpHandTransform = FpHandTransform.Clone();

            weaponStats = Attributes.KeyExists("archeryWeaponStats") ? Attributes?["archeryWeaponStats"].AsObject<ArcheryRangedWeaponStats>() : new ArcheryRangedWeaponStats();
            // /Archery

            if (api.Side != EnumAppSide.Client) return;

            // Archery
            api.Event.RegisterEventBusListener(OnServerFired, 0.5, "archeryRangedWeaponFired");
            // /Archery
        }

        private void OnServerFired(string eventName, ref EnumHandling handling, IAttribute data)
        {
            TreeAttribute tree = data as TreeAttribute;
            EntityAgent byEntity =  api.World.GetEntityById(tree.GetLong("entityId")) as EntityAgent;
            int itemId = tree.GetInt("itemId");

            if (itemId == Id)
            {
                HasShot(byEntity);
                handling = EnumHandling.PreventSubsequent;
            }
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        // Archery    
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                // For spear, change it to only show the raising animation
                float transformFraction;

                if (!rangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, weaponStats.cooldownTime))
                {
                    float cooldownRemaining = weaponStats.cooldownTime - rangedWeaponSystem.GetEntityCooldownTime(byEntity.EntityId);
                    float transformTime = 0.25f;

                    transformFraction = GameMath.Clamp((weaponStats.cooldownTime - cooldownRemaining) / transformTime, 0f, 1f);
                    transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);
                }
                else
                {
                    transformFraction = 0;
                }

                FpHandTransform.Translation.Y = defaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);
            }
        }
        // /Archery

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // Archery
            if (!rangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, weaponStats.cooldownTime))
            {
                handling = EnumHandHandling.NotHandled;
                return;
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                ClientMainPatch.SetRangedWeaponStats(weaponStats);
            }

            byEntity.GetBehavior<EntityBehaviorAimingAccuracy>().SetRangedWeaponStats(weaponStats);
            // /Archery

            ItemSlot invslot = GetNextAmmoSlot(byEntity, slot);
            if (invslot == null) return;

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("archeryAiming", 1); // Archery
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.AnimManager.StartAnimation("bowaim");

            OnAimingStart(slot, byEntity);
            handling = EnumHandHandling.PreventDefault;
        }

        public virtual void OnAimingStart(ItemSlot slot, EntityAgent byEntity) {}

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            // Archery    
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                Vec2f currentAim = ArcheryCore.GetCurrentAim();

                tf.Rotation.Set(-currentAim.X / 15f, currentAim.Y / 15f, 0);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                // Show different crosshair if we are ready to shoot
                SystemRenderAimPatch.readyToShoot = secondsUsed > weaponStats.chargeTime + 0.1f;
            }
            // /Archery

            OnAimingStep(secondsUsed, slot, byEntity);
            
            return true;
        }

        public virtual void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity) {}

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("archeryAiming", 0);  // Archery

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            OnAimingCancel(secondsUsed, slot, byEntity, cancelReason);

            return true;
        }

        public virtual void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) {}

        public virtual ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            throw new NotImplementedException(String.Format("Item {0} does not have implementation for GetNextAmmoSlot()!", Code));
        }

        public virtual float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 0f;
        }

        public virtual float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 1.1f;
        }

        public virtual float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 0.1f;
        }

        public virtual bool GetProjectileDamageOnImpact(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return false;
        }

        public virtual EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            throw new NotImplementedException(String.Format("Item {0} does not have implementation for GetProjectileEntityType()!", Code));
        }

        public virtual int GetWeaponDamageOnShot(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 0;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
            byEntity.Attributes.SetInt("archeryAiming", 0);  // Archery
            byEntity.Attributes.SetInt("shooting", 1);

            // Archery
            float chargeNeeded = api.Side == EnumAppSide.Server ? weaponStats.chargeTime : weaponStats.chargeTime + 0.1f; // slightly longer charge on client, for safety in case of desync

            if (secondsUsed < chargeNeeded) return;
            // /Archery

            ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, slot);
            if (ammoSlot == null) return;

            //string arrowMaterial = arrowSlot.Itemstack.Collectible.FirstCodePart(1);
            float damage = GetProjectileDamage(byEntity, slot);
            float dropChance = GetProjectileDropChance(byEntity, slot);
            float weight = GetProjectileWeight(byEntity, slot);
            bool damageStackOnImpact = GetProjectileDamageOnImpact(byEntity, slot);

            EntityProperties type = GetProjectileEntityType(byEntity, slot);

            ItemStack stack = ammoSlot.TakeOut(1);
            ammoSlot.MarkDirty();

            EntityProjectile entity = byEntity.World.ClassRegistry.CreateEntity(type) as EntityProjectile;;
            entity.FiredBy = byEntity;
            entity.Damage = damage;
            entity.ProjectileStack = stack;
            entity.DropOnImpactChance = dropChance;
            entity.DamageStackOnImpact = damageStackOnImpact;
            entity.Weight = weight;

            // Archery
            // Might as well reuse these for now
            double spreadAngle = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1);
            double spreadMagnitude = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1);

            // New method to generate random spread, works when aimed straight up/straight down
            Vec3d targetVec = byEntity.World.Side == EnumAppSide.Server ? ArcheryCore.aimVectors[byEntity.EntityId] : ArcheryCore.targetVec;

            Vec3d perp = MathHelper.Vec3GetPerpendicular(targetVec);
            Vec3d perp2 = targetVec.Cross(perp);

            double angle = spreadAngle * (GameMath.PI * 2f);
            double offsetAngle = spreadMagnitude *  weaponStats.projectileSpread * GameMath.DEG2RAD;

            double magnitude = GameMath.Tan(offsetAngle);

            Vec3d deviation = magnitude * perp * GameMath.Cos(angle) + magnitude * perp2 * GameMath.Sin(angle);
            Vec3d newAngle = (targetVec + deviation) * (targetVec.Length() / (targetVec.Length() + deviation.Length()));

            //Vec3d velocity = targetVec * byEntity.Stats.GetBlended("bowDrawingStrength") * (weaponStats.projectileVelocity * GlobalConstants.PhysicsFrameTime);
            Vec3d velocity = newAngle * byEntity.Stats.GetBlended("bowDrawingStrength") * (weaponStats.projectileVelocity * GlobalConstants.PhysicsFrameTime);
            // /Archery
            
            entity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0));
            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            ((EntityProjectile)entity).SetRotation();

            byEntity.World.SpawnEntity(entity);

            // Archery
            EntityPlayer entityPlayer = byEntity as EntityPlayer;

            if (byEntity.World.Side == EnumAppSide.Server && entityPlayer != null)
            {
                ArcheryCore.serverInstance.SetFollowArrow((EntityProjectile)entity, entityPlayer);
            }
            
            OnShotImmediate(secondsUsed, slot, byEntity, blockSel, entitySel);
            HasShot(byEntity);

            if (api.Side == EnumAppSide.Server)
            {
                rangedWeaponSystem.SendRangedWeaponFiredPacket(byEntity.EntityId, Id);
            }

            int weaponDamage = GetWeaponDamageOnShot(byEntity, slot);

            if (weaponDamage > 0)
            {
                slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, weaponDamage);
            }
            // /Archery
        }

        public virtual void OnShotImmediate(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {}

        public void HasShot(EntityAgent byEntity)
        {
            if (byEntity.Attributes.GetInt("shooting") == 1)
            {
                byEntity.Attributes.SetInt("shooting", 0);

                rangedWeaponSystem.StartEntityCooldown(byEntity.EntityId);

                OnShotConfirmed(byEntity);
            }
        }

        public virtual void OnShotConfirmed(EntityAgent byEntity) {}
    }
}
