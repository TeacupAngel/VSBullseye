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

namespace Archery
{
    public class ArcheryRangedWeaponSystem : ModSystem
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class ArcheryRangedWeaponFired
        {
            public int itemId;
            public long entityId;
        }

        // Server
        ICoreServerAPI sapi;
        IServerNetworkChannel serverNetworkChannel;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            serverNetworkChannel = api.Network.RegisterChannel("archeryitem")
            .RegisterMessageType<ArcheryRangedWeaponFired>();
        }

        public void SendRangedWeaponFiredPacket(long entityId, int itemId)
        {
            IServerPlayer[] serverPlayer = Array.ConvertAll<IPlayer, IServerPlayer>(sapi.World.AllOnlinePlayers, player => (IServerPlayer)player);

            serverNetworkChannel.SendPacket(new ArcheryRangedWeaponFired()
            {
                entityId = entityId,
                itemId = itemId,
            }, serverPlayer);
        }

        // Client
        ICoreClientAPI capi;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            api.Network.RegisterChannel("archeryitem")
            .RegisterMessageType<ArcheryRangedWeaponFired>()
            .SetMessageHandler<ArcheryRangedWeaponFired>(OnClientRangedWeaponFired);
        }

        public void OnClientRangedWeaponFired(ArcheryRangedWeaponFired packet)
        {
            TreeAttribute tree = new TreeAttribute();
            tree.SetLong("entityId", packet.entityId);
            tree.SetInt("itemId", packet.itemId);

            capi.Event.PushEvent("archeryRangedWeaponFired", tree);
        }

        // Common
        Dictionary<string, double> cooldownByPlayerUID = new Dictionary<string, double>();

        private double currentTime;

        public override void Start(ICoreAPI api)
        {
            api.World.RegisterGameTickListener(OnGameTick, 50);
        }

        protected void OnGameTick(float dt)
        {
            currentTime += dt;
        }

        public void SetPlayerCooldown(string playerUID)
        {
            cooldownByPlayerUID[playerUID] = currentTime;
        }

        public double GetPlayerCooldown(string playerUID)
        {
            return cooldownByPlayerUID.ContainsKey(playerUID) ? cooldownByPlayerUID[playerUID] : -double.MinValue;
        }

        public bool HasPlayerCooldownPassed(string playerUID, double cooldownTime)
        {
            return cooldownByPlayerUID.ContainsKey(playerUID) ? currentTime > cooldownByPlayerUID[playerUID] + cooldownTime : true;
        }
    }
}