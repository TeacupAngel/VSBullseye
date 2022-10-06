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
using Vintagestory.GameContent;
using ProtoBuf;

using System.Reflection;

using HarmonyLib;

using Cairo;

namespace AnimatableCollectible
{
	namespace HarmonyPatches
	{
		class EntityShapeRendererClientPatch
		{
			private static AnimatableCollectibleModsystem animatableModsystem;
			private static Matrixf itemModelMat = new Matrixf();
			private static float[] mvpMat = Mat4f.Create();

			public static void Patch(Harmony harmony)
			{
				harmony.Patch(typeof(EntityShapeRenderer).GetMethod("RenderHeldItem", BindingFlags.Instance | BindingFlags.NonPublic), 
					new HarmonyMethod(typeof(EntityShapeRendererClientPatch).GetMethod("RenderHeldItemPrefix", BindingFlags.Static | BindingFlags.NonPublic) 
				));
			}

			public static void Unpatch()
			{
				animatableModsystem = null;
			}

			static bool RenderHeldItemPrefix(float dt, bool isShadowPass, bool right, EntityShapeRenderer __instance, EntityAgent ___eagent, float[] ___ModelMat, ref float ___accum)
			{
				if (animatableModsystem == null)
				{
					animatableModsystem = __instance.capi.ModLoader.GetModSystem<AnimatableCollectibleModsystem>();
				}

				IRenderAPI rapi = __instance.capi.Render;
				ItemSlot slot = right ? ___eagent?.RightHandItemSlot : ___eagent?.LeftHandItemSlot;
				ItemStack stack = slot?.Itemstack;

				if (stack == null) return true;

				ItemRenderTpDelegate renderDelegate = animatableModsystem?.GetItemstackTpRenderer(stack.Collectible);
				if (renderDelegate == null) return true;

				ItemRenderInfo renderInfo = rapi.GetItemStackRenderInfo(slot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff);
				renderInfo.dt = dt; // need to set it manually, because api.Render.GetItemStackRenderInfo sets it to zero
				if (renderInfo?.Transform == null) return false; // Happens with unknown items/blocks

				AttachmentPointAndPose apap = __instance.entity.AnimManager.Animator.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
				if (apap == null) return false;
				
				AttachmentPoint ap = apap.AttachPoint;
				
				itemModelMat
					.Set(___ModelMat)
					.Mul(apap.AnimModelMatrix)
					.Translate(renderInfo.Transform.Origin.X, renderInfo.Transform.Origin.Y, renderInfo.Transform.Origin.Z)
					.Scale(renderInfo.Transform.ScaleXYZ.X, renderInfo.Transform.ScaleXYZ.Y, renderInfo.Transform.ScaleXYZ.Z)
					.Translate(ap.PosX / 16f + renderInfo.Transform.Translation.X, ap.PosY / 16f + renderInfo.Transform.Translation.Y, ap.PosZ / 16f + renderInfo.Transform.Translation.Z)
					.RotateX((float)(ap.RotationX + renderInfo.Transform.Rotation.X) * GameMath.DEG2RAD)
					.RotateY((float)(ap.RotationY + renderInfo.Transform.Rotation.Y) * GameMath.DEG2RAD)
					.RotateZ((float)(ap.RotationZ + renderInfo.Transform.Rotation.Z) * GameMath.DEG2RAD)
					.Translate(-(renderInfo.Transform.Origin.X), -(renderInfo.Transform.Origin.Y), -(renderInfo.Transform.Origin.Z))
				;

				IStandardShaderProgram prog = null;

				if (isShadowPass)
				{
					rapi.CurrentActiveShader.BindTexture2D("tex2d", renderInfo.TextureId, 0);
					Mat4f.Mul(mvpMat, __instance.capi.Render.CurrentModelviewMatrix, itemModelMat.Values);
					Mat4f.Mul(mvpMat, __instance.capi.Render.CurrentProjectionMatrix, mvpMat);

					__instance.capi.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", mvpMat);
					__instance.capi.Render.CurrentActiveShader.Uniform("origin", new Vec3f());
				}
				else
				{
					prog = rapi.StandardShader;
					prog.Use();
					prog.DontWarpVertices = 0;
					prog.AddRenderFlags = 0;
					prog.NormalShaded = 1;
					prog.Tex2D = renderInfo.TextureId;
					prog.RgbaTint = ColorUtil.WhiteArgbVec;
					prog.AlphaTest = renderInfo.AlphaTest;

					prog.OverlayOpacity = renderInfo.OverlayOpacity;
					if (renderInfo.OverlayTexture != null && renderInfo.OverlayOpacity > 0)
					{
						prog.Tex2dOverlay2D = renderInfo.OverlayTexture.TextureId;
						prog.OverlayTextureSize = new Vec2f(renderInfo.OverlayTexture.Width, renderInfo.OverlayTexture.Height);
						prog.BaseTextureSize = new Vec2f(renderInfo.TextureSize.Width, renderInfo.TextureSize.Height);
						TextureAtlasPosition texPos = rapi.GetTextureAtlasPosition(stack);
						prog.BaseUvOrigin = new Vec2f(texPos.x1, texPos.y1);
					}

					
					Vec4f lightrgbs = __instance.capi.World.BlockAccessor.GetLightRGBs((int)(__instance.entity.Pos.X + __instance.entity.SelectionBox.X1 - __instance.entity.OriginSelectionBox.X1), (int)__instance.entity.Pos.Y, (int)(__instance.entity.Pos.Z + __instance.entity.SelectionBox.Z1 - __instance.entity.OriginSelectionBox.Z1));
					int temp = (int)stack.Collectible.GetTemperature(__instance.capi.World, stack);
					float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
					lightrgbs[0] += glowColor[0];
					lightrgbs[1] += glowColor[1];
					lightrgbs[2] += glowColor[2];

					prog.ExtraGlow = GameMath.Clamp((temp - 500) / 3, 0, 255);
					prog.RgbaAmbientIn = rapi.AmbientColor;
					prog.RgbaLightIn = lightrgbs;
					prog.RgbaFogIn = rapi.FogColor;
					prog.FogMinIn = rapi.FogMin;
					prog.FogDensityIn = rapi.FogDensity;
					prog.NormalShaded = renderInfo.NormalShaded ? 1 : 0;

					prog.ProjectionMatrix = rapi.CurrentProjectionMatrix;
					prog.ViewMatrix = rapi.CameraMatrixOriginf;
					prog.ModelMatrix = itemModelMat.Values;
				}
				
				if (!renderInfo.CullFaces)
				{
					rapi.GlDisableCullFace();
				}

				renderDelegate(slot, renderInfo, itemModelMat, ___eagent, right, isShadowPass ? EnumAnimatableCollectibleRenderStage.ShadowPass : EnumAnimatableCollectibleRenderStage.Standard);

				if (!renderInfo.CullFaces)
				{
					rapi.GlEnableCullFace();
				}

				if (!isShadowPass)
				{
					prog.Stop();

					/*AdvancedParticleProperties[] ParticleProperties = stack.Collectible?.ParticleProperties;

					if (stack.Collectible != null && !__instance.capi.IsGamePaused)
					{
						Vec4f pos = ItemModelMat.TransformVector(new Vec4f(stack.Collectible.TopMiddlePos.X, stack.Collectible.TopMiddlePos.Y, stack.Collectible.TopMiddlePos.Z, 1));
						EntityPlayer entityPlayer = __instance.capi.World.Player.Entity;
						___accum += dt;
						if (ParticleProperties != null && ParticleProperties.Length > 0 && ___accum > 0.025f)
						{
							___accum = ___accum % 0.025f;

							for (int i = 0; i < ParticleProperties.Length; i++)
							{
								AdvancedParticleProperties bps = ParticleProperties[i];
								bps.basePos.X = pos.X + __instance.entity.Pos.X + -(__instance.entity.Pos.X - entityPlayer.CameraPos.X);
								bps.basePos.Y = pos.Y + __instance.entity.Pos.Y + -(__instance.entity.Pos.Y - entityPlayer.CameraPos.Y);
								bps.basePos.Z = pos.Z + __instance.entity.Pos.Z + -(__instance.entity.Pos.Z - entityPlayer.CameraPos.Z);

								___eagent.World.SpawnParticles(bps);
							}
						}
					}*/
				}

				return false;
			}
		}

		class EntityCommonPatch
		{
			public static void Patch(Harmony harmony)
			{
				harmony.Patch(typeof(Entity).GetConstructor(Type.EmptyTypes),
					null,
					new HarmonyMethod(typeof(EntityCommonPatch).GetMethod("ConstructorPostfix", BindingFlags.Static | BindingFlags.NonPublic) 
				));
			}

			public static void Unpatch()
			{
			}

			static void ConstructorPostfix(Entity __instance)
			{
				__instance.AnimManager = new AnimatableCollectibleAnimatonManager();
			}
		}

		class AnimatableCollectibleAnimatonManagerCommonPatch
		{
			public static void Patch(Harmony harmony)
			{
				harmony.Patch(typeof(AnimationManager).GetMethod("OnServerTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy), 
					null,
					new HarmonyMethod(typeof(AnimatableCollectibleAnimatonManagerCommonPatch).GetMethod("OnServerTickPostfix", BindingFlags.Static | BindingFlags.NonPublic) 
				));

				harmony.Patch(typeof(AnimationManager).GetMethod("OnClientFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy), 
					null,
					new HarmonyMethod(typeof(AnimatableCollectibleAnimatonManagerCommonPatch).GetMethod("OnClientFramePostfix", BindingFlags.Static | BindingFlags.NonPublic) 
				));
			}

			public static void Unpatch()
			{
			}

			static void OnServerTickPostfix(AnimatableCollectibleAnimatonManager __instance, float dt)
			{
				__instance.OnServerTickExtended(dt);
			}

			static void OnClientFramePostfix(AnimatableCollectibleAnimatonManager __instance, float dt)
			{
				__instance.OnClientFrameExtended(dt);
			}
		}
	}
}