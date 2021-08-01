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

[assembly: ModInfo( "Archery",
	Description = "",
	Website     = "",
	Authors     = new []{ "rahjital" } )]

namespace Archery
{
    public class ArcheryCore : ModSystem
    {
        ClassRegistry classRegistry;

        public static ArcheryCore clientInstance;
        public static ArcheryCore serverInstance;

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class FreeAimPlayerAimData
        {
            public double aimX;
            public double aimY;
            public double aimZ;
        }
        
        public override void Start(ICoreAPI api)
        {
            classRegistry = Traverse.Create(api.ClassRegistry).Field<ClassRegistry>("registry").Value;

            RegisterItems();
            RegisterEntityBehaviors();
        }

        private EntityProjectile currentArrow;
        private EntityPos arrowLaunchPos;
        private EntityPlayer arrowPlayer;
        private long arrowStartTime;

        public void SetFollowArrow(EntityProjectile arrow, EntityPlayer entityPlayer)
        {
            if (followArrowTickListenerId >= 0)
            {
                if (currentArrow != null && arrowPlayer != null)
                {
                    (arrowPlayer.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, "Tracking new arrow (cannot track two arros at once!)", EnumChatType.Notification);
                }

                currentArrow = arrow;
                arrowPlayer = entityPlayer;
                arrowLaunchPos = arrow.ServerPos.Copy();
                arrowStartTime = arrow.World.ElapsedMilliseconds; 
            }
        }

        private void FollowArrow(float dt)
        {
            if (currentArrow != null) 
            {
                if ((currentArrow.ServerPos.Motion.X == 0 && currentArrow.ServerPos.Motion.Y == 0 && currentArrow.ServerPos.Motion.Z == 0)
                    || !currentArrow.Alive)
                {
                    double arrowDistance = arrowLaunchPos.DistanceTo(currentArrow.ServerPos.XYZ);
                    float arrowFlightTime = (currentArrow.World.ElapsedMilliseconds - arrowStartTime) / 1000f;

                    (arrowPlayer.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, String.Format("Arrow landed at distance {0:0.##}, flight time: {1}", arrowDistance, arrowFlightTime), EnumChatType.Notification);

                    currentArrow = null;
                }
            }
        }

        // Serverside
        public static Dictionary<long, Vec3d> aimVectors;

        long followArrowTickListenerId = -1;

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            serverInstance = this;

            sapi.Network.RegisterChannel("freeaim")
            .RegisterMessageType<FreeAimPlayerAimData>()
            .SetMessageHandler<FreeAimPlayerAimData>(ReceivePlayerAimData);

            aimVectors = new Dictionary<long, Vec3d>();

            sapi.RegisterCommand("trackarrows", "", "", (IServerPlayer player, int groupId, CmdArgs args) => {
                if (followArrowTickListenerId == -1)
                {
                    currentArrow = null;
                    followArrowTickListenerId = sapi.Event.RegisterGameTickListener(FollowArrow, 100);
                    player.SendMessage(groupId, "Now tracking arrows", EnumChatType.Notification);
                }
                else
                {
                    sapi.Event.UnregisterGameTickListener(followArrowTickListenerId);
                    followArrowTickListenerId = -1;
                    player.SendMessage(groupId, "No longer tracking arrows", EnumChatType.Notification);
                }
            });
        }

        private void ReceivePlayerAimData(IPlayer fromPlayer, FreeAimPlayerAimData aimData)
        {
            Vec3d aimVector;

            if (!aimVectors.TryGetValue(fromPlayer.Entity.EntityId, out aimVector))
            {
                aimVectors[fromPlayer.Entity.EntityId] = new Vec3d(aimData.aimX, aimData.aimY, aimData.aimZ);
            }
            else
            {
                aimVector.X = aimData.aimX;
                aimVector.Y = aimData.aimY;
                aimVector.Z = aimData.aimZ;
            }
        }

        // Clientside
        public static bool aiming = false;

        public static float aimX;
        public static float aimY;

        public static float aimOffsetX;
        public static float aimOffsetY;

        public static Vec3d targetVec;

        private ClientMain client;

        private Unproject unproject;
        private double[] viewport;
        private double[] rayStart;
        private double[] rayEnd;

        StackMatrix4 mvMatrix;
        StackMatrix4 pMatrix;

        IClientNetworkChannel cNetworkChannel;

        public override void StartClientSide(ICoreClientAPI capi)
        {
            clientInstance = this;

            Harmony harmony = new Harmony("vs.freeaim");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            client = capi.World as ClientMain;
            unproject = new Unproject();
            viewport = new double[4];
            rayStart = new double[4];
            rayEnd = new double[4];
            
            targetVec = new Vec3d();

            mvMatrix = Traverse.Create(client).Field<StackMatrix4>("MvMatrix").Value;
            pMatrix = Traverse.Create(client).Field<StackMatrix4>("PMatrix").Value;

            cNetworkChannel = capi.Network.RegisterChannel("freeaim")
            .RegisterMessageType<FreeAimPlayerAimData>();

            capi.Event.RegisterGameTickListener(SendAimToServer, 100);
        }

        public static Vec2f GetCurrentAim()
        {
            return new Vec2f(aimX + aimOffsetX, aimY + aimOffsetY);
        }

        public void SetAim()
		{
            Vec2f currentAim = GetCurrentAim();

			int mouseCurrentX = (int)currentAim.X + client.Width / 2;
			int mouseCurrentY = (int)currentAim.Y + client.Height / 2;
			viewport[0] = 0.0;
			viewport[1] = 0.0;
			viewport[2] = client.Width;
			viewport[3] = client.Height;
			unproject.UnProject(mouseCurrentX, client.Height - mouseCurrentY, 1, mvMatrix.Top, pMatrix.Top, viewport, rayEnd);
			unproject.UnProject(mouseCurrentX, client.Height - mouseCurrentY, 0, mvMatrix.Top, pMatrix.Top, viewport, rayStart);
			double offsetX = rayEnd[0] - rayStart[0];
			double offsetY = rayEnd[1] - rayStart[1];
			double offsetZ = rayEnd[2] - rayStart[2];
			float length = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ);
			offsetX /= length;
			offsetY /= length;
			offsetZ /= length;

            targetVec.X = offsetX;
            targetVec.Y = offsetY;
            targetVec.Z = offsetZ;
		}

        private void SendAimToServer(float dt)
        {
            if (aiming)
            {
                cNetworkChannel.SendPacket(new FreeAimPlayerAimData()
                {
                    aimX = targetVec.X,
                    aimY = targetVec.Y,
                    aimZ = targetVec.Z,
                });
            }
        }

        private void RegisterItems()
        {
            //api.RegisterItemClass("archery.ItemBow", typeof(ArcheryItemBow));
            //api.RegisterItemClass("archery.ItemSpear", typeof(ArcheryItemSpear));

            classRegistry.ItemClassToTypeMapping["ItemBow"] = typeof(FreeAim.ItemBow);
        }

        private void RegisterEntityBehaviors()
        {
            classRegistry.entityBehaviorClassNameToTypeMapping["aimingaccuracy"] = typeof(Archery.EntityBehaviorAimingAccuracy);
            classRegistry.entityBehaviorTypeToClassNameMapping[typeof(Archery.EntityBehaviorAimingAccuracy)] = "aimingaccuracy";

            //api.RegisterEntityBehaviorClass("aimingaccuracy", typeof(EntityBehaviorAimingAccuracy));
            //api.RegisterEntityBehaviorClass("archery.aimingaccuracy", typeof(ArcheryEntityBehaviorAimingAccuracy));
        }
    }
}