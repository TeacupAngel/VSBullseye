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
    public class BullseyeCore : ModSystem
    {
        public static BullseyeCore clientInstance;
        public static BullseyeCore serverInstance;
        
        public override void Start(ICoreAPI api)
        {
            ClassRegistry classRegistry = Traverse.Create(api.ClassRegistry).Field<ClassRegistry>("registry").Value;
            //ClassRegistry classRegistry = api.ClassRegistry as ClassRegistry;

            RegisterItems(classRegistry);
            RegisterEntityBehaviors(classRegistry);
        }

        private void RegisterItems(ClassRegistry classRegistry)
        {
            classRegistry.ItemClassToTypeMapping["ItemBow"] = typeof(Bullseye.ItemBow);
            classRegistry.ItemClassToTypeMapping["ItemSpear"] = typeof(Bullseye.ItemSpear);
        }

        private void RegisterEntityBehaviors(ClassRegistry classRegistry)
        {
            //classRegistry.entityBehaviorClassNameToTypeMapping["aimingaccuracy"] = typeof(Bullseye.EntityBehaviorAimingAccuracy);
            //classRegistry.entityBehaviorTypeToClassNameMapping[typeof(Bullseye.EntityBehaviorAimingAccuracy)] = "aimingaccuracy";

            // Not replacing the vanilla AimingAccuracy behaviour for compatibility
            classRegistry.RegisterentityBehavior("bullseye.aimingaccuracy", typeof(Bullseye.EntityBehaviorAimingAccuracy));
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
                    (arrowPlayer.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, "Tracking new arrow (cannot track two arrows at once!)", EnumChatType.Notification);
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
        long followArrowTickListenerId = -1;

        private void CommandTrackArrows(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
        {
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
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            serverInstance = this;

            sapi.RegisterCommand("bullseye", "", "", (IServerPlayer player, int groupId, CmdArgs args) => {
                if (args.Length > 0)
                {
                    string cmd = args.PopWord();

                    switch (cmd)
                    {
                        case "track":
                            CommandTrackArrows(sapi, player, groupId, args);
                            return;
                    }
                }

                player.SendMessage(groupId, "/bullseye [track|setting]", EnumChatType.CommandError);
            }, Privilege.controlserver);
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

        public override void StartClientSide(ICoreClientAPI capi)
        {
            clientInstance = this;

            Harmony harmony = new Harmony("vs.bullseye");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            client = capi.World as ClientMain;
            unproject = new Unproject();
            viewport = new double[4];
            rayStart = new double[4];
            rayEnd = new double[4];
            
            targetVec = new Vec3d();

            mvMatrix = Traverse.Create(client).Field<StackMatrix4>("MvMatrix").Value;
            pMatrix = Traverse.Create(client).Field<StackMatrix4>("PMatrix").Value;
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
        
        public static void SetClientRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
        {
            SystemRenderAimPatch.SetRangedWeaponStats(weaponStats);
            ClientMainPatch.SetRangedWeaponStats(weaponStats);
        }

        public static void SetClientRangedWeaponReticleTextures(int aimTextureId, int aimTextureBlockedId)
        {
            SystemRenderAimPatch.SetReticleTextures(aimTextureId, aimTextureBlockedId);
        }

        public override void Dispose()
        {
            serverInstance = null;
            currentArrow = null;
            arrowLaunchPos = null;
            arrowPlayer = null;

            clientInstance = null;
            client = null;
            unproject = null;
            pMatrix = null;
            mvMatrix = null;
        }
    }
}