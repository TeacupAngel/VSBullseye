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
    public class ItemBow : Item
    {
        WorldInteraction[] interactions;

        private ArcheryRangedWeaponSystem rangedWeaponSystem;

        ModelTransform defaultFpHandTransform;

        public override void OnLoaded(ICoreAPI api)
        {
            // Archery
            rangedWeaponSystem = api.ModLoader.GetModSystem<ArcheryRangedWeaponSystem>();

            defaultFpHandTransform = FpHandTransform.Clone();
            // /Archery

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "bowInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Code.Path.StartsWith("arrow-"))
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-chargebow",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });

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

        private double cooldownTime = 2;

        // Archery    
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor && !rangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, cooldownTime))
            {
                double cooldownRemaining = cooldownTime - rangedWeaponSystem.GetEntityCooldownTime(byEntity.EntityId);

                double transformTime = 0.25;
                // For spear, change it to only show the raising animation
                double transformFraction = GameMath.Clamp((cooldownTime - cooldownRemaining) / transformTime, 0f, 1f);
                transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);

                FpHandTransform.Translation.Y = defaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);
            }
        }
        // /Archery

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // Archery
            if (!rangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, cooldownTime))
            {
                handling = EnumHandHandling.NotHandled;
                return;
            }
            // /Archery

            ItemSlot invslot = GetNextArrow(byEntity);
            if (invslot == null) return;

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 1);

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.AnimManager.StartAnimation("bowaim");

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-draw"), byEntity, byPlayer, false, 8);

            handling = EnumHandHandling.PreventDefault;
        }

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
            }
            // /Archery

//            if (byEntity.World is IClientWorldAccessor)
            {
                int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 4), 0, 3);
                int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 0);

                slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
                slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

                if (prevRenderVariant != renderVariant)
                {
                    (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                }
            }
            
            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            //byEntity.AnimManager.StopAnimation("bowaim"); // Archery

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.AnimManager.StopAnimation("bowaim"); // Archery
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.Attributes.SetInt("shooting", 1);
            //byEntity.AnimManager.StopAnimation("bowaim"); // Archery

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            // Archery
            float chargeTime = api.Side == EnumAppSide.Server ? 0.75f : 0.75f + 0.1f; // slightly longer charge on client, for safety in case of desync

            if (secondsUsed < chargeTime) return;
            // /Archery

            ItemSlot arrowSlot = GetNextArrow(byEntity);
            if (arrowSlot == null) return;

            string arrowMaterial = arrowSlot.Itemstack.Collectible.FirstCodePart(1);
            float damage = 0;

            // Bow damage
            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage += slot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            // Arrow damage
            if (arrowSlot.Itemstack.Collectible.Attributes != null)
            {
                damage += arrowSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            ItemStack stack = arrowSlot.TakeOut(1);
            arrowSlot.MarkDirty();

            // Archery
            /*IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);*/
            // /Archery

            float breakChance = 0.5f;
            if (stack.ItemAttributes != null) breakChance = stack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("arrow-" + stack.Collectible.Variant["material"]));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityProjectile)entity).FiredBy = byEntity;
            ((EntityProjectile)entity).Damage = damage;
            ((EntityProjectile)entity).ProjectileStack = stack;
            ((EntityProjectile)entity).DropOnImpactChance = 1 - breakChance;

            float acc = Math.Max(0.001f, (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0)));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;
            
            // Archery
            //Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            //Vec3d aheadPos = pos.AheadCopy(1, byEntity.SidedPos.Pitch + rndpitch, byEntity.SidedPos.Yaw + rndyaw);
            //Vec3d velocity = (aheadPos - pos) * byEntity.Stats.GetBlended("bowDrawingStrength");

            // implement rndpitch/rndyaw
            Vec3d targetVec = Vec3d.Zero;

            if (byEntity.World.Side == EnumAppSide.Server)
            {
                targetVec = ArcheryCore.aimVectors[byEntity.EntityId];
            }  
            else
            {
                targetVec = ArcheryCore.targetVec;
            }

            Vec3d velocity = targetVec * byEntity.Stats.GetBlended("bowDrawingStrength");
            // /Archery
            
            entity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
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
            
            HasShot(byEntity);

            if (api.Side == EnumAppSide.Server)
            {
                rangedWeaponSystem.SendRangedWeaponFiredPacket(byEntity.EntityId, Id);
            }
            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);

            //byEntity.AnimManager.StartAnimation("bowhit");
            // /Archery
        }

        public void HasShot(EntityAgent byEntity)
        {
            if (byEntity.Attributes.GetInt("shooting") == 1)
            {
                byEntity.Attributes.SetInt("shooting", 0);

                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);

                rangedWeaponSystem.StartEntityCooldown(byEntity.EntityId);

                byEntity.AnimManager.StartAnimation("bowhit");

                api.Event.RegisterCallback((ms) => {byEntity.AnimManager.StopAnimation("bowaim");}, 500);
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("piercing-damage"));
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
