using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using ProtoBuf;

namespace Bullseye
{
	public class BullseyeSystemCoreServer : ModSystem
	{
		private BullseyeSystemConfig configSystem;

		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return forSide == EnumAppSide.Server;
		}

		public override void StartServerSide(ICoreServerAPI sapi)
		{
			sapi.RegisterCommand("bullseye", "", "", (IServerPlayer player, int groupId, CmdArgs args) => {
				if (args.Length > 0)
				{
					string cmd = args.PopWord();

					switch (cmd)
					{
						case "set":
							configSystem.CommandSet(sapi, player, groupId, args);
							return;
						case "get":
							configSystem.CommandGet(sapi, player, groupId, args);
							return;
					}
				}

				player.SendMessage(groupId, "/bullseye [set|get]", EnumChatType.CommandError);
			}, Privilege.controlserver);

			configSystem = sapi.ModLoader.GetModSystem<BullseyeSystemConfig>();
		}

		public override void Dispose()
		{
			configSystem = null;
		}
	}
}