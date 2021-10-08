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
using Vintagestory.API;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using ProtoBuf;
using System.Reflection;

using HarmonyLib;

using Cairo;

namespace Bullseye
{
    public class BullseyeConfigSystem : ModSystem
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BullseyeServerConfigPacket
        {
            public string data;
        }

        // Common
        public BullseyeServerConfig serverConfig;

        public override double ExecuteOrder()
        {
            return 0.05;
        }

        // Server
        ICoreServerAPI sapi;
        IServerNetworkChannel serverNetworkChannel;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            serverNetworkChannel = api.Network.RegisterChannel("bullseyeserverconfig")
            .RegisterMessageType<BullseyeServerConfigPacket>();

            api.Event.PlayerJoin += OnPlayerJoin;

            try
            {
                serverConfig = api.LoadModConfig<BullseyeServerConfig>("BullseyeConfig.json");

                if (serverConfig == null)
                {
                    serverConfig = new BullseyeServerConfig();
                    api.StoreModConfig<BullseyeServerConfig>(serverConfig, "BullseyeConfig.json");
                }
            }
            catch
            {
                serverConfig = new BullseyeServerConfig();
                sapi.SendMessageToGroup(GlobalConstants.ServerInfoChatGroup, "Bullseye: failed to load server config file BullseyeConfig.json! Please check for typos or anything else that could make it fail", EnumChatType.Notification);
            }
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            SendServerConfigToPlayer(player);
        }

        public void OnServerConfigChanged()
        {
            if (sapi != null)
            {
                foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
                {
                    SendServerConfigToPlayer(player);
                }

                sapi.StoreModConfig<BullseyeServerConfig>(serverConfig, "BullseyeConfig.json");
            }
        }

        private void SendServerConfigToPlayer(IServerPlayer player)
        {
            BullseyeServerConfigPacket packet = new BullseyeServerConfigPacket() {data = JsonUtil.ToString<BullseyeServerConfig>(serverConfig)};

            serverNetworkChannel.SendPacket(packet, player);
        }

        // Client
        ICoreClientAPI capi;
        IClientNetworkChannel clientNetworkChannel;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            clientNetworkChannel = api.Network.RegisterChannel("bullseyeserverconfig")
            .RegisterMessageType<BullseyeServerConfigPacket>()
            .SetMessageHandler<BullseyeServerConfigPacket>(OnServerConfigPacket);
        }

        private void OnServerConfigPacket(BullseyeServerConfigPacket packet)
        {
            serverConfig = JsonUtil.FromString<BullseyeServerConfig>(packet.data);
        }

        // Common        
        public override void Dispose()
        {
            sapi = null;
            serverNetworkChannel = null;

            capi = null;
            clientNetworkChannel = null;
        }
    }
}