using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;

namespace Bullseye
{
	public class BullseyeInventoryAmmoSelect : InventoryBase
	{
		static int dummyId = 1;
		public string AmmoCategory {get; set;}

		List<ItemSlot> slots = new List<ItemSlot>();

		public EntityPlayer PlayerEntity {get; set;}

		public BullseyeInventoryAmmoSelect(ICoreAPI api) : this("inventoryAmmoSelect-" + (dummyId++), api)
		{
		}

		private BullseyeInventoryAmmoSelect(string inventoryID, ICoreAPI api) : base(inventoryID, api)
		{
		}

		private BullseyeInventoryAmmoSelect(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
		{
		}

		protected override ItemSlot NewSlot(int i)
		{
			return new BullseyeItemSlotAmmo(this);
		}

		public void SetAmmoStacks(List<ItemStack> ammoStacks)
		{
			if (ammoStacks.Count < Count)
			{
				slots.RemoveRange(ammoStacks.Count, Count - ammoStacks.Count);
			}

			for (int i = 0; i < ammoStacks.Count; i++)
			{
				this[i].Itemstack = ammoStacks[i];
			}
		}

		public void SetSelectedAmmoItemStack(ItemStack ammoItemStack)
		{
			foreach (ItemSlot itemSlot in this)
			{
				itemSlot.HexBackgroundColor = itemSlot.Itemstack.Equals(Api.World, ammoItemStack, GlobalConstants.IgnoredStackAttributes) ? "#ff8080" : null;
			}
		}

		public override object ActivateSlot(int slotId, ItemSlot mouseSlot, ref ItemStackMoveOperation op)
		{
			if (Api is ICoreClientAPI capi)
			{
				BullseyeSystemRangedWeapon rangedWeaponSystem = Api.ModLoader.GetModSystem<BullseyeSystemRangedWeapon>();

				ItemSlot selectedSlot = this[slotId];

				rangedWeaponSystem.EntitySetAmmoType((Api as ICoreClientAPI).World.Player.Entity, AmmoCategory, selectedSlot.Itemstack);
				rangedWeaponSystem.SendRangedWeaponAmmoSelectPacket(AmmoCategory, selectedSlot.Itemstack);

				capi.Gui.OpenedGuis.Find((dialog) => dialog is BullseyeGuiDialogAmmoSelect)?.TryClose();
			}

			return InvNetworkUtil.GetActivateSlotPacket(slotId, op);
		}

		public override ItemSlot this[int slotId]
		{
			get
			{
				if (slotId < 0) throw new ArgumentOutOfRangeException(nameof(slotId));
				if (slotId >= Count)
				{
					for (int i = Count; i <= slotId; i++)
					{
						slots.Add(NewSlot(i));
					}
				}
				return slots[slotId];
			}
			set
			{
				if (slotId < 0) throw new ArgumentOutOfRangeException(nameof(slotId));
				if (slotId >= Count)
				{
					for (int i = Count; i <= slotId; i++)
					{
						slots.Add(NewSlot(i));
					}
				}
				slots[slotId] = value ?? throw new ArgumentNullException(nameof(value));
			}
		}

		public override int Count => slots.Count;

		public override void FromTreeAttributes(ITreeAttribute tree)
		{
			slots = SlotsFromTreeAttributes(tree, null).ToList();
		}

		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			SlotsToTreeAttributes(slots.ToArray(), tree);
		}

		public override float GetTransitionSpeedMul(EnumTransitionType transType, ItemStack stack)
		{
			return 0;
		}
	}
}

