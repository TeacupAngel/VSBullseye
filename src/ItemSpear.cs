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
    public class ItemSpear : ItemRangedWeapon
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            weaponStats.weaponType = ArcheryRangedWeaponType.Throw;
        }

        public override void OnAimingStart(ItemSlot slot, EntityAgent byEntity)
        {
            byEntity.StartAnimation("aim");
        }

        public override void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 4f, 0, 2f);

                tf.Translation.Set(-offset/3, 0, offset / 3);
                tf.Rotation.Set(0, -offset * 15, 0);

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }
        }

        public override void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) 
        {
            byEntity.StopAnimation("aim");
        }

        public override ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return weaponSlot;
        }

        public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            float damage = 0f;

            if (weaponSlot.Itemstack.Collectible.Attributes != null)
            {
                damage = weaponSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            }

            return damage;
        }

        public override float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 0.3f;
        }

        public override int GetProjectileDamageOnImpact(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return 3;
        }

        public override EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot)
        {
            return byEntity.World.GetEntityType(new AssetLocation(Attributes["spearEntityCode"].AsString()));
        }

        public override void OnShot(ItemSlot slot, EntityAgent byEntity) 
        {
            if (byEntity is EntityPlayer) RefillSlotIfEmpty(slot, byEntity, (itemstack) => itemstack.Collectible is ItemSpear);

            byEntity.StopAnimation("aim");

            (api as ICoreClientAPI)?.World.AddCameraShake(0.17f);

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            byEntity.StartAnimation("throw");

            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.5f);
        }

        // Spear melee specific
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

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {

        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Collectible.Attributes == null) return;

            float damage = 0f;

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
