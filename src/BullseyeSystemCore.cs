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

		public override void StartPre(ICoreAPI api)
		{
			harmony = new Harmony(harmonyId);
		}

		public override void Start(ICoreAPI api)
		{
			ClassRegistry classRegistry = Traverse.Create(api.ClassRegistry).Field<ClassRegistry>("registry").Value;

			RegisterItems(classRegistry);
			RegisterEntityBehaviors(classRegistry);
		}

		public override void StartServerSide(ICoreServerAPI sapi)
		{
			HarmonyPatches.PatchManager.PatchServerside(harmony, sapi);
		}

		public override void StartClientSide(ICoreClientAPI capi)
		{
			HarmonyPatches.PatchManager.PatchClientside(harmony, capi);

			capi.Input.RegisterHotKey("bullseye.ammotypeselect", Lang.Get("bullseye:select-ammo"), GlKeys.F, HotkeyType.GUIOrOtherControls);

			capi.Gui.RegisterDialog(new BullseyeGuiDialogAmmoSelect(capi));
		}

		private void RegisterItems(ClassRegistry classRegistry)
		{
			classRegistry.ItemClassToTypeMapping["ItemBow"] = typeof(Bullseye.BullseyeItemBow);
			classRegistry.ItemClassToTypeMapping["ItemSpear"] = typeof(Bullseye.BullseyeItemSpear);
			classRegistry.ItemClassToTypeMapping["ItemSling"] = typeof(Bullseye.BullseyeItemSling);
		}

		private void RegisterEntityBehaviors(ClassRegistry classRegistry)
		{
			classRegistry.RegisterentityBehavior("bullseye.aimingaccuracy", typeof(Bullseye.BullseyeEntityBehaviorAimingAccuracy));
		}

		public override void Dispose()
		{
			harmony?.UnpatchAll(harmonyId);
			harmony = null;

			HarmonyPatches.PatchManager.Dispose();
		}
	}
}