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

namespace Bullseye
{
	public class BullseyeCollectibleBehaviorAnimatable : CollectibleBehavior, ITexPositionSource
	{
		public AnimatorBase Animator;
		public Dictionary<string, AnimationMetaData> ActiveAnimationsByAnimCode = new Dictionary<string, AnimationMetaData>();

		// ITexPositionSource
		ITextureAtlasAPI curAtlas;
		public Size2i AtlasSize => curAtlas.Size;

		public virtual TextureAtlasPosition this[string textureCode]
		{
			get
			{
				AssetLocation texturePath = null;
				if (texturePath == null)
				{
					currentShape?.Textures.TryGetValue(textureCode, out texturePath);
				}

				if (texturePath == null)
				{
					texturePath = new AssetLocation(textureCode);
				}

				return GetOrCreateTexPos(texturePath);
			}
		}

		protected TextureAtlasPosition GetOrCreateTexPos(AssetLocation texturePath)
		{
			TextureAtlasPosition texpos = curAtlas[texturePath];

			if (texpos == null)
			{
				IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
				if (texAsset != null)
				{
					curAtlas.GetOrInsertTexture(texturePath, out _, out texpos);
				}
				else
				{
					capi.World.Logger.Warning("Bullseye.CollectibleBehaviorAnimatable: Item {0} defined texture {1}, not no such texture found.", collObj.Code, texturePath);
				}
			}

			return texpos;
		}

		public BullseyeCollectibleBehaviorAnimatable(CollectibleObject collObj) : base(collObj)
		{
		}

		protected string cacheKey => "animatedCollectibleMeshes-" + collObj.Code.ToShortString();

		protected BullseyeSystemAnimatable modsystem;

		protected ICoreClientAPI capi;
		protected Shape currentShape;
		protected MeshRef currentMeshRef;

		protected string animatedShapePath;
		protected bool onlyWhenAnimating;

		protected float[] tmpMvMat = Mat4f.Create();

		public override void Initialize(JsonObject properties)
		{
			animatedShapePath = properties["animatedShape"].AsString(null);
			onlyWhenAnimating = properties["onlyWhenAnimating"].AsBool(true);

			base.Initialize(properties);
		}

		public override void OnLoaded(ICoreAPI api)
		{
			modsystem = api.ModLoader.GetModSystem<BullseyeSystemAnimatable>();

			if (api.Side == EnumAppSide.Client)
			{
				if (!(collObj is Item))
				{
					throw new InvalidOperationException("CollectibleBehaviorAnimatable can only be used on Items, not Blocks!");
				}

				capi = api as ICoreClientAPI;

				InitAnimatable();

				// Don't bother registering a renderer if we can't get the proper data. Should hopefully save us from some crashes
				if (Animator == null || currentMeshRef == null) return;

				capi.Event.RegisterItemstackRenderer(collObj, (inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize) => RenderHandFp(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize), EnumItemRenderTarget.HandFp);
			}
		}

		public virtual void InitAnimatable()
		{
			Item item = (collObj as Item);

			curAtlas = capi.ItemTextureAtlas;

			AssetLocation loc = animatedShapePath != null ? new AssetLocation(animatedShapePath) : item.Shape.Base.Clone();
			loc = loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
			currentShape = Shape.TryGet(capi, loc);

			if (currentShape == null) return;

			Vec3f rendererRot = new Vec3f(0f, 1f, 0f);

			MeshData meshData = InitializeMeshData(cacheKey, currentShape, this);
			currentMeshRef = InitializeMeshRef(meshData);

			Animator = GetAnimator(capi, cacheKey, currentShape);
		}

		public MeshData InitializeMeshData(string cacheDictKey, Shape shape, ITexPositionSource texSource)
		{
			if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

			Item item = (collObj as Item);

			MeshData meshdata;

			shape.ResolveReferences(capi.World.Logger, cacheDictKey);
			CacheInvTransforms(shape.Elements);
			shape.ResolveAndLoadJoints();

			capi.Tesselator.TesselateShapeWithJointIds("collectible", shape, out meshdata, texSource, null);

			return meshdata;
		}

		public MeshRef InitializeMeshRef(MeshData meshdata) 
		{            
			MeshRef meshRef = null;

			if (RuntimeEnv.MainThreadId == Environment.CurrentManagedThreadId)
			{
				meshRef = capi.Render.UploadMesh(meshdata);
			} else
			{
				capi.Event.EnqueueMainThreadTask(() => {
					meshRef = capi.Render.UploadMesh(meshdata);
				}, "uploadmesh");
			}

			return meshRef;
		}

		public static AnimatorBase GetAnimator(ICoreClientAPI capi, string cacheDictKey, Shape blockShape)
		{
			if (blockShape == null)
			{
				return null;
			}

			object animCacheObj;
			Dictionary<string, AnimCacheEntry> animCache = null;
			capi.ObjectCache.TryGetValue("coAnimCache", out animCacheObj);
			animCache = animCacheObj as Dictionary<string, AnimCacheEntry>;
			if (animCache == null)
			{
				capi.ObjectCache["coAnimCache"] = animCache = new Dictionary<string, AnimCacheEntry>();
			}

			AnimatorBase animator;

			AnimCacheEntry cacheObj = null;
			if (animCache.TryGetValue(cacheDictKey, out cacheObj))
			{
				animator = capi.Side == EnumAppSide.Client ?
					new ClientAnimator(() => 1, cacheObj.RootPoses, cacheObj.Animations, cacheObj.RootElems, blockShape.JointsById) :
					new ServerAnimator(() => 1, cacheObj.RootPoses, cacheObj.Animations, cacheObj.RootElems, blockShape.JointsById)
				;
			}
			else
			{
				for (int i = 0; blockShape.Animations != null && i < blockShape.Animations.Length; i++)
				{
					blockShape.Animations[i].GenerateAllFrames(blockShape.Elements, blockShape.JointsById);
				}

				animator = capi.Side == EnumAppSide.Client ?
					new ClientAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById) :
					new ServerAnimator(() => 1, blockShape.Animations, blockShape.Elements, blockShape.JointsById)
				;

				animCache[cacheDictKey] = new AnimCacheEntry()
				{
					Animations = blockShape.Animations,
					RootElems = (animator as ClientAnimator).rootElements,
					RootPoses = (animator as ClientAnimator).RootPoses
				};
			}

			return animator;
		}

		public static void CacheInvTransforms(ShapeElement[] elements)
		{
			if (elements == null) return;

			for (int i = 0; i < elements.Length; i++)
			{
				elements[i].CacheInverseTransformMatrix();
				CacheInvTransforms(elements[i].Children);
			}
		}

		public void StartAnimation(AnimationMetaData metaData)
		{
			if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");

			if (!ActiveAnimationsByAnimCode.ContainsKey(metaData.Code))
			{
				ActiveAnimationsByAnimCode[metaData.Code] = metaData;
			}
		}

		public void StopAnimation(string code, bool forceImmediate = false)
		{
			if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented.");

			if (ActiveAnimationsByAnimCode.ContainsKey(code) && forceImmediate)
			{
				RunningAnimation anim = Array.Find(Animator.anims, (anim) => {return anim.Animation.Code == code;});
				anim.EasingFactor = 0f;
			}

			ActiveAnimationsByAnimCode.Remove(code);
		}

		public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
		{
			if (Animator == null || capi.IsGamePaused || target != EnumItemRenderTarget.HandFp) return; // We don't get entity here, so only do it for the FP target

			if (ActiveAnimationsByAnimCode.Count > 0 || Animator.ActiveAnimationCount > 0)
			{
				Animator.OnFrame(ActiveAnimationsByAnimCode, renderinfo.dt);
			}
		}

		public virtual void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, double posX, double posY, double posZ, float size, int color, bool rotate = false, bool showStackSize = true)
		{
			if (onlyWhenAnimating && ActiveAnimationsByAnimCode.Count == 0)
			{
				capi.Render.RenderMultiTextureMesh(renderInfo.ModelRef);
			}
			else
			{
				IShaderProgram prevProg = capi.Render.CurrentActiveShader;
				IShaderProgram prog = null;

				IRenderAPI rpi = capi.Render;
				prevProg?.Stop();

				prog = modsystem.AnimatedItemShaderProgram;
				prog.Use();
				prog.Uniform("alphaTest", collObj.RenderAlphaTest);
				prog.UniformMatrix("modelViewMatrix", modelMat.Values);
				prog.Uniform("normalShaded", renderInfo.NormalShaded ? 1 : 0);
				prog.Uniform("overlayOpacity", renderInfo.OverlayOpacity);

				if (renderInfo.OverlayTexture != null && renderInfo.OverlayOpacity > 0f)
				{
					prog.Uniform("tex2dOverlay", renderInfo.OverlayTexture.TextureId);
					prog.Uniform("overlayTextureSize", new Vec2f(renderInfo.OverlayTexture.Width, renderInfo.OverlayTexture.Height));
					prog.Uniform("baseTextureSize", new Vec2f(renderInfo.TextureSize.Width, renderInfo.TextureSize.Height));
					TextureAtlasPosition textureAtlasPosition = capi.Render.GetTextureAtlasPosition(inSlot.Itemstack);
					prog.Uniform("baseUvOrigin", new Vec2f(textureAtlasPosition.x1, textureAtlasPosition.y1));
				}

				Vec4f lightRGBSVec4f = capi.World.BlockAccessor.GetLightRGBs((int)(capi.World.Player.Entity.Pos.X + capi.World.Player.Entity.LocalEyePos.X), (int)(capi.World.Player.Entity.Pos.Y + capi.World.Player.Entity.LocalEyePos.Y), (int)(capi.World.Player.Entity.Pos.Z + capi.World.Player.Entity.LocalEyePos.Z));
				int num16 = (int)inSlot.Itemstack.Collectible.GetTemperature(capi.World, inSlot.Itemstack);
				float[] incandescenceColorAsColor4f = ColorUtil.GetIncandescenceColorAsColor4f(num16);
				int num17 = GameMath.Clamp((num16 - 550) / 2, 0, 255);
				Vec4f rgbaGlowIn = new Vec4f(incandescenceColorAsColor4f[0], incandescenceColorAsColor4f[1], incandescenceColorAsColor4f[2], (float)num17 / 255f);
				prog.Uniform("extraGlow", num17);
				prog.Uniform("rgbaAmbientIn", capi.Ambient.BlendedAmbientColor);
				prog.Uniform("rgbaLightIn", lightRGBSVec4f);
				prog.Uniform("rgbaGlowIn", rgbaGlowIn);

				float[] tmpVals = new float[4];
				Vec4f outPos = new Vec4f();
				float[] array = Mat4f.Create();
				Mat4f.RotateY(array, array, capi.World.Player.Entity.SidedPos.Yaw);
				Mat4f.RotateX(array, array, capi.World.Player.Entity.SidedPos.Pitch);
				Mat4f.Mul(array, array, modelMat.Values);
				tmpVals[0] = capi.Render.ShaderUniforms.LightPosition3D.X;
				tmpVals[1] = capi.Render.ShaderUniforms.LightPosition3D.Y;
				tmpVals[2] = capi.Render.ShaderUniforms.LightPosition3D.Z;
				tmpVals[3] = 0f;
				Mat4f.MulWithVec4(array, tmpVals, outPos);
				prog.Uniform("lightPosition", new Vec3f(outPos.X, outPos.Y, outPos.Z).Normalize());
				prog.UniformMatrix("toShadowMapSpaceMatrixFar", capi.Render.ShaderUniforms.ToShadowMapSpaceMatrixFar);
				prog.UniformMatrix("toShadowMapSpaceMatrixNear", capi.Render.ShaderUniforms.ToShadowMapSpaceMatrixNear);
				prog.BindTexture2D("itemTex", renderInfo.TextureId, 0);
				prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
				
				prog.UniformMatrices4x3(
					"elementTransforms",
					GlobalConstants.MaxAnimatedElements,
					Animator.Matrices4x3
				);

				capi.Render.RenderMesh(currentMeshRef);

				prog?.Stop();
				prevProg?.Use();
			}
		}
	}
}
;