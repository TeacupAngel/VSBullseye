using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Bullseye
{
	[Obsolete("Use BullseyeCollectibleBehaviorBow instead")]
	public class BullseyeItemBow : BullseyeItemRangedWeapon
	{
		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);

			WeaponStats.weaponType = BullseyeRangedWeaponType.Bow;
		}

		public override void OnAimingStart(ItemSlot slot, EntityAgent byEntity)
		{
			if (byEntity.World is IClientWorldAccessor)
			{
				slot.Itemstack.TempAttributes.SetInt("renderVariant", 1);

				GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.StartAnimation(new AnimationMetaData()
				{
					Animation = "Draw",
					Code = "draw",
					AnimationSpeed = 0.5f / GetChargeNeeded(api, byEntity),
					EaseOutSpeed = 6,
					EaseInSpeed = 15
				});
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
				int renderVariant = GameMath.Clamp((int)Math.Ceiling(secondsUsed * 3f / GetChargeNeeded(api, byEntity)), 0, 4);
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
			if (byEntity == null) return;

			if (byEntity.World is IClientWorldAccessor)
			{
				slot.Itemstack?.TempAttributes.RemoveAttribute("renderVariant");
			}

			slot.Itemstack?.Attributes.SetInt("renderVariant", 0);
			(byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

			if (cancelReason != EnumItemUseCancelReason.ReleasedMouse || secondsUsed < GetChargeNeeded(api, byEntity))
			{
				byEntity.AnimManager.StopAnimation("bowaim");

				if (byEntity.Api.Side == EnumAppSide.Client)
				{
					GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.StopAnimation("draw");
				}
			}
		}

		public override List<ItemStack> GetAvailableAmmoTypes(ItemSlot slot, IClientPlayer forPlayer)
		{
			if (AmmoType == null)
			{
				return null;
			}

			List<ItemStack> ammoTypes = new List<ItemStack>();

			forPlayer.Entity.WalkInventory((invslot) =>
			{
				if (invslot is ItemSlotCreative) return true;

				if (invslot.Itemstack != null && (AmmoType == invslot.Itemstack.ItemAttributes?["ammoType"].AsString() || invslot.Itemstack.Collectible.Code.Path.StartsWith("arrow-")))
				{
					ItemStack ammoStack = ammoTypes.Find(itemstack => itemstack.Equals(api.World, invslot.Itemstack, GlobalConstants.IgnoredStackAttributes));

					if (ammoStack == null)
					{
						ammoStack = invslot.Itemstack.GetEmptyClone();
						ammoStack.StackSize = invslot.StackSize;
						ammoTypes.Add(ammoStack);
					}
					else
					{
						ammoStack.StackSize += invslot.StackSize;
					}
				}

				return true;
			});

			if (ammoTypes.Count <= 0)
			{
				return null;
			}

			ammoTypes.Sort((ItemStack X, ItemStack Y) => {
				float xDamage = X.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;
				float yDamage = Y.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;

				return xDamage > yDamage ? 1 : (xDamage < yDamage ? -1 : String.Compare(X.GetName(), Y.GetName())); 
			});

			return ammoTypes;
		}

		public override ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot, bool isStartCheck = false)
		{
			if (AmmoType == null || byEntity == null || weaponSlot.Itemstack == null) return null;

			ItemSlot arrowSlot = null;
			ItemStack ammoType = isStartCheck ? GetEntitySelectedAmmoType(byEntity) : weaponSlot.Itemstack?.TempAttributes?.GetItemstack("loadedAmmo", null);

			byEntity.WalkInventory((invslot) =>
			{
				if (invslot == null || invslot is ItemSlotCreative) return true;

				if (invslot.Itemstack != null && (AmmoType == invslot.Itemstack.ItemAttributes?["ammoType"].AsString() || invslot.Itemstack.Collectible.Code.Path.StartsWith("arrow-")))
				{
					// If we found the selected ammo type or no ammo type is specifically selected, return the first one we find
					if (ammoType == null || invslot.Itemstack.Equals(api.World, ammoType, GlobalConstants.IgnoredStackAttributes))
					{
						arrowSlot = invslot;
						return false;
					}

					// Otherwise just get the first ammo stack we find, if we only just started drawing the bow
					if (arrowSlot == null && isStartCheck)
					{
						arrowSlot = invslot;
					}
				}

				return true;
			});

			if (isStartCheck && arrowSlot != null)
			{
				weaponSlot.Itemstack?.TempAttributes?.SetItemstack("loadedAmmo", arrowSlot.Itemstack);

				if (api is ICoreClientAPI capi)
				{
					ItemRenderInfo renderInfo = capi.Render.GetItemStackRenderInfo(arrowSlot, EnumItemRenderTarget.Ground);

					float arrowScale = weaponSlot.Itemstack?.Collectible?.Attributes?["arrowScale"].AsFloat(1) ?? 1f;

					renderInfo.Transform = renderInfo.Transform.Clone();
					renderInfo.Transform.ScaleXYZ.X = arrowScale;
					renderInfo.Transform.ScaleXYZ.Y = arrowScale;
					renderInfo.Transform.ScaleXYZ.Z = arrowScale;

					/*GetBehavior<CollectibleBehaviorAnimatableSimpleWithAttach>()?.SetAttachedRenderInfo(capi.TesselatorManager.GetDefaultItemMeshRef(arrowSlot.Itemstack.Item),
						capi.Render.GetItemStackRenderInfo(arrowSlot, EnumItemRenderTarget.Ground));*/

					GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.SetAttachedRenderInfo(renderInfo);
				}
			}

			return arrowSlot;
		}

		public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			float damage = 0f;

			// Arrow damage
			damage += ammoSlot.Itemstack?.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;

			// Bow damage
			damage *= (1f + weaponSlot.Itemstack?.Collectible?.Attributes?["damagePercent"].AsFloat(0) ?? 0f);
			damage += weaponSlot.Itemstack?.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0;

			damage *= ConfigSystem.GetSyncedConfig().ArrowDamage;

			return damage;
		}

		public override float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			float breakChance = 0.5f;

			if (ammoSlot.Itemstack.ItemAttributes != null) {
				if (ammoSlot.Itemstack.ItemAttributes.KeyExists("averageLifetimeDamage"))
				{
					breakChance = 1f / (ammoSlot.Itemstack.ItemAttributes["averageLifetimeDamage"].AsFloat() / GetProjectileDamage(byEntity, weaponSlot, ammoSlot));
				}
				else
				{
					breakChance = ammoSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0.5f);
				}
			}

			return 1f - breakChance;
		}

		public override EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			string entityCode = ammoSlot.Itemstack.Collectible.Attributes["entityCode"].AsString();

			if (entityCode != null) return byEntity.World.GetEntityType(new AssetLocation(entityCode));

			// Fallback for modded arrows that aren't made explicitly compatible
			return byEntity.World.GetEntityType(new AssetLocation("arrow-" + ammoSlot.Itemstack.Collectible.Variant["material"]));
		}

		public override int GetWeaponDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return 1;
		}

		public override void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity) 
		{
			if (byEntity.World is IClientWorldAccessor)
			{
				slot.Itemstack.TempAttributes.RemoveAttribute("renderVariant");

				GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.StopAnimation("draw", true);
				GetBehavior<BullseyeCollectibleBehaviorAnimatableAttach>()?.SetAttachedRenderInfo(null);
			}

			slot.Itemstack.Attributes.SetInt("renderVariant", 0);
			(byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

			IPlayer byPlayer = null;
			if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
			byEntity.World.PlaySoundAt(new AssetLocation("sounds/bow-release"), byEntity, byPlayer, false, 8);
			byEntity.AnimManager.StartAnimation("bowhit");

			api.Event.RegisterCallback((ms) => 
			{
				byEntity.AnimManager.StopAnimation("bowaim");
			}, 500);
		}

		public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

			if (inSlot.Itemstack.Collectible.Attributes == null) return;

			float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0) * ConfigSystem.GetSyncedConfig().ArrowDamage;
			if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("piercing-damage"));

			float dmgPercent = inSlot.Itemstack.Collectible.Attributes["damagePercent"].AsFloat(0) * 100f;
			if (dmgPercent != 0) dsc.AppendLine((dmgPercent > 0 ? "+" : "") + Lang.Get("bullseye:weapon-bonus-damage-ranged", dmgPercent));
		}
	}
}
