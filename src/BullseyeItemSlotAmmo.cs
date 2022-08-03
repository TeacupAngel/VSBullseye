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

			if (inventory is BullseyeInventoryAmmoSelect inventoryAmmoSelect && inventoryAmmoSelect.PlayerEntity?.RightHandItemSlot?.Itemstack?.Item is BullseyeItemRangedWeapon itemRangedWeapon)
			{
				float weaponDamage = itemRangedWeapon.GetProjectileDamage(inventoryAmmoSelect.PlayerEntity, inventoryAmmoSelect.PlayerEntity.RightHandItemSlot, this);
				
				if (this.Itemstack.ItemAttributes?["benefitsFromClass"].AsBool(true) ?? true)
				{
					weaponDamage *= inventoryAmmoSelect.PlayerEntity.Stats.GetBlended("rangedWeaponsDamage");
				}

				string weaponName = itemRangedWeapon.GetHeldItemName(inventoryAmmoSelect.PlayerEntity.RightHandItemSlot.Itemstack);

				stackDescription += "\n" + Lang.Get("bullseye:weapon-ranged-total-damage", weaponDamage, weaponName);
			}

			return stackDescription;
		}
	}
}
