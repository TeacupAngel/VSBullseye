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
    public class BullseyeCoreServerSystem : ModSystem
    {
        public BullseyeConfigSystem configSystem;

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
                            CommandSet(sapi, player, groupId, args);
                            return;
                        case "get":
                            CommandGet(sapi, player, groupId, args);
                            return;
                    }
                }

                player.SendMessage(groupId, "/bullseye [set|get]", EnumChatType.CommandError);
            }, Privilege.controlserver);

            configSystem = sapi.ModLoader.GetModSystem<BullseyeConfigSystem>();
        }

		private void CommandSet(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                string setting = args.PopWord();

                switch (setting.ToLowerInvariant())
                {
					case "aimdifficulty":
                    case "globalaccuracy":
                        if (args.Length > 0)
                        {
                            float argument = GameMath.Clamp((float)args.PopFloat(1f), 0f, 10f);
                            configSystem.serverConfig.aimDifficulty = argument;
                            configSystem.OnServerConfigChanged();
                            player.SendMessage(groupId, $"Bullseye: {setting} set to {argument}", EnumChatType.CommandSuccess);
                        }
                        else
                        {
                            player.SendMessage(groupId, $"Bullseye: {setting} requires 1 parameter", EnumChatType.CommandSuccess);
                        }
                        return;
					default:
						player.SendMessage(groupId, $"Bullseye: unknown setting '{setting}'", EnumChatType.CommandSuccess);
						return;
                }
            }
            else
            {
                player.SendMessage(groupId, "/bullseye set [aimDifficulty] [arguments]", EnumChatType.CommandError);
            }
        }

        private void CommandGet(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                string setting = args.PopWord();

                switch (setting.ToLowerInvariant())
                {
					case "aimdifficulty":
                    case "globalaccuracy":
                        player.SendMessage(groupId, $"Bullseye: {setting} is currently {configSystem.serverConfig.aimDifficulty}", EnumChatType.CommandSuccess);
                        return;
					default:
						player.SendMessage(groupId, $"Bullseye: unknown setting '{setting}'", EnumChatType.CommandSuccess);
						return;
                }
            }
            else
            {
                player.SendMessage(groupId, "/bullseye get [aimDifficulty]", EnumChatType.CommandError);
            }
        }

        public override void Dispose()
        {
            configSystem = null;
        }
    }
}