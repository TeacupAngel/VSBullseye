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
    public class BullseyeRangedWeaponSystem : ModSystem
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class BullseyeRangedWeaponFirePacket
        {
            public int itemId;
            public long entityId;
            public double aimX;
            public double aimY;
            public double aimZ;
        }

        // Server
        ICoreServerAPI sapi;
        IServerNetworkChannel serverNetworkChannel;

        Dictionary<long, ItemSlot> lastRangedSlotByEntityId;
        Dictionary<long, long> rangedChargeStartByEntityId;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            world = api.World;

            lastRangedSlotByEntityId = new Dictionary<long, ItemSlot>();
            rangedChargeStartByEntityId = new Dictionary<long, long>();

            serverNetworkChannel = api.Network.RegisterChannel("bullseyeitem")
            .RegisterMessageType<BullseyeRangedWeaponFirePacket>()
            .SetMessageHandler<BullseyeRangedWeaponFirePacket>(OnServerRangedWeaponFire);
        }

        public void OnServerRangedWeaponFire(IServerPlayer fromPlayer, BullseyeRangedWeaponFirePacket packet)
        {
            TreeAttribute tree = new TreeAttribute();
            tree.SetLong("entityId", packet.entityId);
            tree.SetInt("itemId", packet.itemId);
            tree.SetDouble("aimX", packet.aimX);
            tree.SetDouble("aimY", packet.aimY);
            tree.SetDouble("aimZ", packet.aimZ);

            sapi.Event.PushEvent("bullseyeRangedWeaponFire", tree);
        }

        public void SetLastEntityRangedChargeData(long entityId, ItemSlot itemSlot)
        {
            lastRangedSlotByEntityId[entityId] = itemSlot;
            rangedChargeStartByEntityId[entityId] = sapi.World.ElapsedMilliseconds;
        }

        public ItemSlot GetLastEntityRangedItemSlot(long entityId)
        {
            return lastRangedSlotByEntityId.ContainsKey(entityId) ? lastRangedSlotByEntityId[entityId] : null;
        }

        public float GetEntityChargeStart(long entityId)
        {
            return rangedChargeStartByEntityId.ContainsKey(entityId) ? rangedChargeStartByEntityId[entityId] / 1000f : 0;
        }

        // Client
        IClientNetworkChannel clientNetworkChannel;

        public override void StartClientSide(ICoreClientAPI api)
        {
            world = api.World;

            clientNetworkChannel = api.Network.RegisterChannel("bullseyeitem")
            .RegisterMessageType<BullseyeRangedWeaponFirePacket>();
        }

        public void SendRangedWeaponFirePacket(long entityId, int itemId, Vec3d targetVec)
        {
            clientNetworkChannel.SendPacket(new BullseyeRangedWeaponFirePacket()
            {
                entityId = entityId,
                itemId = itemId,
                aimX = targetVec.X,
                aimY = targetVec.Y,
                aimZ = targetVec.Z
            });
        }

        // Common
        IWorldAccessor world;

        Dictionary<long, long> cooldownByEntityId = new Dictionary<long, long>();

        public void StartEntityCooldown(long entityId)
        {
            cooldownByEntityId[entityId] = world.ElapsedMilliseconds;
        }

        public float GetEntityCooldownTime(long entityId)
        {
            return cooldownByEntityId.ContainsKey(entityId) ? (world.ElapsedMilliseconds - cooldownByEntityId[entityId]) / 1000f : 0;
        }

        public bool HasEntityCooldownPassed(long entityId, double cooldownTime)
        {
            return cooldownByEntityId.ContainsKey(entityId) ? world.ElapsedMilliseconds > cooldownByEntityId[entityId] + (cooldownTime * 1000) : true;
        }
        
        public override void Dispose()
        {
            sapi = null;
            serverNetworkChannel = null;

            clientNetworkChannel = null;

            world = null;
        }
    }
}