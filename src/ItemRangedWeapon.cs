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

namespace Bullseye
{
    public class ItemRangedWeapon : Item
    {
        protected BullseyeRangedWeaponSystem rangedWeaponSystem;
        protected BullseyeRangedWeaponStats weaponStats;

        protected ModelTransform defaultFpHandTransform;

        protected int aimTextureId = -1;
        protected int aimTextureBlockedId = -1;

        public override void OnLoaded(ICoreAPI api)
        {
            rangedWeaponSystem = api.ModLoader.GetModSystem<BullseyeRangedWeaponSystem>();

            defaultFpHandTransform = FpHandTransform.Clone();

            weaponStats = Attributes.KeyExists("bullseyeWeaponStats") ? Attributes?["bullseyeWeaponStats"].AsObject<BullseyeRangedWeaponStats>() : new BullseyeRangedWeaponStats();

            if (api.Side == EnumAppSide.Server)
            {
                api.Event.RegisterEventBusListener(ServerHandleFire, 0.5, "bullseyeRangedWeaponFire");
            }
            else
            {
                BullseyeReticleLoadSystem reticleLoadSystem = api.ModLoader.GetModSystem<BullseyeReticleLoadSystem>();
                if (weaponStats.aimTexturePath != null) aimTextureId = reticleLoadSystem.GetReticleTextureId(weaponStats.aimTexturePath);
                if (weaponStats.aimTextureBlockedPath != null) aimTextureBlockedId = reticleLoadSystem.GetReticleTextureId(weaponStats.aimTextureBlockedPath);
            }
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        // Keeping this as backup in case OnBeforeRender has some hidden flaw
        /*public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                float transformFraction;

                if (!rangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, weaponStats.cooldownTime))
                {
                    float cooldownRemaining = weaponStats.cooldownTime - rangedWeaponSystem.GetEntityCooldownTime(byEntity.EntityId);
                    float transformTime = 0.25f;

                    transformFraction = weaponStats.weaponType != BullseyeRangedWeaponType.Throw ? 
                        GameMath.Clamp((weaponStats.cooldownTime - cooldownRemaining) / transformTime, 0f, 1f) : 1f;
                    transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);
                }
                else
                {
                    transformFraction = 0;
                }

                FpHandTransform.Translation.Y = defaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);
            }
        }*/

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandFp)
            {
                float transformFraction;

                if (!rangedWeaponSystem.HasEntityCooldownPassed(capi.World.Player.Entity.EntityId, weaponStats.cooldownTime))
                {
                    float cooldownRemaining = weaponStats.cooldownTime - rangedWeaponSystem.GetEntityCooldownTime(capi.World.Player.Entity.EntityId);
                    float transformTime = 0.25f;

                    transformFraction = weaponStats.weaponType != BullseyeRangedWeaponType.Throw ? 
                        GameMath.Clamp((weaponStats.cooldownTime - cooldownRemaining) / transformTime, 0f, 1f) : 1f;
                    transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);
                }
                else
                {
                    transformFraction = 0;
                }

                renderinfo.Transform.Translation.Y = defaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);

                if (BullseyeCore.aiming)
                {
                    Vec2f currentAim = BullseyeCore.GetCurrentAim();

                    renderinfo.Transform.Rotation.X = defaultFpHandTransform.Rotation.X - (currentAim.Y / 15f); 
                    renderinfo.Transform.Rotation.Y = defaultFpHandTransform.Rotation.Y - (currentAim.X / 15f);
                }
                else
                {
                    renderinfo.Transform.Rotation.Set(defaultFpHandTransform.Rotation);
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!rangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, weaponStats.cooldownTime))
            {
                handling = EnumHandHandling.NotHandled;
                return;
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                BullseyeCore.SetClientRangedWeaponStats(weaponStats);
                BullseyeCore.SetClientRangedWeaponReticleTextures(aimTextureId, aimTextureBlockedId);
            }
            
            if (api.Side == EnumAppSide.Server) {
                rangedWeaponSystem.SetLastEntityRangedChargeData(byEntity.EntityId, slot);
            }

            byEntity.GetBehavior<EntityBehaviorAimingAccuracy>().SetRangedWeaponStats(weaponStats);

            ItemSlot invslot = GetNextAmmoSlot(byEntity, slot);
            if (invslot == null) return;

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("bullseyeAiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);

            OnAimingStart(slot, byEntity);
            handling = EnumHandHandling.PreventDefault;
        }

        public virtual void OnAimingStart(ItemSlot slot, EntityAgent byEntity) {}

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                // Show different crosshair if we are ready to shoot
                SystemRenderAimPatch.readyToShoot = secondsUsed > weaponStats.chargeTime + 0.1f;
            }

            if (byEntity.Attributes.GetInt("bullseyeAiming") == 1)
            {
                OnAimingStep(secondsUsed, slot, byEntity);
            }
            
            return true;
        }

        public virtual void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity) {}

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("bullseyeAiming", 0);

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

        public virtual int GetProjectileDamageOnImpact(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 0;
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
            byEntity.Attributes.SetInt("bullseyeAiming", 0);

            float chargeNeeded = api.Side == EnumAppSide.Server ? weaponStats.chargeTime : weaponStats.chargeTime + 0.1f; // slightly longer charge on client, for safety in case of desync

            if (secondsUsed < chargeNeeded)
            {
                OnShotCancelled(slot, byEntity);
                return;
            }

            if (api.Side != EnumAppSide.Client) 
            {
                // Just to make sure animations etc. get stopped if a shot looks legit on the server but was stopped on the client
                api.Event.RegisterCallback((ms) => {if (byEntity.Attributes.GetInt("bullseyeAiming") == 0) OnShotCancelled(slot, byEntity);}, 500);
                return;
            }

            Vec3d targetVec = BullseyeCore.targetVec;

            Shoot(slot, byEntity, targetVec);

            rangedWeaponSystem.SendRangedWeaponFirePacket(byEntity.EntityId, Id, targetVec);
        }

        public void Shoot(ItemSlot slot, EntityAgent byEntity, Vec3d targetVec)
        {
            byEntity.Attributes.SetInt("bullseyeAiming", 0);

            ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, slot);
            if (ammoSlot == null) return;

            //string arrowMaterial = arrowSlot.Itemstack.Collectible.FirstCodePart(1);
            float damage = GetProjectileDamage(byEntity, slot);
            float dropChance = GetProjectileDropChance(byEntity, slot);
            float weight = GetProjectileWeight(byEntity, slot);
            bool damageStackOnImpact = GetProjectileDamageOnImpact(byEntity, slot) > 0;

            EntityProperties type = GetProjectileEntityType(byEntity, slot);

            // If we need to damage the projectile by more than 1 durability per shot, do it here, but leave at least 1 durability
            if (GetProjectileDamageOnImpact(byEntity, slot) > 1)
            {
                int durability = slot.Itemstack.Attributes.GetInt("durability", Durability);

                int projectileDamage = GetProjectileDamageOnImpact(byEntity, slot) - 1;
                projectileDamage = projectileDamage >= durability ? durability - 1 : projectileDamage;

                slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, projectileDamage);
            }

            ItemStack stack = ammoSlot.TakeOut(1);
            ammoSlot.MarkDirty();

            EntityProjectile entity = byEntity.World.ClassRegistry.CreateEntity(type) as EntityProjectile;;
            entity.FiredBy = byEntity;
            entity.Damage = damage;
            entity.ProjectileStack = stack;
            entity.DropOnImpactChance = dropChance;
            entity.DamageStackOnImpact = damageStackOnImpact;
            entity.Weight = weight;

            // Might as well reuse these for now
            double spreadAngle = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1);
            double spreadMagnitude = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1);

            // New method to generate random spread, works when aimed straight up/straight down
            //Vec3d targetVec = byEntity.World.Side == EnumAppSide.Server ? BullseyeCore.aimVectors[byEntity.EntityId] : BullseyeCore.targetVec;

            Vec3d perp = MathHelper.Vec3GetPerpendicular(targetVec);
            Vec3d perp2 = targetVec.Cross(perp);

            double angle = spreadAngle * (GameMath.PI * 2f);
            double offsetAngle = spreadMagnitude *  weaponStats.projectileSpread * GameMath.DEG2RAD;

            double magnitude = GameMath.Tan(offsetAngle);

            Vec3d deviation = magnitude * perp * GameMath.Cos(angle) + magnitude * perp2 * GameMath.Sin(angle);
            Vec3d newAngle = (targetVec + deviation) * (targetVec.Length() / (targetVec.Length() + deviation.Length()));

            //Vec3d velocity = targetVec * byEntity.Stats.GetBlended("bowDrawingStrength") * (weaponStats.projectileVelocity * GlobalConstants.PhysicsFrameTime);
            Vec3d velocity = newAngle * byEntity.Stats.GetBlended("bowDrawingStrength") * (weaponStats.projectileVelocity * GlobalConstants.PhysicsFrameTime);
            // What the heck? Server's SidedPos.Motion is somehow twice that of client's!
            velocity += api.Side == EnumAppSide.Client ? byEntity.SidedPos.Motion : byEntity.SidedPos.Motion / 2;
            
            // Used in vanilla spears but feels awful, might redo later with zeroing and proper offset to the right
            //entity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0));
            entity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            ((EntityProjectile)entity).SetRotation();

            byEntity.World.SpawnEntity(entity);

            EntityPlayer entityPlayer = byEntity as EntityPlayer;

            if (byEntity.World.Side == EnumAppSide.Server && entityPlayer != null)
            {
                BullseyeCore.serverInstance.SetFollowArrow((EntityProjectile)entity, entityPlayer);
            }

            rangedWeaponSystem.StartEntityCooldown(byEntity.EntityId);

            OnShot(slot, byEntity);

            int weaponDamage = GetWeaponDamageOnShot(byEntity, slot);

            if (weaponDamage > 0)
            {
                slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, weaponDamage);
            }
        }

        public virtual void OnShot(ItemSlot slot, EntityAgent byEntity) {}
        public virtual void OnShotCancelled(ItemSlot slot, EntityAgent byEntity) {}

        private void ServerHandleFire(string eventName, ref EnumHandling handling, IAttribute data)
        {
            TreeAttribute tree = data as TreeAttribute;
            int itemId = tree.GetInt("itemId");

            if (itemId == Id)
            {
                long entityId = tree.GetLong("entityId");

                ItemSlot itemSlot = rangedWeaponSystem.GetLastEntityRangedItemSlot(entityId);

                if (rangedWeaponSystem.GetEntityChargeStart(entityId) + weaponStats.chargeTime < api.World.ElapsedMilliseconds / 1000f && itemSlot != null)
                {
                    EntityAgent byEntity =  api.World.GetEntityById(entityId) as EntityAgent;
                    Vec3d targetVec = new Vec3d(tree.GetDouble("aimX"), tree.GetDouble("aimY"), tree.GetDouble("aimZ"));

                    Shoot(itemSlot, byEntity, targetVec);
                    
                    handling = EnumHandling.PreventSubsequent;
                }
            }
        }
    }
}