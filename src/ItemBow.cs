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

namespace Bullseye
{
    public class ItemBow : ItemRangedWeapon
    {
        WorldInteraction[] interactions;

        ItemSlot currentArrowSlot;
        float currentArrowDamage;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            weaponStats.weaponType = BullseyeRangedWeaponType.Bow;

            if (api.Side != EnumAppSide.Client) return;

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
                // Vanilla is broken, only shows 2 out of 3 charged states
                int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 3f / weaponStats.chargeTime), 0, 4);
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
                byEntity.AnimManager.StopAnimation("bowaim");
            }
        }

        /*public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            SkillItem[] mats = new SkillItem[be.MaterialIds.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Block block = api.World.GetBlock(be.MaterialIds[i]);
                ItemStack stack = new ItemStack(block);
                mats[i] = new SkillItem()
                {
                    Code = block.Code,
                    Data = be.MaterialIds[i],
                    Linebreak = i==0,
                    Name = block.GetHeldItemName(stack),
                    RenderHandler = (AssetLocation code, float dt, double atPosX, double atPosY) =>
                    {
                        float wdt = (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
                        ICoreClientAPI capi = api as ICoreClientAPI;
                        capi.Render.RenderItemstackToGui(stack, atPosX + wdt/2, atPosY + wdt/2, 50, wdt/2, ColorUtil.WhiteArgb, true, false, false);
                    }
                };
            }

            return mats;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            if (toolMode > 6)
            {
                slot.Itemstack.Attributes.SetInt("materialNum", toolMode - 7);
                return;
            }

            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }*/

        public override ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            ItemSlot arrowSlot = null;

            /*if (byEntity is EntityPlayer entityPlayer)
            {
                //entityPlayer.LeftHandItemSlot
            }*/

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

            // Arrow damage
            if (currentArrowSlot.Itemstack.Collectible.Attributes != null)
            {
                damage += currentArrowSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            // Bow damage
            if (weaponSlot.Itemstack.Collectible.Attributes != null)
            {
                damage *= 1f + weaponSlot.Itemstack.Collectible.Attributes["damagePercent"].AsFloat(0);
                damage += weaponSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }
            
            currentArrowDamage = damage;

            return damage;
        }

        public override float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            float breakChance = 0.5f;

            if (currentArrowSlot.Itemstack.ItemAttributes != null) {
                if (currentArrowSlot.Itemstack.ItemAttributes.KeyExists("averageLifetimeDamage"))
                {
                    breakChance = 1f / (currentArrowSlot.Itemstack.ItemAttributes["averageLifetimeDamage"].AsFloat() / currentArrowDamage);
                }
                else
                {
                    breakChance = currentArrowSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);
                }
            }

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

        public override void OnShot(ItemSlot slot, EntityAgent byEntity) 
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);

            byEntity.AnimManager.StartAnimation("bowhit");

            api.Event.RegisterCallback((ms) => {byEntity.AnimManager.StopAnimation("bowaim");}, 500);
        }

        public override void OnShotCancelled(ItemSlot slot, EntityAgent byEntity) 
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");
            }

            slot.Itemstack.Attributes.SetInt("renderVariant", 0);
            (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
            
            byEntity.AnimManager.StopAnimation("bowaim");
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("piercing-damage"));

            float dmgPercent = inSlot.Itemstack.Collectible.Attributes["damagePercent"].AsFloat(0) * 100f;
            if (dmgPercent != 0) dsc.AppendLine((dmgPercent > 0 ? "+" : "") + Lang.Get("bullseye:weapon-bonus-damage-ranged", dmgPercent));
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
