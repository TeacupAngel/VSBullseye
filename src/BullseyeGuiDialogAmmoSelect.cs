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

		public override bool TryOpen()
		{
			ItemSlot activeHotbarSlot = capi.World.Player?.InventoryManager?.ActiveHotbarSlot;
			BullseyeCollectibleBehaviorRangedWeapon behaviorRangedWeapon = activeHotbarSlot?.Itemstack?.Collectible.GetCollectibleBehavior<BullseyeCollectibleBehaviorRangedWeapon>(true);

			if (behaviorRangedWeapon?.AmmoType == null)
			{
				return false;
			}

			List<ItemStack> ammoStacks = behaviorRangedWeapon.GetAvailableAmmoTypes(activeHotbarSlot, capi.World.Player);

			if (ammoStacks == null || ammoStacks.Count == 0)
			{
				return false;
			}

			inventoryAmmoSelect.AmmoCategory = behaviorRangedWeapon.AmmoType;
			inventoryAmmoSelect.SetAmmoStacks(ammoStacks);
			inventoryAmmoSelect.SetSelectedAmmoItemStack(behaviorRangedWeapon.GetEntitySelectedAmmoType(capi.World.Player.Entity));
			inventoryAmmoSelect.PlayerEntity = capi.World.Player.Entity;

			return base.TryOpen();
		}

		public override void OnGuiOpened()
		{
			ComposeDialog();
		}

		private void ComposeDialog()
		{
			ClearComposers();

			int ammoStackCount = inventoryAmmoSelect.Count;

			int maxItemsPerLine = 8;
			int widestLineItems = GameMath.Min(ammoStackCount, maxItemsPerLine);

			double unscaledSlotPaddedSize = GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding;
			double lineWidth = widestLineItems * unscaledSlotPaddedSize;
			int lineCount = 1 + (ammoStackCount - (ammoStackCount % maxItemsPerLine)) / maxItemsPerLine;

			ElementBounds elementBounds = ElementBounds.Fixed(0.0, 0.0, lineWidth, lineCount * unscaledSlotPaddedSize);
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
