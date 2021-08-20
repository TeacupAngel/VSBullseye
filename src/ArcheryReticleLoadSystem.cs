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
    public class ArcheryReticleLoadSystem : ModSystem
    {
        Dictionary<string, int> reticleTextureIdByPath = new Dictionary<string, int>();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        // Execute after blocks and items are already loaded
        /*public override double ExecuteOrder()
        {
            return 0.3;
        }*/

        ICoreClientAPI capi;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
        }

        public int GetReticleTextureId(string path)
        {
            int result;

            if (!reticleTextureIdByPath.TryGetValue(path, out result))
            {
                result = capi.Render.GetOrLoadTexture(new AssetLocation(path));
            }

            return result;
        }
    }
}