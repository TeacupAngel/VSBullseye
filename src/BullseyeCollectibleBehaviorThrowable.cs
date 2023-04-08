using System;
using System.Text;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Bullseye
{
	public class BullseyeCollectibleBehaviorThrowable : BullseyeCollectibleBehaviorRangedWeapon
	{
		public BullseyeCollectibleBehaviorThrowable(CollectibleObject collObj) : base(collObj) {}

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);

			WeaponStats.weaponType = BullseyeRangedWeaponType.Throw;
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

				float offset = GameMath.Serp(0, 2, GameMath.Clamp(secondsUsed * 4f, 0, 2f) / 2f);

				tf.Translation.Set(0, offset / 5, offset / 3);
				tf.Rotation.Set(offset * 10, 0, 0);
				byEntity.Controls.UsingHeldItemTransformAfter = tf;
			}
		}

		public override void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) 
		{
			byEntity?.StopAnimation("aim");
		}

		public override List<ItemStack> GetAvailableAmmoTypes(ItemSlot slot, IClientPlayer forPlayer)
		{
			return null;
		}

		public override ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot, bool isStartCheck = false) => weaponSlot;

		public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return weaponSlot.Itemstack?.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;
		}

		public override float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0.2f;
		public override int GetProjectileDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0;

		public override EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			string entityCode = collObj.Attributes["projectileEntityCode"].AsString();

			return byEntity.World.GetEntityType(new AssetLocation(entityCode));
		}

		public override void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity) 
		{
			byEntity.StopAnimation("aim");

			if (byEntity is EntityPlayer) 
			{
				string refillIdentifier = collObj.Attributes?["slotRefillIdentifier"].ToString();

				collObj.RefillSlotIfEmpty(slot, byEntity as EntityAgent, (stack) => {
					return refillIdentifier != null ? stack.ItemAttributes?["slotRefillIdentifier"]?.ToString() == refillIdentifier : stack.Collectible.Id == collObj.Id;
				});
			}

			(api as ICoreClientAPI)?.World.AddCameraShake(0.17f);

			IPlayer byPlayer = null;
			if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
			byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

			byEntity.StartAnimation("throw");

			float pitch = (byEntity as EntityPlayer)?.talkUtil.pitchModifier ?? 1f;
            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, pitch * 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);
		}
	}
}
