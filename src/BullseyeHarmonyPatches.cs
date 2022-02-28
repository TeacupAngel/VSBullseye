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
			public static void PatchClientside(Harmony harmony)
			{
				SystemRenderAimClientPatch.Patch(harmony);
				ClientMainPatch.Patch(harmony);
			}

			public static void PatchCommon(Harmony harmony)
			{
				ItemStonePatch.Patch(harmony);
				ItemArrowPatch.Patch(harmony);
			}
		}

		public class SystemRenderAimClientPatch
		{
			public static BullseyeClientAimingSystem clientAimingSystem;

			public static void Patch(Harmony harmony)
			{
				harmony.Patch(typeof(SystemRenderAim).GetMethod("DrawAim", BindingFlags.Instance | BindingFlags.NonPublic), 
					prefix: new HarmonyMethod(typeof(SystemRenderAimClientPatch).GetMethod("DrawAimPrefix", BindingFlags.Static | BindingFlags.NonPublic) 
				));
			}

			static bool DrawAimPrefix()
			{
				return !(clientAimingSystem?.Aiming ?? false);
			}
		}

		public class ClientMainPatch
		{
			public static BullseyeClientAimingSystem clientAimingSystem;

			public static void Patch(Harmony harmony)
			{
				harmony.Patch(typeof(ClientMain).GetMethod("UpdateCameraYawPitch", BindingFlags.Instance | BindingFlags.NonPublic), 
					prefix: new HarmonyMethod(typeof(ClientMainPatch).GetMethod("UpdateCameraYawPitchPrefix", BindingFlags.Static | BindingFlags.NonPublic) 
				));
			}

			static bool UpdateCameraYawPitchPrefix(ClientMain __instance, 
				ref double ___MouseDeltaX, ref double ___MouseDeltaY, 
				ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY,
				float dt)
			{
				if (clientAimingSystem == null)
				{
					return true;
				}

				clientAimingSystem.UpdateAimPoint(__instance, ref ___MouseDeltaX, ref ___MouseDeltaY, ref ___DelayedMouseDeltaX, ref ___DelayedMouseDeltaY, dt);
				
				return true;
			}
		}

		public class ItemStonePatch
		{
			public static void Patch(Harmony harmony)
			{
				harmony.Patch(typeof(ItemStone).GetMethod("GetHeldItemInfo"),
					postfix: new HarmonyMethod(typeof(ItemStonePatch).GetMethod("GetHeldItemInfoPostfix", BindingFlags.Static | BindingFlags.NonPublic) 
				));
			}

			static void GetHeldItemInfoPostfix(ItemSlot inSlot, StringBuilder dsc)
			{
				float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            	if (dmg != 0) dsc.AppendLine(Lang.Get("bullseye:damage-with-sling", dmg));
			}
		}
	}

	public class ItemArrowPatch
	{
		private static void BaseGetHeldItemInfo(object instance, ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			throw new NotImplementedException("Called unpatched BaseGetHeldItemInfo stub in ItemArrowPatch!");
		}

		public static void Patch(Harmony harmony)
		{
			MethodInfo test1 = typeof(CollectibleObject).GetMethod("GetHeldItemInfo");
			HarmonyMethod test2 = new HarmonyMethod(typeof(ItemArrowPatch).GetMethod("BaseGetHeldItemInfo", BindingFlags.Static | BindingFlags.NonPublic));

			ReversePatcher reversePatcher = harmony.CreateReversePatcher(typeof(CollectibleObject).GetMethod("GetHeldItemInfo"), 
				new HarmonyMethod(typeof(ItemArrowPatch).GetMethod("BaseGetHeldItemInfo", BindingFlags.Static | BindingFlags.NonPublic)));
			reversePatcher.Patch(HarmonyReversePatchType.Snapshot);

			harmony.Patch(typeof(ItemArrow).GetMethod("GetHeldItemInfo"),
				prefix: new HarmonyMethod(typeof(ItemArrowPatch).GetMethod("GetHeldItemInfoPrefix", BindingFlags.Static | BindingFlags.NonPublic) 
			));
		}

		private static bool GetHeldItemInfoPrefix(ItemArrow __instance, ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			BaseGetHeldItemInfo(__instance, inSlot, dsc, world, withDebugInfo);

			float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
			if (dmg != 0) dsc.AppendLine(dmg + Lang.Get("game:piercing-damage"));

			if (inSlot.Itemstack.Collectible.Attributes.KeyExists("averageLifetimeDamage"))
			{
				dsc.AppendLine(Lang.Get("bullseye:lifetime-projectile-damage", inSlot.Itemstack.ItemAttributes["averageLifetimeDamage"].AsFloat()));
			}
			else
			{
				float breakChance = inSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0);

				if (breakChance != 0) dsc.AppendLine(Lang.Get("game:breakchanceonimpact", breakChance));
			}

			return false;
		}
	}
}