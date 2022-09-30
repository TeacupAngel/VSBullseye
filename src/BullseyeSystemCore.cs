using System;
using System.Collections.Generic;
using Vintagestory.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

using HarmonyLib;

namespace Bullseye
{
	public class BullseyeSystemCore : ModSystem
	{
		private Harmony harmony;
		private readonly string harmonyId = "vs.teacupangel.bullseye";

		private ICoreAPI api;

		public override void StartPre(ICoreAPI api)
		{
			this.api = api;
			api.Logger.Notification("[Bullseye] Starting Harmony instance");

			harmony = new Harmony(harmonyId);
		}

		public override void Start(ICoreAPI api)
		{
			RegisterItems(api);
			RegisterEntityBehaviors(api);
		}

		public override void StartServerSide(ICoreServerAPI sapi)
		{
			api.Logger.Notification("[Bullseye] Applying server-side Harmony patches");
			HarmonyPatches.PatchManager.PatchServerside(harmony, sapi);
		}

		public override void StartClientSide(ICoreClientAPI capi)
		{
			api.Logger.Notification("[Bullseye] Applying client-side Harmony patches");
			HarmonyPatches.PatchManager.PatchClientside(harmony, capi);

			capi.Input.RegisterHotKey("bullseye.ammotypeselect", Lang.Get("bullseye:select-ammo"), GlKeys.F, HotkeyType.GUIOrOtherControls);

			capi.Gui.RegisterDialog(new BullseyeGuiDialogAmmoSelect(capi));
		}

		private void RegisterItems(ICoreAPI api)
		{
			api.RegisterItemClass("bullseye.ItemBow", typeof(Bullseye.BullseyeItemBow));
			api.RegisterItemClass("bullseye.ItemSpear", typeof(Bullseye.BullseyeItemSpear));
			api.RegisterItemClass("bullseye.ItemSling", typeof(Bullseye.BullseyeItemSling));

			api.RegisterItemClass("bullseye.ItemArrow", typeof(Bullseye.BullseyeItemArrow));
			api.RegisterItemClass("bullseye.ItemBullet", typeof(Bullseye.BullseyeItemBullet));
		}

		private void RegisterEntityBehaviors(ICoreAPI api)
		{
			api.RegisterEntityBehaviorClass("bullseye.aimingaccuracy", typeof(Bullseye.BullseyeEntityBehaviorAimingAccuracy));
		}

		public override void Dispose()
		{
			api.Logger.Notification("[Bullseye] Unpatching and disposing of Harmony");

			harmony?.UnpatchAll(harmonyId);
			harmony = null;

			HarmonyPatches.PatchManager.Dispose();
		}
	}
}