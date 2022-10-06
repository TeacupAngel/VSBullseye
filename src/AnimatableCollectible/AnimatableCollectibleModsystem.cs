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

namespace AnimatableCollectible
{
	public delegate void ItemRenderTpDelegate(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, EntityAgent entity, bool isRight, EnumAnimatableCollectibleRenderStage stage);

	public enum EnumAnimatableCollectibleRenderStage
	{
		Standard,
		ShadowPass,
		//ShadowPassBatched
	}
 
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AnimatableCollectibleStartAnimPacket
    {
		public long EntityId;
		public int Hand;
        public uint AnimationByCrc32;
		public float AnimationSpeed;
    }

	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AnimatableCollectibleStopAnimPacket
    {
		public long EntityId;
		public int Hand;
        public uint AnimationByCrc32;
    }

	/*public class AnimatableCollectibleEntityAnimator
	{ 
		public CollectibleObject Collectible;
		public AnimatorBase Animator;
		public Dictionary<string, AnimationMetaData> ActiveAnimationsByAnimCode = new Dictionary<string, AnimationMetaData>();
	}*/

    public class AnimatableCollectibleModsystem : ModSystem
    {
		// Serverside
		ICoreServerAPI sapi;
		IServerNetworkChannel serverNetworkChannel;

		// Temporarily disabled
		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return false;
		}

		public override void StartServerSide(ICoreServerAPI api)
        {
			HarmonyPatches.EntityCommonPatch.Patch(harmony);
			HarmonyPatches.AnimatableCollectibleAnimatonManagerCommonPatch.Patch(harmony);

			sapi = api;
			serverNetworkChannel = api.Network.GetChannel("animatableCollectible");
        }

		public void SendAnimationStartPacket(Entity entity, EnumHand hand, uint animationByCrc32, float animationSpeed, bool ignoreEntityPlayer)
		{
			ActionConsumable<IPlayer> filter = ignoreEntityPlayer ? (ActionConsumable<IPlayer>)((IPlayer player) => {return player.Entity != entity;}) : null;

			IServerPlayer[] players = GetServerPlayersAround(entity.ServerPos.XYZ, 256f, 128f, filter);

			if (players.Length > 0)
			{
				serverNetworkChannel.SendPacket(new AnimatableCollectibleStartAnimPacket {
					EntityId = entity.EntityId,
					Hand = (int)hand,
					AnimationByCrc32 = animationByCrc32,
					AnimationSpeed = animationSpeed
				}, players);
			}
		}

		public void SendAnimationStopPacket(Entity entity, EnumHand hand, uint animationByCrc32, bool ignoreEntityPlayer)
		{
			ActionConsumable<IPlayer> filter = ignoreEntityPlayer ? (ActionConsumable<IPlayer>)((IPlayer player) => {return player.Entity != entity;}) : null;

			IServerPlayer[] players = GetServerPlayersAround(entity.ServerPos.XYZ, 256f, 128f, filter);

			if (players.Length > 0)
			{
				serverNetworkChannel.SendPacket(new AnimatableCollectibleStopAnimPacket {
					EntityId = entity.EntityId,
					Hand = (int)hand,
					AnimationByCrc32 = animationByCrc32
				}, players);
			}
		}

		public IServerPlayer[] GetServerPlayersAround(Vec3d position, float horRange, float vertRange, ActionConsumable<IPlayer> matches = null)
		{
			List<IServerPlayer> list = new List<IServerPlayer>();

			foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
			{
				if (player.ConnectionState == EnumClientState.Playing && player.Entity != null && player.Entity.Pos.InRangeOf(position, horRange, vertRange) && (matches == null || matches(player)))
				{
					list.Add(player);
				}
			}
			return list.ToArray();
		}

		// Clientside
		private Harmony harmony;
		private readonly string harmonyId = "vs.animatablecollectible";

		public IShaderProgram AnimatedItemShaderProgram {get; private set;}
		public IShaderProgram AnimatedStandardShaderProgram {get; private set;}

		private ICoreClientAPI capi;

		private Dictionary<EnumItemClass, Dictionary<int, ItemRenderTpDelegate>> tpRenderDelegates = new Dictionary<EnumItemClass, Dictionary<int, ItemRenderTpDelegate>>();
		//private Dictionary<long, Dictionary<EnumHand, AnimatableCollectibleEntityAnimator>> entityHandAnimatorData = new Dictionary<long, Dictionary<EnumHand, AnimatableCollectibleEntityAnimator>>();

        public override void StartClientSide(ICoreClientAPI api)
        {
            HarmonyPatches.EntityShapeRendererClientPatch.Patch(harmony);

			if (!api.IsSinglePlayer)
			{
				HarmonyPatches.EntityCommonPatch.Patch(harmony);
				HarmonyPatches.AnimatableCollectibleAnimatonManagerCommonPatch.Patch(harmony);
			}

			capi = api;

			capi.Event.ReloadShader += LoadAnimatedItemShaders;
            LoadAnimatedItemShaders();

			tpRenderDelegates = new Dictionary<EnumItemClass, Dictionary<int, ItemRenderTpDelegate>>()
			{
				[EnumItemClass.Item] = new Dictionary<int, ItemRenderTpDelegate>(),
				[EnumItemClass.Block] = new Dictionary<int, ItemRenderTpDelegate>()
			};

			api.Network.GetChannel("animatableCollectible")
			.SetMessageHandler<AnimatableCollectibleStartAnimPacket>(AnimationStartPacketClient)
			.SetMessageHandler<AnimatableCollectibleStopAnimPacket>(AnimationStopPacketClient);
        }

		public void AnimationStartPacketClient(AnimatableCollectibleStartAnimPacket networkMessage)
		{
			if (capi.World.GetEntityById(networkMessage.EntityId) is EntityAgent entity)
			{
				EnumHand hand = (EnumHand)networkMessage.Hand;
				ItemSlot itemSlot = hand == EnumHand.Left ? entity.LeftHandItemSlot : entity.RightHandItemSlot;

				CollectibleBehaviorAnimatable collectibleBehaviorAnimatable = itemSlot?.Itemstack?.Collectible.GetCollectibleBehavior(typeof(CollectibleBehaviorAnimatable), true) as CollectibleBehaviorAnimatable;
				collectibleBehaviorAnimatable?.StartAnimation(capi.World.GetEntityById(networkMessage.EntityId), hand, new AnimationMetaData()
				{
					Animation = "Draw",
					Code = "draw",
					AnimationSpeed = 0.5f,
					EaseOutSpeed = 6,
					EaseInSpeed = 15
				});
			}
		}

		public void AnimationStopPacketClient(AnimatableCollectibleStopAnimPacket networkMessage)
		{
			if (capi.World.GetEntityById(networkMessage.EntityId) is EntityAgent entity)
			{
				EnumHand hand = (EnumHand)networkMessage.Hand;
				ItemSlot itemSlot = hand == EnumHand.Left ? entity.LeftHandItemSlot : entity.RightHandItemSlot;

				CollectibleBehaviorAnimatable collectibleBehaviorAnimatable = itemSlot?.Itemstack?.Collectible.GetCollectibleBehavior(typeof(CollectibleBehaviorAnimatable), true) as CollectibleBehaviorAnimatable;
				//collectibleBehaviorAnimatable?.StopAnimation(networkMessage.EntityId, hand, ne);
			}
		}

		public bool LoadAnimatedItemShaders()
        {
            AnimatedItemShaderProgram = capi.Shader.NewShaderProgram();
			(AnimatedItemShaderProgram as ShaderProgram).AssetDomain = Mod.Info.ModID;
			capi.Shader.RegisterFileShaderProgram("helditemanimated", AnimatedItemShaderProgram);
            AnimatedItemShaderProgram.Compile();

			AnimatedStandardShaderProgram = capi.Shader.NewShaderProgram();
			(AnimatedStandardShaderProgram as ShaderProgram).AssetDomain = Mod.Info.ModID;
			capi.Shader.RegisterFileShaderProgram("standardanimated", AnimatedStandardShaderProgram);
            AnimatedStandardShaderProgram.Compile();

            return true;
        }

		public void RegisterItemstackTpRenderer(CollectibleObject collectible, ItemRenderTpDelegate rendererDelegate)
		{
			tpRenderDelegates[collectible.ItemClass][collectible.Id] = rendererDelegate;
		}

		public ItemRenderTpDelegate GetItemstackTpRenderer(CollectibleObject collectible)
		{
			tpRenderDelegates[collectible.ItemClass].TryGetValue(collectible.Id, out ItemRenderTpDelegate result);

			return result;
		}

		/*public AnimatableCollectibleEntityAnimator GetEntityHandAnimatorData(long entityId, EnumHand hand)
		{
			if (entityHandAnimatorData.TryGetValue(entityId, out Dictionary<EnumHand, AnimatableCollectibleEntityAnimator> handDict))
			{
				if (handDict.TryGetValue(hand, out AnimatableCollectibleEntityAnimator data))
				{
					return data;
				}
			}

			return null;
		}

		public void SetEntityHandAnimatorData(long entityId, EnumHand hand, AnimatableCollectibleEntityAnimator animator)
		{
			if (!entityHandAnimatorData.ContainsKey(entityId))
			{
				entityHandAnimatorData[entityId] = new Dictionary<EnumHand, AnimatableCollectibleEntityAnimator>();
			}

			entityHandAnimatorData[entityId][hand] = animator;
		}*/

		// Common
		public override void StartPre(ICoreAPI api)
		{
			harmony = new Harmony(harmonyId);
		}

		public override void Start(ICoreAPI api)
		{
			api.RegisterCollectibleBehaviorClass("Animatable", typeof(CollectibleBehaviorAnimatable));
			api.RegisterCollectibleBehaviorClass("MultiAnimatable", typeof(CollectibleBehaviorMultiAnimatable));
			api.RegisterCollectibleBehaviorClass("AnimatableWithAttach", typeof(CollectibleBehaviorAnimatableWithAttach));

			api.Network.RegisterChannel("animatableCollectible")
			.RegisterMessageType<AnimatableCollectibleStartAnimPacket>()
			.RegisterMessageType<AnimatableCollectibleStopAnimPacket>();
		}

        public override void Dispose()
        {
			// Server
			sapi = null;
			serverNetworkChannel = null;

			// Client
			capi = null;

			AnimatedItemShaderProgram = null;
			AnimatedStandardShaderProgram = null;

			HarmonyPatches.EntityShapeRendererClientPatch.Unpatch();
			harmony?.UnpatchAll(harmonyId);
			harmony = null;
        }
    }
}