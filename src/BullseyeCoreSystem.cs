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
    public class BullseyeCoreSystem : ModSystem
    {
		private Harmony harmony;
		private readonly string harmonyId = "vs.bullseye";

		public override void StartPre(ICoreAPI api)
		{
            harmony = new Harmony(harmonyId);
		}

        public override void Start(ICoreAPI api)
        {
            ClassRegistry classRegistry = Traverse.Create(api.ClassRegistry).Field<ClassRegistry>("registry").Value;

            RegisterItems(classRegistry);
			RegisterCollectibleBehaviors(classRegistry);
            RegisterEntityBehaviors(classRegistry);
        }

		public override void StartServerSide(ICoreServerAPI aapi)
        {
			HarmonyPatches.PatchManager.PatchCommon(harmony);
        }

		public override void StartClientSide(ICoreClientAPI capi)
        {
            HarmonyPatches.PatchManager.PatchClientside(harmony);

			if (!capi.IsSinglePlayer)
			{
				HarmonyPatches.PatchManager.PatchCommon(harmony);
			}

            capi.Input.RegisterHotKey("bullseye.ammotypeselect", Lang.Get("bullseye:select-ammo"), GlKeys.F, HotkeyType.GUIOrOtherControls);

			capi.Gui.RegisterDialog(new GuiDialogAmmoSelect(capi));
        }

        private void RegisterItems(ClassRegistry classRegistry)
        {
            classRegistry.ItemClassToTypeMapping["ItemBow"] = typeof(Bullseye.ItemBow);
            classRegistry.ItemClassToTypeMapping["ItemSpear"] = typeof(Bullseye.ItemSpear);
			classRegistry.ItemClassToTypeMapping["ItemSling"] = typeof(Bullseye.ItemSling);
        }

		private void RegisterCollectibleBehaviors(ClassRegistry classRegistry)
        {
            classRegistry.RegisterCollectibleBehaviorClass("bullseye.StoneDescription", typeof(CollectibleBehaviorStoneDescription));
        }

        private void RegisterEntityBehaviors(ClassRegistry classRegistry)
        {
            classRegistry.RegisterentityBehavior("bullseye.aimingaccuracy", typeof(Bullseye.EntityBehaviorAimingAccuracy));
        }

		public override void Dispose()
        {
            harmony?.UnpatchAll(harmonyId);
            harmony = null;
        }
    }
}