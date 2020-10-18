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

using Vintagestory.GameContent;

namespace Archery
{
    public class ArcheryItemBow : ArcheryItemRangedWeapon
    {
        WorldInteraction[] interactions;

        long lastShotTime;

        ItemSlot arrowItemSlot;
        ItemStack currentArrowItemStack;

        public override void OnLoaded(ICoreAPI api)
        {
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

        public override bool CanShoot(EntityAgent entity)
        {
            arrowItemSlot = GetNextArrow(entity);
            
            return arrowItemSlot != null;
        }

        public override void StartAim(ItemSlot slot, EntityAgent entity)
        {
            if (entity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 1);

            entity.AnimManager.StartAnimation("bowaim");

            IPlayer byPlayer = null;
            if (entity is EntityPlayer) byPlayer = entity.World.PlayerByUid(((EntityPlayer)entity).PlayerUID);
            entity.World.PlaySoundAt(new AssetLocation("sounds/bow-draw"), entity, byPlayer, false, 8);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel))           
            {
                int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 4), 0, 3);
                int prevRenderVariant = slot.Itemstack.Attributes.GetInt("renderVariant", 0);

                slot.Itemstack.TempAttributes.SetInt("renderVariant", renderVariant);
                slot.Itemstack.Attributes.SetInt("renderVariant", renderVariant);

                if (prevRenderVariant != renderVariant)
                {
                    (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
                }

                return true;
            }

            return false;
        }

        public override ItemStack GetProjectileItemStack(ItemSlot slot, EntityAgent byEntity)
        {
            currentArrowItemStack = arrowItemSlot.TakeOut(1);
            arrowItemSlot.MarkDirty();

            return currentArrowItemStack;
        }

        public override string GetProjectileEntityName(ItemSlot slot, EntityAgent byEntity)
        {
            return "arrow-" + currentArrowItemStack.Collectible.Variant["material"];
        }

        public override float GetProjectileDamage(ItemSlot slot, EntityAgent byEntity)
        {
            float damage = 0f;

            // Bow damage
            if (slot.Itemstack.Collectible.Attributes != null)
            {
                damage += slot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            // Arrow damage
            if (currentArrowItemStack.Collectible.Attributes != null)
            {
                damage += currentArrowItemStack.Collectible.Attributes["damage"].AsFloat(0);
            }

            return damage;
        }

        public override float GetProjectileBreakChance(ItemSlot slot, EntityAgent byEntity)
        {
            return (currentArrowItemStack.ItemAttributes != null) ? currentArrowItemStack.ItemAttributes["breakChanceOnImpact"].AsFloat(-1f) : -1f;
        }

        public override void BeforeShot(ItemSlot slot, EntityAgent byEntity) 
        {
            byEntity.AnimManager.StopAnimation("bowaim");

            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
        }

        public override void AfterShot(ItemSlot slot, EntityAgent byEntity) 
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);

            byEntity.AnimManager.StartAnimation("bowhit");

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                world.AddCameraShake(0.2f);
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
