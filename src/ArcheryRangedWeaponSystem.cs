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

        IWorldAccessor world;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            world = api.World;

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

            world = api.World;

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
        Dictionary<long, long> cooldownByEntityID = new Dictionary<long, long>();

        public void StartEntityCooldown(long entityID)
        {
            cooldownByEntityID[entityID] = world.ElapsedMilliseconds;
        }

        public float GetEntityCooldownTime(long entityID)
        {
            return cooldownByEntityID.ContainsKey(entityID) ? (world.ElapsedMilliseconds - cooldownByEntityID[entityID]) / 1000f : 0;
        }

        public bool HasEntityCooldownPassed(long entityID, double cooldownTime)
        {
            return cooldownByEntityID.ContainsKey(entityID) ? world.ElapsedMilliseconds > cooldownByEntityID[entityID] + (cooldownTime * 1000) : true;
        }
    }
}