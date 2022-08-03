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
	public class BullseyeSystemClientAiming : ModSystem, IRenderer
	{
		public BullseyeSystemConfig configSystem;

		public bool Aiming = false;

		private Random random;
		private ICoreClientAPI capi;

		private bool disposed = false;

		private EnumAppSide atSide;

		// Renderer
		public double RenderOrder => 0.98;
		public int RenderRange => 9999;

		public override bool ShouldLoad(EnumAppSide forSide)
		{
			atSide = forSide;
			return forSide == EnumAppSide.Client;
		}

		public override void StartClientSide(ICoreClientAPI capi)
		{
			this.capi = capi;

			configSystem = capi.ModLoader.GetModSystem<BullseyeSystemConfig>();

			StartAimSystem(capi);

			//random = new Random((int)(capi.World.Seed * capi.World.Calendar.TotalHours));
			random = new Random();

			StartRenderer(capi);
			capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho);
		}

		public void SetRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
		{
			this.weaponStats = weaponStats;
		}

		// Aiming system
		private float aimX;
		private float aimY;

		private float aimOffsetX;
		private float aimOffsetY;

		private bool showAim = true;

		private BullseyeRangedWeaponStats weaponStats = new BullseyeRangedWeaponStats();

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

			return new Vec2f(aimX + aimOffsetX * offsetMagnitude * weaponStats.horizontalAccuracyMult, aimY + aimOffsetY * offsetMagnitude * weaponStats.verticalAccuracyMult);
		}

		public void UpdateAimPoint(ClientMain __instance, 
				ref double ___MouseDeltaX, ref double ___MouseDeltaY, 
				ref double ___DelayedMouseDeltaX, ref double ___DelayedMouseDeltaY,
				float dt)
		{			
			if (Aiming)
			{
				// = Aiming system #3 - simpler, Receiver-inspired =
				switch (weaponStats.weaponType)
				{
					case BullseyeRangedWeaponType.Sling: UpdateAimOffsetSling(__instance, dt); break;
					default: UpdateAimOffsetSimple(__instance, dt); break;
				}

				// Aiming itself
				float horizontalAimLimit = __instance.Width / 2f * weaponStats.horizontalLimit;
				float verticalAimLimit = __instance.Height / 2f * weaponStats.verticalLimit;
				float verticalAimOffset = __instance.Height / 2f * weaponStats.verticalOffset;

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
			float fovRatio = __instance.Width / 1920f;

			float driftMaxRatio = 0.9f;
			float twitchMaxRatio = 7f;

			aimOffsetX += (((float)noisegen.Noise(__instance.ElapsedMilliseconds * weaponStats.aimDriftFrequency, 1000f) - 0.5f) - aimOffsetX / (weaponStats.aimDrift / driftMaxRatio * DriftMultiplier)) * weaponStats.aimDrift * DriftMultiplier * dt * fovRatio;
			aimOffsetY += (((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * weaponStats.aimDriftFrequency) - 0.5f) - aimOffsetY / (weaponStats.aimDrift / driftMaxRatio * DriftMultiplier)) * weaponStats.aimDrift * DriftMultiplier * dt * fovRatio;

			if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + weaponStats.aimTwitchDuration)
			{
				twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
				twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

				float twitchMax = weaponStats.aimTwitch / twitchMaxRatio * TwitchMultiplier;

				twitchX = (((float)random.NextDouble() - 0.5f) * 2f) * twitchMax - aimOffsetX / twitchMax;
				twitchY = (((float)random.NextDouble() - 0.5f) * 2f) * twitchMax - aimOffsetY / twitchMax;

				twitchLength = GameMath.Sqrt(twitchX * twitchX + twitchY * twitchY);

				twitchX = twitchX / twitchLength;
				twitchY = twitchY / twitchLength;
			}

			float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.aimTwitchDuration;
			float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.aimTwitchDuration;

			float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

			aimOffsetX += twitchX * stepSize * (weaponStats.aimTwitch * TwitchMultiplier * dt) * (weaponStats.aimTwitchDuration / 20) * fovRatio;
			aimOffsetY += twitchY * stepSize * (weaponStats.aimTwitch * TwitchMultiplier * dt) * (weaponStats.aimTwitchDuration / 20) * fovRatio;

			twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;
		}

		// In progress fixing of an FOV bug
		/*public void UpdateAimOffsetSimple(ClientMain __instance, float dt)
		{
			// Default FOV is 70, and 1920 is the screen width of my dev machine :)
			float fovRatio = (__instance.Width / 1920f) * (70f / ClientSettings.FieldOfView) * (70f / ClientSettings.FieldOfView);

			float driftMaxRatio = 0.9f;
			float twitchMaxRatio = 7f;

			aimOffsetX += (((float)noisegen.Noise(__instance.ElapsedMilliseconds * weaponStats.aimDriftFrequency, 1000f) - 0.5f) - aimOffsetX / (weaponStats.aimDrift / driftMaxRatio * DriftMultiplier)) * weaponStats.aimDrift * DriftMultiplier * dt * fovRatio;
			aimOffsetY += (((float)noisegen.Noise(-1000f, __instance.ElapsedMilliseconds * weaponStats.aimDriftFrequency) - 0.5f) - aimOffsetY / (weaponStats.aimDrift / driftMaxRatio * DriftMultiplier)) * weaponStats.aimDrift * DriftMultiplier * dt * fovRatio;

			if (__instance.Api.World.ElapsedMilliseconds > twitchLastChangeMilliseconds + weaponStats.aimTwitchDuration)
			{
				twitchLastChangeMilliseconds = __instance.Api.World.ElapsedMilliseconds;
				twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;

				float twitchMax = weaponStats.aimTwitch / twitchMaxRatio * TwitchMultiplier;

				twitchX = (((float)random.NextDouble() - 0.5f) * 2f) * twitchMax - aimOffsetX / twitchMax;
				twitchY = (((float)random.NextDouble() - 0.5f) * 2f) * twitchMax - aimOffsetY / twitchMax;

				twitchLength = GameMath.Sqrt(twitchX * twitchX + twitchY * twitchY);

				twitchX = twitchX / twitchLength;
				twitchY = twitchY / twitchLength;
			}

			float lastStep = (twitchLastStepMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.aimTwitchDuration;
			float currentStep = (__instance.Api.World.ElapsedMilliseconds - twitchLastChangeMilliseconds) / (float)weaponStats.aimTwitchDuration;

			float stepSize = ((1f - lastStep) * (1f - lastStep)) - ((1f - currentStep) * (1f - currentStep));

			aimOffsetX += twitchX * stepSize * (weaponStats.aimTwitch * TwitchMultiplier * dt) * (weaponStats.aimTwitchDuration / 20) * fovRatio;
			aimOffsetY += twitchY * stepSize * (weaponStats.aimTwitch * TwitchMultiplier * dt) * (weaponStats.aimTwitchDuration / 20) * fovRatio;

			twitchLastStepMilliseconds = __instance.Api.World.ElapsedMilliseconds;
		}*/

		private float slingDt;

		private float slingHorizRandomOffset;

		public void UpdateAimOffsetSling(ClientMain __instance, float dt)
		{
			float fovRatio = (__instance.Width / 1920f) * (70f / ClientSettings.FieldOfView);

			float slingCycleLength = 0.75f;
			float slingCycleStartDeadzone = 0.2f;
			float slingCycleEndCoyoteTime = 0.1f; // Human visual reaction time is 250ms on average, a little 'coyote time' makes shooting more satisfying

			float slingRiseArea = 450 * fovRatio;
			float slingHorizArea = 45 * fovRatio;

			float slingHorizTwitch = weaponStats.aimTwitch * TwitchMultiplier * fovRatio;

			slingDt += dt;
			
			if (slingDt >= slingCycleLength)
			{
				slingDt -= slingCycleLength;
				slingHorizRandomOffset = slingHorizTwitch * (((float)random.NextDouble() - 0.5f) * 2f);
			}

			showAim = slingDt > slingCycleStartDeadzone && slingDt < slingCycleLength - slingCycleEndCoyoteTime;

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
			unproject.UnProject(mouseCurrentX, capi.Render.FrameHeight - mouseCurrentY, 1, capi.Render.MvMatrix.Top, capi.Render.PMatrix.Top, viewport, rayEnd);
			unproject.UnProject(mouseCurrentX, capi.Render.FrameHeight - mouseCurrentY, 0, capi.Render.MvMatrix.Top, capi.Render.PMatrix.Top, viewport, rayStart);
			double offsetX = rayEnd[0] - rayStart[0];
			double offsetY = rayEnd[1] - rayStart[1];
			double offsetZ = rayEnd[2] - rayStart[2];
			float length = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ);
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

			showAim = true;

			slingDt = 0;
		}

		public void ResetAim()
		{
			aimX = 0f;
			aimY = 0f;

			ResetAimOffset();
		}

		// Renderer
		private LoadedTexture defaultAimTexPartCharge;
		private LoadedTexture defaultAimTexFullCharge;
		private LoadedTexture defaultAimTexBlocked;

		private LoadedTexture currentAimTexPartCharge;
		private LoadedTexture currentAimTexFullCharge;
		private LoadedTexture currentAimTexBlocked;

		private LoadedTexture aimTextureThrowCircle;

		public enum EnumReadinessState
		{
			Blocked,
			PartCharge,
			FullCharge
		}

		public EnumReadinessState ReadinessState = EnumReadinessState.Blocked;

		private void StartRenderer(ICoreClientAPI capi)
		{
			defaultAimTexPartCharge = new LoadedTexture(capi);
			defaultAimTexFullCharge = new LoadedTexture(capi);
			defaultAimTexBlocked = new LoadedTexture(capi);

			aimTextureThrowCircle = new LoadedTexture(capi);

			capi.Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimdefaultpart.png"), ref defaultAimTexPartCharge);
			capi.Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimdefaultfull.png"), ref defaultAimTexFullCharge);
			capi.Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/aimblockeddefault.png"), ref defaultAimTexBlocked);

			capi.Render.GetOrLoadTexture(new AssetLocation("bullseye", "gui/throw_circle.png"), ref aimTextureThrowCircle);
		}

		public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			if (Aiming && showAim)
			{
				Vec2f currentAim = GetCurrentAim();

				LoadedTexture texture = ReadinessState == EnumReadinessState.FullCharge ? currentAimTexFullCharge : 
								(ReadinessState == EnumReadinessState.PartCharge ? currentAimTexPartCharge : currentAimTexBlocked);
				
				capi.Render.Render2DTexture(texture.TextureId, 
					(capi.Render.FrameWidth / 2) - (texture.Width / 2) + currentAim.X, 
					(capi.Render.FrameHeight / 2) - (texture.Height / 2) + currentAim.Y, 
					texture.Width, texture.Height, 10000f)
				;

				// Puts a dot straight on the aiming spot. Useful for debugging
				/*capi.Render.Render2DTexture(defaultAimTexFullCharge.TextureId, 
					(capi.Render.FrameWidth / 2) - (texture.Width / 2) + aimX, 
					(capi.Render.FrameHeight / 2) - (texture.Height / 2) + aimY, 
					texture.Width, texture.Height, 10000f)
				;*/

				if (weaponStats.weaponType == BullseyeRangedWeaponType.Throw)
				{
					capi.Render.Render2DTexture(aimTextureThrowCircle.TextureId, 
						(capi.Render.FrameWidth / 2) - (aimTextureThrowCircle.Width / 2), 
						(capi.Render.FrameHeight / 2) - (aimTextureThrowCircle.Height / 2), 
						aimTextureThrowCircle.Width, aimTextureThrowCircle.Height, 10001f)
					;
				}
			}
		}

		public void SetReticleTextures(LoadedTexture partChargeTex, LoadedTexture fullChargeTex, LoadedTexture blockedTex)
		{
			currentAimTexPartCharge = partChargeTex.TextureId > 0 ? partChargeTex : defaultAimTexPartCharge;
			currentAimTexFullCharge = fullChargeTex.TextureId > 0 ? fullChargeTex : defaultAimTexFullCharge;
			currentAimTexBlocked = blockedTex.TextureId > 0 ? blockedTex : defaultAimTexBlocked;
		}

		public void SetShootReadinessState(EnumReadinessState state)
		{
			ReadinessState = state;
		}

		// ---
		public override void Dispose()
		{
			if (atSide == EnumAppSide.Client && !disposed)
			{
				unproject = null;

				capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);

				defaultAimTexPartCharge?.Dispose();
				defaultAimTexFullCharge?.Dispose();
				defaultAimTexBlocked?.Dispose();
				aimTextureThrowCircle?.Dispose();

				foreach (CollectibleObject collectible in capi.World.Collectibles)
				{
					if (collectible is BullseyeItemRangedWeapon rangedWeapon)
					{
						rangedWeapon.Dispose();
					}
				}

				capi = null;

				disposed = true;
			}

			configSystem = null;

			random = null;

			weaponStats = null;
			noisegen = null;
			unproject = null;

			base.Dispose();
		}
	}
}