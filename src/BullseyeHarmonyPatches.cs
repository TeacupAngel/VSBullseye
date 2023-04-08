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
			}

			public static void Dispose()
			{
				SystemRenderAimClientPatch.Dispose();
				ClientMainPatch.Dispose();
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
	}
}