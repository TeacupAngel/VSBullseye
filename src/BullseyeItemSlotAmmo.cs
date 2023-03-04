using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Bullseye
{
	public class BullseyeItemSlotAmmo : ItemSlot
	{
		public BullseyeItemSlotAmmo(InventoryBase inventory, bool isBuyingSlot = false) : base(inventory)
		{
		}

		public override bool CanTake()
		{
			return false;
		}

		public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
		{
			return false;
		}

		public override bool CanHold(ItemSlot sourceSlot)
		{
			return false;
		}

		public override bool TryFlipWith(ItemSlot itemSlot)
		{
			return false;
		}

		protected override void FlipWith(ItemSlot withSlot)
		{
			return;
		}

		public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
		{
			
		}

		public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
		{
			return 0;
		}

		public override int TryPutInto(IWorldAccessor world, ItemSlot sinkSlot, int quantity = 1)
		{
			return 0;
		}

		public override string GetStackDescription(IClientWorldAccessor world, bool extendedDebugInfo)
		{
			string stackDescription = base.GetStackDescription(world, extendedDebugInfo);

			if (inventory is not BullseyeInventoryAmmoSelect inventoryAmmoSelect) return stackDescription;

			BullseyeCollectibleBehaviorRangedWeapon behaviorRangedWeapon = inventoryAmmoSelect.PlayerEntity?.RightHandItemSlot?.Itemstack?.Item.GetCollectibleBehavior<BullseyeCollectibleBehaviorRangedWeapon>(true);
			if (behaviorRangedWeapon == null) return stackDescription;

			float weaponDamage = behaviorRangedWeapon.GetProjectileDamage(inventoryAmmoSelect.PlayerEntity, inventoryAmmoSelect.PlayerEntity.RightHandItemSlot, this);
			
			float breakChance = 1f - behaviorRangedWeapon.GetProjectileDropChance(inventoryAmmoSelect.PlayerEntity, inventoryAmmoSelect.PlayerEntity.RightHandItemSlot, this);

			if (this.Itemstack.ItemAttributes?["benefitsFromClass"].AsBool(true) ?? true)
			{
				weaponDamage *= inventoryAmmoSelect.PlayerEntity.Stats.GetBlended("rangedWeaponsDamage");
			}

			string weaponName = behaviorRangedWeapon.collObj.GetHeldItemName(inventoryAmmoSelect.PlayerEntity.RightHandItemSlot.Itemstack);

			if (breakChance > 0f && breakChance < 1f)
			{
				stackDescription += "\n" + Lang.Get("bullseye:weapon-ranged-total-damage", weaponDamage, breakChance * 100f, weaponName);
			}
			else
			{
				stackDescription += "\n" + Lang.Get("bullseye:weapon-ranged-total-damage-no-drops", weaponDamage, weaponName);

				if (breakChance >= 1f) stackDescription += "\n" + Lang.Get("bullseye:projectile-always-breaks");
			}

			return stackDescription;
		}
	}
}
