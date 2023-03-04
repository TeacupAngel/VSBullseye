using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Vintagestory.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using ProtoBuf;
using Vintagestory.GameContent;

using HarmonyLib;

using Cairo;

namespace Bullseye
{
	namespace HarmonyPatches
	{
		public class PatchManager
		{
			public static void PatchServerside(Harmony harmony, ICoreServerAPI sapi)
			{
				HarmonyPatches.PatchManager.PatchCommon(harmony, sapi);
			}

			public static void PatchClientside(Harmony harmony, ICoreClientAPI capi)
			{
				SystemRenderAimClientPatch.Patch(harmony, capi);
				ClientMainPatch.Patch(harmony, capi);

				if (!capi.IsSinglePlayer)
				{
					HarmonyPatches.PatchManager.PatchCommon(harmony, capi);
				}
			}

			private static void PatchCommon(Harmony harmony, ICoreAPI api)
			{
				//ItemStonePatch.Patch(harmony, api);
				//ItemArrowPatch.Patch(harmony, api); // Disabled as a test fix for 2.4.0-rc.4
			}

			public static void Dispose()
			{
				SystemRenderAimClientPatch.Dispose();
				ClientMainPatch.Dispose();

				//ItemStonePatch.Dispose();
				//ItemArrowPatch.Dispose();
			}
		}

		// CLIENTSIDE PATCHES
		public class SystemRenderAimClientPatch
		{
			private static BullseyeSystemClientAiming clientAimingSystem;

			public static void Patch(Harmony harmony, ICoreClientAPI capi)
			{
				harmony.Patch(typeof(SystemRenderAim).GetMethod("DrawAim", BindingFlags.Instance | BindingFlags.NonPublic), 
					prefix: new HarmonyMethod(typeof(SystemRenderAimClientPatch).GetMethod("DrawAimPrefix", BindingFlags.Static | BindingFlags.NonPublic) 
				));

				clientAimingSystem = capi.ModLoader.GetModSystem<BullseyeSystemClientAiming>();
			}

			static bool DrawAimPrefix()
			{
				return !(clientAimingSystem?.Aiming ?? false);
			}

			public static void Dispose()
			{
				clientAimingSystem = null;
			}
		}

		public class ClientMainPatch
		{
			private static BullseyeSystemClientAiming clientAimingSystem;

			public static void Patch(Harmony harmony, ICoreClientAPI capi)
			{
				harmony.Patch(typeof(ClientMain).GetMethod("UpdateCameraYawPitch", BindingFlags.Instance | BindingFlags.NonPublic), 
					prefix: new HarmonyMethod(typeof(ClientMainPatch).GetMethod("UpdateCameraYawPitchPrefix", BindingFlags.Static | BindingFlags.NonPublic) 
				));

				clientAimingSystem = capi.ModLoader.GetModSystem<BullseyeSystemClientAiming>();
			}

			static bool UpdateCameraYawPitchPrefix(ClientMain __instance, 
				ref double ___MouseDeltaX, ref double ___MouseDeltaY, 
				ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY,
				float dt)
			{
				clientAimingSystem?.UpdateAimPoint(__instance, ref ___MouseDeltaX, ref ___MouseDeltaY, ref ___DelayedMouseDeltaX, ref ___DelayedMouseDeltaY, dt);
				
				return true;
			}

			public static void Dispose()
			{
				clientAimingSystem = null;
			}
		}

		// COMMON PATCHES
		// Disabled in 2.5.0 with the transition to CollectibleBehaviors
		/*public class ItemStonePatch
		{
			private static BullseyeSystemConfig configSystem;

			public static void Patch(Harmony harmony, ICoreAPI api)
			{
				harmony.Patch(typeof(ItemStone).GetMethod("GetHeldItemInfo"),
					postfix: new HarmonyMethod(typeof(ItemStonePatch).GetMethod("GetHeldItemInfoPostfix", BindingFlags.Static | BindingFlags.NonPublic) 
				));

				configSystem = api.ModLoader.GetModSystem<BullseyeSystemConfig>();
			}

			static void GetHeldItemInfoPostfix(ItemSlot inSlot, StringBuilder dsc)
			{
				float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0) * configSystem?.GetSyncedConfig().SlingDamage ?? 1f;
				if (dmg != 0) dsc.AppendLine(Lang.Get("bullseye:damage-with-sling", dmg));
			}

			public static void Dispose()
			{
				configSystem = null;
			}
		}*/
	}

	/*public class ItemArrowPatch
	{
		private static BullseyeSystemConfig configSystem;

		// 2.4.0-rc.4 - could this potentially cause Harmony patches to crash, somehow? Commented this whole thing out, just in case
		private static void BaseGetHeldItemInfo(object instance, ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			throw new NotImplementedException("Called unpatched BaseGetHeldItemInfo stub in ItemArrowPatch!");
		}

		public static void Patch(Harmony harmony, ICoreAPI api)
		{
			ReversePatcher reversePatcher = harmony.CreateReversePatcher(typeof(CollectibleObject).GetMethod("GetHeldItemInfo"), 
				new HarmonyMethod(typeof(ItemArrowPatch).GetMethod("BaseGetHeldItemInfo", BindingFlags.Static | BindingFlags.NonPublic)));
			
			//reversePatcher.Patch(HarmonyReversePatchType.Snapshot); // Crashes since 1.17, probably because new Harmony version :(
			reversePatcher.Patch(HarmonyReversePatchType.Original);

			harmony.Patch(typeof(ItemArrow).GetMethod("GetHeldItemInfo"),
				prefix: new HarmonyMethod(typeof(ItemArrowPatch).GetMethod("GetHeldItemInfoPrefix", BindingFlags.Static | BindingFlags.NonPublic) 
			));

			configSystem = api.ModLoader.GetModSystem<BullseyeSystemConfig>();
		}

		private static bool GetHeldItemInfoPrefix(ItemArrow __instance, ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			BaseGetHeldItemInfo(__instance, inSlot, dsc, world, withDebugInfo);

			float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0) * configSystem?.GetSyncedConfig().ArrowDamage ?? 1f;
			if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("game:piercing-damage"));

			if (inSlot.Itemstack.Collectible.Attributes.KeyExists("averageLifetimeDamage"))
			{
				dsc.AppendLine(Lang.Get("bullseye:lifetime-projectile-damage", inSlot.Itemstack.ItemAttributes["averageLifetimeDamage"].AsFloat()));
			}
			else
			{
				float breakChance = inSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0);

				if (breakChance != 0) dsc.AppendLine(Lang.Get("game:breakchanceonimpact", (int)(breakChance * 100)));
			}

			return false;
		}

		public static void Dispose()
		{
			configSystem = null;
		}
	}*/
}