using System;
using System.Collections.Generic;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using ProtoBuf;
using System.Reflection;

namespace Bullseye
{
	public class BullseyeSystemClientAiming : ModSystem
	{
		public BullseyeSystemConfig configSystem;

		public bool Aiming {get; set;} = false;
		public bool ShowReticle {get; private set;} = true;

		private Random random;
		private ICoreClientAPI capi;

		private BullseyeReticleRenderer reticleRenderer;

		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return forSide == EnumAppSide.Client;
		}

		public override void StartClientSide(ICoreClientAPI capi)
		{
			this.capi = capi;

			configSystem = capi.ModLoader.GetModSystem<BullseyeSystemConfig>();

			StartAimSystem(capi);

			//random = new Random((int)(capi.World.Seed * capi.World.Calendar.TotalHours));
			random = new Random();

			reticleRenderer = new BullseyeReticleRenderer(capi, this);
			capi.Event.RegisterRenderer(reticleRenderer, EnumRenderStage.Ortho);
		}

		public void SetRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
		{
			this.WeaponStats = weaponStats;
		}

		// Aiming system
		private float aimX;
		private float aimY;

		private float aimOffsetX;
		private float aimOffsetY;

		public BullseyeRangedWeaponStats WeaponStats {get; private set;} = new BullseyeRangedWeaponStats();

		public float DriftMultiplier {get; set;} = 1f;
		public float TwitchMultiplier {get; set;} = 1f;

		private NormalizedSimplexNoise noisegen = NormalizedSimplexNoise.FromDefaultOctaves(4, 1.0, 0.9, 123L);

		private long twitchLastChangeMilliseconds;
		private long twitchLastStepMilliseconds;

		private float twitchX;
		private float twitchY;
		private float twitchLength;

		public Vec3d TargetVec {get; private set;}

		private Unproject unproject;
		private double[] viewport;
		private double[] rayStart;
		private double[] rayEnd;

		public void StartAimSystem(ICoreClientAPI capi)
		{
			unproject = new Unproject();
			viewport = new double[4];
			rayStart = new double[4];
			rayEnd = new double[4];
			
			TargetVec = new Vec3d();
		}

		public Vec2f GetCurrentAim()
		{
			float offsetMagnitude = configSystem.GetSyncedConfig().AimDifficulty;

			if (capi.World.Player?.Entity != null)
			{
				offsetMagnitude /= GameMath.Max(capi.World.Player.Entity.Stats.GetBlended("rangedWeaponsAcc"), 0.001f);
			}

			return new Vec2f(aimX + aimOffsetX * offsetMagnitude * WeaponStats.horizontalAccuracyMult, aimY + aimOffsetY * offsetMagnitude * WeaponStats.verticalAccuracyMult);
		}

		public void UpdateAimPoint(ClientMain __instance, 
				ref double ___MouseDeltaX, ref double ___MouseDeltaY, 
				ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY,
				float dt)
		{			
			if (Aiming)
			{
				// = Aiming system #3 - simpler, Receiver-inspired =
				switch (WeaponStats.weaponType)
				{
					case BullseyeRangedWeaponType.Sling: UpdateAimOffsetSling(__instance, dt); break;
					default: UpdateAimOffsetSimple(__instance, dt); break;
				}

				// Aiming itself
				float horizontalAimLimit = (__instance.Width / 2f) * WeaponStats.horizontalLimit;
				float verticalAimLimit = (__instance.Height / 2f) * WeaponStats.verticalLimit;
				float verticalAimOffset = (__instance.Height / 2f) * WeaponStats.verticalOffset;

				float deltaX = (float)(___MouseDeltaX - ___DelayedMouseDeltaX);
				float deltaY = (float)(___MouseDeltaY - ___DelayedMouseDeltaY);

				if (Math.Abs(aimX + deltaX) > horizontalAimLimit)
				{
					aimX = aimX > 0 ? horizontalAimLimit : -horizontalAimLimit;
				}
				else
				{
					aimX += deltaX;
					___DelayedMouseDeltaX = ___MouseDeltaX;
				}

				if (Math.Abs(aimY + deltaY - verticalAimOffset) > verticalAimLimit)
				{
					aimY = (aimY > 0 ? verticalAimLimit : -verticalAimLimit) + verticalAimOffset;
				}
				else
				{
					aimY += deltaY;
					___DelayedMouseDeltaY = ___MouseDeltaY;
				}

				SetAim();
			}
		}

		public void UpdateAimOffsetSimple(ClientMain __instance, float dt)
		{
			// Default FOV is 70, and 1920 is the screen width of my dev machine :) 
			float fovRatio = (__instance.Width / 1920f) * (GameMath.Tan((70f / 2 * GameMath.DEG2RAD)) / GameMath.Tan((ClientSettings.FieldOfView / 2 * GameMath.DEG2RAD)));

			UpdateAimOffsetSimpleDrift(__instance, dt, fovRatio);
			UpdateAimOffsetSimpleTwitch(__instance, dt, fovRatio);
		}
		
		public void UpdateAimOffsetSimpleDrift(ClientMain __instance, float dt, float fovRatio)
		{
			float fovModAimOffsetX = aimOffsetX * fovRatio;
			float fovModAimOffsetY = aimOffsetY * fovRatio;

			const float driftMaxRatio = 1.1f;

			float xNoise = ((float)noisegen.Noise(__instance.ElapsedMilliseconds * WeaponStats.aimDriftFrequency, 1000f) - 0.5f);
			float yNoise = ((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * WeaponStats.aimDriftFrequency) - 0.5f);

			float maxDrift = GameMath.Max(WeaponStats.aimDrift * driftMaxRatio * DriftMultiplier, 1f) * fovRatio;

			aimOffsetX += ((xNoise - aimOffsetX / maxDrift) * WeaponStats.aimDrift * DriftMultiplier * dt * fovRatio);
			aimOffsetY += ((yNoise - aimOffsetY / maxDrift) * WeaponStats.aimDrift * DriftMultiplier * dt * fovRatio);
		}

		public void UpdateAimOffsetSimpleTwitch(ClientMain __instance, float dt, float fovRatio)
		{
			// Don't ask me why aimOffset needs to be multiplied by fovRatio here, but not in the Drift function
			// Frankly the whole thing is up for a full rework anyway, but I don't want to get into that until I get started on crossbows and stuff
			float fovModAimOffsetX = aimOffsetX * fovRatio;
			float fovModAimOffsetY = aimOffsetY * fovRatio;

			const float twitchMaxRatio = 1 / 7f;

			if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + WeaponStats.aimTwitchDuration)
			{
				twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
				twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

				float twitchMax = GameMath.Max(WeaponStats.aimTwitch * twitchMaxRatio * TwitchMultiplier, 1f) * fovRatio;

				twitchX = (((float)random.NextDouble() - 0.5f) * 2f) * twitchMax - fovModAimOffsetX / twitchMax;
				twitchY = (((float)random.NextDouble() - 0.5f) * 2f) * twitchMax - fovModAimOffsetY / twitchMax;

				twitchLength = GameMath.Max(GameMath.Sqrt(twitchX * twitchX + twitchY * twitchY), 1f);

				twitchX = twitchX / twitchLength;
				twitchY = twitchY / twitchLength;
			}

			float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)WeaponStats.aimTwitchDuration;
			float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)WeaponStats.aimTwitchDuration;

			float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

			aimOffsetX += (twitchX * stepSize * (WeaponStats.aimTwitch * TwitchMultiplier * dt) * (WeaponStats.aimTwitchDuration / 20) * fovRatio);
			aimOffsetY += (twitchY * stepSize * (WeaponStats.aimTwitch * TwitchMultiplier * dt) * (WeaponStats.aimTwitchDuration / 20) * fovRatio);

			twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;
		}

		private float slingDt;

		private float slingHorizRandomOffset;

		public void UpdateAimOffsetSling(ClientMain __instance, float dt)
		{
			float fovRatio = (__instance.Width / 1920f) * (GameMath.Tan((70f / 2 * GameMath.DEG2RAD)) / GameMath.Tan((ClientSettings.FieldOfView / 2 * GameMath.DEG2RAD)));

			const float slingCycleLength = 0.75f;
			const float slingCycleStartDeadzone = 0.2f;
			const float slingCycleEndCoyoteTime = 0.1f; // Human visual reaction time is 250ms on average, a little 'coyote time' makes shooting more satisfying

			float slingRiseArea = 450 * fovRatio;
			float slingHorizArea = 45 * fovRatio;

			float slingHorizTwitch = WeaponStats.aimTwitch * TwitchMultiplier * fovRatio;

			slingDt += dt;
			
			if (slingDt >= slingCycleLength)
			{
				slingDt -= slingCycleLength;
				slingHorizRandomOffset = slingHorizTwitch * (((float)random.NextDouble() - 0.5f) * 2f);
			}

			ShowReticle = slingDt > slingCycleStartDeadzone && slingDt < slingCycleLength - slingCycleEndCoyoteTime;

			float slingRatioCurrent = (slingDt - slingCycleStartDeadzone);
			float slingRatioMax = (slingCycleLength - slingCycleStartDeadzone - slingCycleEndCoyoteTime);

			float slingCurrentPoint = GameMath.Min(slingRatioCurrent / slingRatioMax, 1f);

			aimOffsetX = slingHorizRandomOffset - slingHorizArea * slingCurrentPoint;
			aimOffsetY = (slingRiseArea / 2f) - (slingRiseArea * slingCurrentPoint);
		}

		public void SetAim()
		{
			Vec2f currentAim = GetCurrentAim();

			int mouseCurrentX = (int)currentAim.X + capi.Render.FrameWidth / 2;
			int mouseCurrentY = (int)currentAim.Y + capi.Render.FrameHeight / 2;
			viewport[0] = 0.0;
			viewport[1] = 0.0;
			viewport[2] = capi.Render.FrameWidth;
			viewport[3] = capi.Render.FrameHeight;
			
			bool unprojectPassed = true;
			unprojectPassed |= unproject.UnProject(mouseCurrentX, capi.Render.FrameHeight - mouseCurrentY, 1, capi.Render.MvMatrix.Top, capi.Render.PMatrix.Top, viewport, rayEnd);
			unprojectPassed |= unproject.UnProject(mouseCurrentX, capi.Render.FrameHeight - mouseCurrentY, 0, capi.Render.MvMatrix.Top, capi.Render.PMatrix.Top, viewport, rayStart);
			
			// If unproject fails, well, not much we can do really. Try not to crash
			if (!unprojectPassed) return;

			double offsetX = rayEnd[0] - rayStart[0];
			double offsetY = rayEnd[1] - rayStart[1];
			double offsetZ = rayEnd[2] - rayStart[2];
			float length = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ);

			// If length is *somehow* zero, just abort not to crash. The start and end of the ray are in the same place, what to even do in that situation?
			if (length == 0) return;

			offsetX /= length;
			offsetY /= length;
			offsetZ /= length;

			TargetVec.X = offsetX;
			TargetVec.Y = offsetY;
			TargetVec.Z = offsetZ;
		}

		public void ResetAimOffset()
		{
			aimOffsetX = 0f;
			aimOffsetY = 0f;

			twitchX = 0f;
			twitchY = 0f;

			ShowReticle = true;

			slingDt = 0;
		}

		public void ResetAim()
		{
			aimX = 0f;
			aimY = 0f;

			ResetAimOffset();
		}

		public EnumWeaponReadiness WeaponReadiness {get; set;} = EnumWeaponReadiness.Blocked;

		public void SetWeaponReadinessState(EnumWeaponReadiness state)
		{
			WeaponReadiness = state;
		}

		public void SetReticleTextures(LoadedTexture partChargeTex, LoadedTexture fullChargeTex, LoadedTexture blockedTex)
		{
			reticleRenderer.SetReticleTextures(partChargeTex, fullChargeTex, blockedTex);
		}

		// ---
		public override void Dispose()
		{
			// Can be null when loading a world aborts partway through
			if (reticleRenderer != null)
			{
				capi.Event.UnregisterRenderer(reticleRenderer, EnumRenderStage.Ortho);
				reticleRenderer = null;
			}

			capi = null;

			configSystem = null;

			random = null;

			WeaponStats = null;
			noisegen = null;
			unproject = null;
		}
	}
}