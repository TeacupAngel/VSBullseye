using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

using Vintagestory.GameContent;

using Vintagestory.Client.NoObf;

using HarmonyLib;

namespace AnimatableCollectible
{
	public class CollectibleAnimatorData
	{ 
		public CollectibleObject Collectible;
		public AnimatorBase Animator;
		public Dictionary<string, AnimationMetaData> ActiveAnimationsByAnimCode = new Dictionary<string, AnimationMetaData>();
	}

    public class AnimatableCollectibleAnimatonManager : AnimationManager
    {
		public CollectibleAnimatorData leftHandAnimatorData;
		public CollectibleAnimatorData rightHandAnimatorData;

		public void OnServerTickExtended(float dt)
		{

		}

		/*
		key.IsRendered = game.frustumCuller.SphereInFrustum(key.Pos.X, key.Pos.Y, key.Pos.Z, 3.0) 
						&& xYZ.HorizontalSquareDistanceTo(key.Pos.X, key.Pos.Z) < (float)viewDistance 
						&& (key != game.EntityPlayer || game.MainCamera.CameraMode != 0 || game.AmbientManager.ShadowQuality > 0 || ClientSettings.ImmersiveFpMode) 
						&& (key == game.EntityPlayer || game.WorldMap.IsChunkRendered((int)key.Pos.X / 32, (int)key.Pos.Y / 32, (int)key.Pos.Z / 32));
		*/

		public void OnClientFrameExtended(float dt)
		{
			if (capi == null) return; // Added in 1.17 since ClientFrame is apparently called before Init
			if (capi.IsGamePaused || (!entity.IsRendered && entity.Alive && entity != capi.World.Player.Entity)) return;

			if (leftHandAnimatorData != null && (leftHandAnimatorData.ActiveAnimationsByAnimCode.Count > 0 || leftHandAnimatorData.Animator.ActiveAnimationCount > 0))
            {
				leftHandAnimatorData.Animator.OnFrame(leftHandAnimatorData.ActiveAnimationsByAnimCode, dt);
            }

			if (rightHandAnimatorData != null && (rightHandAnimatorData.ActiveAnimationsByAnimCode.Count > 0 || rightHandAnimatorData.Animator.ActiveAnimationCount > 0))
            {
				rightHandAnimatorData.Animator.OnFrame(rightHandAnimatorData.ActiveAnimationsByAnimCode, dt);
            }
		}

		public CollectibleAnimatorData GetCollectibleAnimatorData(EnumHand hand)
		{
			return hand == EnumHand.Right ? rightHandAnimatorData : leftHandAnimatorData;
		}

		public void SetCollectibleAnimatorData(CollectibleAnimatorData animatorData, EnumHand hand)
		{
			_ = hand == EnumHand.Right ? rightHandAnimatorData = animatorData : leftHandAnimatorData = animatorData;
		}
    }
}
