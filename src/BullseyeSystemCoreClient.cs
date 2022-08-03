using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using ProtoBuf;

namespace Bullseye
{
	public class BullseyeSystemCoreClient : ModSystem
	{
		private BullseyeSystemConfig configSystem;

		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return forSide == EnumAppSide.Client;
		}

		public override void StartClientSide(ICoreClientAPI capi)
		{
			capi.RegisterCommand("bullseye", "", "", (int groupId, CmdArgs args) => {
				if (args.Length > 0)
				{
					string cmd = args.PopWord();

					switch (cmd)
					{
						case "set":
							configSystem.CommandSet(capi, groupId, args);
							return;
						case "get":
							configSystem.CommandGet(capi, groupId, args);
							return;
					}
				}

				capi.ShowChatMessage(".bullseye [set|get]");
			});

			configSystem = capi.ModLoader.GetModSystem<BullseyeSystemConfig>();
		}

		public override void Dispose()
		{
			configSystem = null;
		}
	}
}