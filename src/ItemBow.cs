using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using System.Globalization;

namespace Archery
{
    public class ItemBow : ItemRangedWeapon
    {
        WorldInteraction[] interactions;

        private ArcheryRangedWeaponSystem rangedWeaponSystem;
        ArcheryRangedWeaponStats weaponStats;

        ItemSlot currentArrowSlot;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

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

        public override void OnAimingStart(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 1);

            byEntity.AnimManager.StartAnimation("bowaim");

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-draw"), byEntity, byPlayer, false, 8);
        }

        public override void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            //if (byEntity.World is IClientWorldAccessor)
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
        }

        public override void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) 
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.AnimManager.StopAnimation("bowaim"); // Archery
            }
        }

        public override ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            ItemSlot arrowSlot = null;
            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible.Code.Path.StartsWith("arrow-"))
                {
                    arrowSlot = invslot;
                    currentArrowSlot = invslot;
                    return false;
                }

                return true;
            });

            return arrowSlot;
        }

        public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            float damage = 0f;

            // Bow damage
            if (weaponSlot.Itemstack.Collectible.Attributes != null)
            {
                damage += weaponSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            // Arrow damage
            if (currentArrowSlot.Itemstack.Collectible.Attributes != null)
            {
                damage += currentArrowSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            return damage;
        }

        public override float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            float breakChance = 0.5f;
            if (currentArrowSlot.Itemstack.ItemAttributes != null) breakChance = currentArrowSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);
            return 1f - breakChance;
        }

        public override EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return byEntity.World.GetEntityType(new AssetLocation("arrow-" + currentArrowSlot.Itemstack.Collectible.Variant["material"]));
        }

        public override int GetWeaponDamageOnShot(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 1;
        }

        public override void OnShotImmediate(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
        }

        public override void OnShotConfirmed(EntityAgent byEntity) 
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);

            byEntity.AnimManager.StartAnimation("bowhit");

            api.Event.RegisterCallback((ms) => {byEntity.AnimManager.StopAnimation("bowaim");}, 500);
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
