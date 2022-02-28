using System;
using System.Collections.Generic;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using ProtoBuf;
using System.Reflection;

using HarmonyLib;

namespace AnimatableCollectibleSimple
{
    public class AnimatableCollectibleSimpleModsystem : ModSystem
    {
		// Clientside
		public IShaderProgram AnimatedItemShaderProgram {get; private set;}

		private ICoreClientAPI capi;

		/*public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }*/

        public override void StartClientSide(ICoreClientAPI api)
        {
			capi = api;

			capi.Event.ReloadShader += LoadAnimatedItemShaders;
            LoadAnimatedItemShaders();
        }

		public bool LoadAnimatedItemShaders()
        {
            AnimatedItemShaderProgram = capi.Shader.NewShaderProgram();
			(AnimatedItemShaderProgram as ShaderProgram).AssetDomain = Mod.Info.ModID;
			capi.Shader.RegisterFileShaderProgram("helditemanimated", AnimatedItemShaderProgram);
            AnimatedItemShaderProgram.Compile();

            return true;
        }

		public override void Start(ICoreAPI api)
		{
			api.RegisterCollectibleBehaviorClass("Animatable", typeof(CollectibleBehaviorAnimatableSimple));
			api.RegisterCollectibleBehaviorClass("AnimatableWithAttach", typeof(CollectibleBehaviorAnimatableSimpleWithAttach));
		}

        public override void Dispose()
        {
			capi = null;

			AnimatedItemShaderProgram = null;
        }
    }
}