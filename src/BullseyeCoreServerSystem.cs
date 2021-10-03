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

        private EntityProjectile currentArrow;
        private EntityPos arrowLaunchPos;
        private EntityPlayer arrowPlayer;
        private long arrowStartTime;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

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

        long followArrowTickListenerId = -1;

        private void CommandTrackArrows(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
        {
            if (followArrowTickListenerId == -1)
            {
                currentArrow = null;
                followArrowTickListenerId = sapi.Event.RegisterGameTickListener(FollowArrow, 100);
                player.SendMessage(groupId, "Bullseye: now tracking arrows", EnumChatType.CommandSuccess);
            }
            else
            {
                sapi.Event.UnregisterGameTickListener(followArrowTickListenerId);
                followArrowTickListenerId = -1;
                player.SendMessage(groupId, "Bullseye: no longer tracking arrows", EnumChatType.CommandSuccess);
            }
        }

        private void CommandSet(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                string setting = args.PopWord();

                switch (setting)
                {
                    case "globalAccuracy":
                        if (args.Length > 0)
                        {
                            float argument = GameMath.Clamp((float)args.PopFloat(1f), 0f, 10f);
                            configSystem.serverConfig.globalAccuracy = argument;
                            configSystem.OnServerConfigChanged();
                            player.SendMessage(groupId, $"Bullseye: globalAccuracy set to {argument}", EnumChatType.CommandSuccess);
                        }
                        else
                        {
                            player.SendMessage(groupId, "Bullseye: globalAccuracy requries 1 parameter", EnumChatType.CommandSuccess);
                        }
                        return;
                }
            }
            else
            {
                player.SendMessage(groupId, "/bullseye set [globalAccuracy] [arguments]", EnumChatType.CommandError);
            }
        }

        private void CommandGet(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 0)
            {
                string setting = args.PopWord();

                switch (setting)
                {
                    case "globalAccuracy":
                        player.SendMessage(groupId, $"Bullseye: globalAccuracy is currently {configSystem.serverConfig.globalAccuracy}", EnumChatType.CommandSuccess);
                        return;
                }
            }
            else
            {
                player.SendMessage(groupId, "/bullseye get [globalAccuracy]", EnumChatType.CommandError);
            }
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            sapi.RegisterCommand("bullseye", "", "", (IServerPlayer player, int groupId, CmdArgs args) => {
                if (args.Length > 0)
                {
                    string cmd = args.PopWord();

                    switch (cmd)
                    {
                        case "track":
                            CommandTrackArrows(sapi, player, groupId, args);
                            return;
                        case "set":
                            CommandSet(sapi, player, groupId, args);
                            return;
                        case "get":
                            CommandGet(sapi, player, groupId, args);
                            return;
                    }
                }

                player.SendMessage(groupId, "/bullseye [track|set|get]", EnumChatType.CommandError);
            }, Privilege.controlserver);

            configSystem = sapi.ModLoader.GetModSystem<BullseyeConfigSystem>();
        }

        public override void Dispose()
        {
            configSystem = null;

            currentArrow = null;
            arrowLaunchPos = null;
            arrowPlayer = null;
        }
    }
}