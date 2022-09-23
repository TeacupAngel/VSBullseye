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
	public class BullseyeReticleRenderer : IRenderer
	{
		private ICoreClientAPI capi;
		public BullseyeSystemClientAiming clientAimingSystem;

		// Renderer
		public double RenderOrder => 0.98;
		public int RenderRange => 9999;

		// Renderer
		private LoadedTexture defaultAimTexPartCharge;
		private LoadedTexture defaultAimTexFullCharge;
		private LoadedTexture defaultAimTexBlocked;

		private LoadedTexture currentAimTexPartCharge;
		private LoadedTexture currentAimTexFullCharge;
		private LoadedTexture currentAimTexBlocked;

		private LoadedTexture aimTextureThrowCircle;

		public BullseyeReticleRenderer(ICoreClientAPI capi, BullseyeSystemClientAiming clientAimingSystem)
		{
			this.capi = capi;
			this.clientAimingSystem = clientAimingSystem;

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
			if (clientAimingSystem.Aiming && clientAimingSystem.ShowReticle)
			{
				Vec2f currentAim = clientAimingSystem.GetCurrentAim();

				LoadedTexture texture = clientAimingSystem.WeaponReadiness == EnumWeaponReadiness.FullCharge ? currentAimTexFullCharge : 
								(clientAimingSystem.WeaponReadiness == EnumWeaponReadiness.PartCharge ? currentAimTexPartCharge : currentAimTexBlocked);
				
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

				if (clientAimingSystem.WeaponStats.weaponType == BullseyeRangedWeaponType.Throw)
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

		// ---
		public void Dispose()
		{
			defaultAimTexPartCharge?.Dispose();
			defaultAimTexFullCharge?.Dispose();
			defaultAimTexBlocked?.Dispose();
			aimTextureThrowCircle?.Dispose();

			capi = null;
		}
	}
}