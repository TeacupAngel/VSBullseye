using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Bullseye
{
	public class BullseyeGuiDialogAmmoSelect : GuiDialog
	{
		private BullseyeInventoryAmmoSelect inventoryAmmoSelect;

		public override string ToggleKeyCombinationCode => "bullseye.ammotypeselect";
		public override bool PrefersUngrabbedMouse => false;

		public BullseyeGuiDialogAmmoSelect(ICoreClientAPI api)	: base(api)
		{
			inventoryAmmoSelect = new BullseyeInventoryAmmoSelect(api);
		}

		/*private void OnEventBusEvent(string eventName, ref EnumHandling handling, IAttribute data)
		{
			capi.Event.EnqueueMainThreadTask(() =>
			{
				TryOpen();
			}, "reopentoolmodedlg");
		}*/

		// internal won't be removed from this method until 1.17 :(
		/*internal override bool OnKeyCombinationToggle(KeyCombination viaKeyComb)
		{
			ItemSlot itemSlot = capi.World.Player?.InventoryManager?.ActiveHotbarSlot;
			if (itemSlot?.Itemstack?.Collectible.GetToolModes(itemSlot, capi.World.Player, capi.World.Player.CurrentBlockSelection) == null)
			{
				return false;
			}
			blockSele = capi.World.Player.CurrentBlockSelection?.Clone();
			return base.OnKeyCombinationToggle(viaKeyComb);
		}*/

		public override bool TryOpen()
		{
			ItemSlot activeHotbarSlot = capi.World.Player?.InventoryManager?.ActiveHotbarSlot;
			BullseyeItemRangedWeapon rangedWeaponItem = (activeHotbarSlot?.Itemstack?.Collectible) as BullseyeItemRangedWeapon;

			if (rangedWeaponItem?.AmmoType == null)
			{
				return false;
			}

			inventoryAmmoSelect.AmmoCategory = rangedWeaponItem.AmmoType;

			return base.TryOpen();
		}

		public override void OnGuiOpened()
		{
			ComposeDialog();
		}

		private void ComposeDialog()
		{
			ItemSlot activeHotbarSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
			BullseyeItemRangedWeapon rangedWeaponItem = (activeHotbarSlot?.Itemstack?.Collectible) as BullseyeItemRangedWeapon;

			ClearComposers();

			List<ItemStack> ammoStacks = rangedWeaponItem.GetAvailableAmmoTypes(activeHotbarSlot, capi.World.Player);

			if (ammoStacks == null)
			{
				return;
			}

			inventoryAmmoSelect.SetAmmoStacks(ammoStacks);
			inventoryAmmoSelect.SetSelectedAmmoItemStack(rangedWeaponItem.GetEntitySelectedAmmoType(capi.World.Player.Entity));
			inventoryAmmoSelect.PlayerEntity = capi.World.Player.Entity;

			int maxItemsPerLine = 8;

			int widestLineItems = GameMath.Min(ammoStacks.Count, maxItemsPerLine);

			double unscaledSlotPaddedSize = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
			double lineWidth = widestLineItems * unscaledSlotPaddedSize;
			int lineCount = 1 + (ammoStacks.Count - (ammoStacks.Count % maxItemsPerLine)) / maxItemsPerLine;

			ElementBounds elementBounds = ElementBounds.Fixed(0.0, 0.0, lineWidth, lineCount * unscaledSlotPaddedSize);
			ElementBounds bounds = ElementBounds.Fixed(0.0, lineCount * (unscaledSlotPaddedSize + 2.0) + 5.0, lineWidth, 25.0);
			SingleComposer = capi.Gui.CreateCompo("ammotypeselect", ElementStdBounds.AutosizedMainDialog).AddShadedDialogBG(ElementStdBounds.DialogBackground().WithFixedPadding(GuiStyle.ElementToDialogPadding / 2.0), withTitleBar: false).BeginChildElements();

			SingleComposer.AddItemSlotGrid(inventoryAmmoSelect, null, 8, elementBounds, "inventoryAmmoSelectGrid");
			SingleComposer.Compose();
		}

		public override void Dispose()
		{
			capi = null;
		}
	}
}
