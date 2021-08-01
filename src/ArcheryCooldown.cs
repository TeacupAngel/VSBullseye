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
    public class ArcheryCooldown : ModSystem
    {
        Dictionary<string, double> cooldownByPlayerUID = new Dictionary<string, double>();

        public void SetCooldownTime(string playerUID)
        {
            double time = 0;

            cooldownByPlayerUID[playerUID] = time;

            Console.WriteLine(String.Format("Setting shot time to {0}", time));
        }

        public override void Start(ICoreAPI api)
        {
            Console.WriteLine("test");
        }
    }
}