using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using ProtoBuf;

using Cairo;

[assembly: ModInfo( "Archery",
	Description = "",
	Website     = "",
	Authors     = new []{ "rahjital" } )]

namespace Archery
{
    public class ArcheryDistanceChatCommand : ClientChatCommand
    {
        public override void CallHandler(IPlayer player, int groupId, CmdArgs args)
        {
            EntityPos currentPos = player.Entity.Pos.Copy();

            if (ArcheryCore.distancePos == null)
            {
                ArcheryCore.distancePos = currentPos;
                (player.Entity.World.Api as ICoreClientAPI)?.ShowChatMessage("Measurement start point set");
            }
            else 
            {
                float distance = (float)currentPos.DistanceTo(ArcheryCore.distancePos.XYZ);

                (player.Entity.World.Api as ICoreClientAPI)?.ShowChatMessage(String.Format("Measured distance is {0}", distance));

                ArcheryCore.distancePos = null;
            }
        }
    }

    /// <summary>
    /// Super basic example on how to read/set blocks in the game
    /// </summary>
    public class ArcheryCore : ModSystem
    {
        ICoreAPI api;

        public static EntityPos distancePos = null;
        
        public override void Start(ICoreAPI api)
        {
            this.api = api;

            RegisterItems();
            RegisterEntityBehaviors();
        }

        private void RegisterItems()
        {
            api.RegisterItemClass("archery.ItemBow", typeof(ArcheryItemBow));
            api.RegisterItemClass("archery.ItemSpear", typeof(ArcheryItemSpear));
        }

        private void RegisterEntityBehaviors()
        {
            api.RegisterEntityBehaviorClass("archery.aimingaccuracy", typeof(ArcheryEntityBehaviorAimingAccuracy));
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            ArcheryDistanceChatCommand distanceChatCommand = new ArcheryDistanceChatCommand()
            {
                Command = "distance",
                Description = "",
                Syntax = ""
            };
            capi.RegisterCommand(distanceChatCommand);
        }
    }
}