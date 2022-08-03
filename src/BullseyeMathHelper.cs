using System;
using System.Collections.Generic;
using Vintagestory.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using ProtoBuf;

using HarmonyLib;

using Cairo;

namespace Bullseye
{
	public static class BullseyeMathHelper
	{
		public static Vec3d Vec3GetPerpendicular(Vec3d original)
		{
			int vecHighest = Math.Abs(original.X) > Math.Abs(original.Y) ? 0 : 1;
			vecHighest = Math.Abs(original[vecHighest]) > Math.Abs(original.Z) ? vecHighest : 2;

			int vecLowest = Math.Abs(original.X) < Math.Abs(original.Y) ? 0 : 1;
			vecLowest = Math.Abs(original[vecLowest]) < Math.Abs(original.Z) ? vecLowest : 2;

			int vecMiddle = Math.Abs(original.X) < Math.Abs(original[vecHighest]) ? 0 : -1;
			vecMiddle = vecMiddle < 0 
						|| (Math.Abs(original.Y) > Math.Abs(original[vecMiddle]) && Math.Abs(original.Y) < Math.Abs(original[vecHighest])) ? 1 : vecMiddle;
			vecMiddle =    (Math.Abs(original.Z) > Math.Abs(original[vecMiddle]) && Math.Abs(original.Z) < Math.Abs(original[vecHighest])) ? 2 : vecMiddle;

			Vec3d perp = new Vec3d();
			perp[vecHighest] = original[vecMiddle];
			perp[vecMiddle] = -original[vecHighest];
			return perp.Normalize();
		}
	}
}