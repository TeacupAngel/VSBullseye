using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Archery
{
    public class ArcheryItemSpear : ArcheryItemRangedWeapon
    {
        ItemStack spearItemStack;

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void StartAim(ItemSlot slot, EntityAgent entity)
        {
            entity.StartAnimation("aim");
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel))           
            {
                if (byEntity.World is IClientWorldAccessor)
                {
                    ModelTransform tf = new ModelTransform();
                    tf.EnsureDefaultValues();

                    float offset = GameMath.Clamp(secondsUsed * 4f, 0, 2f);

                    //tf.Translation.Set(-offset/4, 0, offset/3);
                    tf.Translation.Set(-offset/3, 0, offset / 3);
                    tf.Rotation.Set(0, -offset * 15, 0);

                    byEntity.Controls.UsingHeldItemTransformBefore = tf;
                }

                return true;
            }

            return false;
        }

        public override ItemStack GetProjectileItemStack(ItemSlot slot, EntityAgent byEntity)
        {
            spearItemStack = slot.TakeOut(1);
            slot.MarkDirty();
            spearItemStack.Collectible.DamageItem(byEntity.World, byEntity, new DummySlot(spearItemStack), 3); // Parametrize - 3 damage to item per throw

            return spearItemStack;
        }

        public override string GetProjectileEntityName(ItemSlot slot, EntityAgent byEntity)
        {
            return Attributes["spearEntityCode"].AsString();
        }

        public override float GetProjectileDamage(ItemSlot slot, EntityAgent byEntity)
        {
            float damage = 0f;

            if (spearItemStack.Collectible.Attributes != null)
            {
                damage += spearItemStack.Collectible.Attributes["damage"].AsFloat(0);
            }

            return damage;
        }

        public override float GetProjectileWeight(ItemSlot slot, EntityAgent byEntity)
        {
            return 0.3f;
        }

        public override void BeforeShot(ItemSlot slot, EntityAgent byEntity) 
        {
            byEntity.AnimManager.StopAnimation("aim");
        }

        public override void AfterShot(ItemSlot slot, EntityAgent byEntity) 
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            byEntity.StartAnimation("throw");

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                world.AddCameraShake(0.17f);
            }

            RefillSlotIfEmpty(slot, byEntity);
            //byPlayer?.InventoryManager.BroadcastHotbarSlot();

            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.5f);
        }

        // Melee attack
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            byEntity.Attributes.SetInt("didattack", 0);

            byEntity.World.RegisterCallback((dt) =>
            {
                IPlayer byPlayer = (byEntity as EntityPlayer).Player;
                if (byPlayer == null) return;

                if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
                {
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.5f);
                }
            }, 464);

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            float backwards = -Math.Min(0.35f, 2 * secondsPassed);
            float stab = Math.Min(1.2f, 20 * Math.Max(0, secondsPassed - 0.35f)); // + Math.Max(0, 5*(secondsPassed - 0.5f));

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float sum = stab + backwards;
                float ztranslation = Math.Min(0.2f, 1.5f * secondsPassed);
                float easeout = Math.Max(0, 10 * (secondsPassed - 1));

                if (secondsPassed > 0.4f) sum = Math.Max(0, sum - easeout);
                ztranslation = Math.Max(0, ztranslation - easeout);

                tf.Translation.Set(sum * 0.8f, 2.5f * sum / 3, -ztranslation);
                tf.Rotation.Set(sum * 10, sum * 2, sum * 25);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0)
                {
                    world.TryAttackEntity(entitySel);
                    byEntity.Attributes.SetInt("didattack", 1);
                    world.AddCameraShake(0.25f);
                }
            }

            return secondsPassed < 1.2f;
        }

        private void RefillSlotIfEmpty(ItemSlot slot, EntityAgent byEntity)
        {
            if (!slot.Empty) return;

            byEntity.WalkInventory((invslot) =>
            {
                if (invslot is ItemSlotCreative) return true;

                InventoryBase inv = invslot.Inventory;
                if (!(inv is InventoryBasePlayer) && !inv.HasOpened((byEntity as EntityPlayer).Player)) return true;

                if (invslot.Itemstack != null && invslot.Itemstack.Collectible is ArcheryItemSpear)
                {
                    invslot.TryPutInto(byEntity.World, slot);
                    invslot.Inventory.PerformNotifySlot(invslot.Inventory.GetSlotId(invslot));
                    slot.Inventory.PerformNotifySlot(slot.Inventory.GetSlotId(slot));

                    return false;
                }

                return true;
            });
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float damage = 1.5f;

            if (inSlot.Itemstack.Collectible.Attributes != null)
            {
                damage = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            dsc.AppendLine(damage + Lang.Get("piercing-damage-thrown"));
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-throw",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
