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
    public class CollectibleBehaviorAnimatable : CollectibleBehavior, ITexPositionSource
    {
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
                    BitmapRef bmp = texAsset.ToBitmap(capi);
                    curAtlas.InsertTextureCached(texturePath, bmp, out _, out texpos);
                }
                else
                {
                    capi.World.Logger.Warning("AnimatableCollectible: Item {0} defined texture {1}, not no such texture found.", collObj.Code, texturePath);
                }
            }

            return texpos;
        }

		// CollectibleBehaviorAnimatable
        public CollectibleBehaviorAnimatable(CollectibleObject collObj) : base(collObj)
        {
        }

		protected string cacheKey => "animatedCollectibleMeshes-" + collObj.Code.ToShortString();

		protected AnimatableCollectibleModsystem modsystem;

		protected ICoreClientAPI capi;
		protected Shape currentShape;
		protected MeshRef currentMeshRef;

		//protected AnimatorBase currentAnimator;
		//protected Dictionary<string, AnimationMetaData> currentActiveAnimationsByAnimCode = new Dictionary<string, AnimationMetaData>();

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
			modsystem = api.ModLoader.GetModSystem<AnimatableCollectibleModsystem>();

			if (api.Side == EnumAppSide.Client)
			{
				if (!(collObj is Item))
				{
					throw new InvalidOperationException("CollectibleBehaviorAnimatable can only be used on Items, not Blocks!");
				}

				capi = api as ICoreClientAPI;

				InitAnimatable();

				capi.Event.RegisterItemstackRenderer(collObj, (inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize) => RenderHandFpPre(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize), EnumItemRenderTarget.HandFp);
				// Not yet vanilla :'(
				//capi.Event.RegisterItemstackRenderer(collObj, (inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize) => RenderHandTp(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate, showStackSize), EnumItemRenderTarget.HandTp);
			
				capi.ModLoader.GetModSystem<AnimatableCollectibleModsystem>().RegisterItemstackTpRenderer(collObj, (slot, renderInfo, itemModelMat, entity, isRight, stage) => RenderHandTpPre(slot, renderInfo, itemModelMat, entity, isRight, stage));
			}
		}

		public virtual void InitAnimatable()
		{
			Item item = (collObj as Item);

			curAtlas = capi.ItemTextureAtlas;

			AssetLocation loc = animatedShapePath != null ? new AssetLocation(animatedShapePath) : item.Shape.Base.Clone();
			loc = loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
			currentShape = capi.Assets.TryGet(loc)?.ToObject<Shape>();

			Vec3f rendererRot = new Vec3f(0f, 1f, 0f);

			MeshData meshData = InitializeMeshData(cacheKey, currentShape, this);
			//currentAnimator = InitializeAnimator(cacheKey, meshData, currentShape, rendererRot);
			currentMeshRef = InitializeMeshRef(meshData);
		}

		/*public void InitializeAnimator(string cacheDictKey, Vec3f rotation = null, Shape shape = null)
        {
            if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

			Item item = (collObj as Item);

            ITexPositionSource texSource = capi.Tesselator.GetTextureSource(item);
            MeshData meshdata;

            if (shape == null)
            {
                IAsset asset = capi.Assets.TryGet(item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                shape = asset.ToObject<Shape>();
            }

            shape.ResolveReferences(capi.World.Logger, cacheDictKey);
            CacheInvTransforms(shape.Elements);
            shape.ResolveAndLoadJoints();

            capi.Tesselator.TesselateShapeWithJointIds("entity", shape, out meshdata, texSource, null, item.Shape.QuantityElements, item.Shape.SelectiveElements);

            InitializeAnimator(cacheDictKey, rotation, shape, capi.Render.UploadMesh(meshdata));
        }*/

        /*public MeshData InitializeAnimator(string cacheDictKey, Shape shape, ITexPositionSource texSource, Vec3f rotation)
        {
            if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

            Item item = (collObj as Item);

            MeshData meshdata;

            if (shape == null)
            {
                IAsset asset = capi.Assets.TryGet(item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                shape = asset.ToObject<Shape>();
            }

            shape.ResolveReferences(capi.World.Logger, cacheDictKey);
            CacheInvTransforms(shape.Elements);
            shape.ResolveAndLoadJoints();

            capi.Tesselator.TesselateShapeWithJointIds("entity", shape, out meshdata, texSource, null, item.Shape.QuantityElements, item.Shape.SelectiveElements);

            if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

            InitializeAnimator(cacheDictKey, meshdata, shape, rotation);

            return meshdata;
        }*/

		// adapted from: public MeshData InitializeAnimator(string cacheDictKey, Shape shape, ITexPositionSource texSource, Vec3f rotation)
		public MeshData InitializeMeshData(string cacheDictKey, Shape shape, ITexPositionSource texSource)
        {
            if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

            Item item = (collObj as Item);

            MeshData meshdata;

            /*if (shape == null)
            {
                IAsset asset = capi.Assets.TryGet(item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                shape = asset.ToObject<Shape>();
            }*/

            shape.ResolveReferences(capi.World.Logger, cacheDictKey);
            CacheInvTransforms(shape.Elements);
            shape.ResolveAndLoadJoints();

            //capi.Tesselator.TesselateShapeWithJointIds("collectible", shape, out meshdata, texSource, null, item.Shape.QuantityElements, item.Shape.SelectiveElements);
			capi.Tesselator.TesselateShapeWithJointIds("collectible", shape, out meshdata, texSource, null);

            if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

            //InitializeAnimator(cacheDictKey, meshdata, shape, rotation);

            return meshdata;
        }

		//public void InitializeAnimator(string cacheDictKey, MeshData meshdata, Shape shape, Vec3f rotation) 
		public AnimatorBase InitializeAnimator(string cacheDictKey, MeshData meshdata, Shape shape, Vec3f rotation) 
        {
            if (meshdata == null)
            {
                throw new ArgumentException("meshdata cannot be null");
            }

            AnimatorBase animator = GetAnimator(capi, cacheDictKey, shape);
            
            /*if (RuntimeEnv.MainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId)
            {
                currentMeshRef = capi.Render.UploadMesh(meshdata);
            } else
            {
                capi.Event.EnqueueMainThreadTask(() => {
                    currentMeshRef = capi.Render.UploadMesh(meshdata);
                }, "uploadmesh");
            }*/

			return animator;
        }

		public MeshRef InitializeMeshRef(MeshData meshdata) 
        {            
			MeshRef meshRef = null;

            if (RuntimeEnv.MainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId)
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

        /*public void InitializeAnimator(string cacheDictKey, Vec3f rotation, Shape blockShape, MeshRef meshref)
        {
            if (capi.Side != EnumAppSide.Client) throw new NotImplementedException("Server side animation system not implemented yet.");

            animator = GetAnimator(capi, cacheDictKey, blockShape);

            //(api as ICoreClientAPI).Event.RegisterRenderer(this, EnumRenderStage.Opaque, "beanimutil");
            //renderer = new BEAnimatableRenderer(api as ICoreClientAPI, be.Pos, rotation, animator, activeAnimationsByAnimCode, meshref);
        }*/

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

		/*public AnimatableCollectibleEntityAnimator GetEntityAnimatorData(long entityId, EnumHand hand)
		{
			AnimatableCollectibleEntityAnimator entityAnimator = modsystem.GetEntityHandAnimatorData(entityId, hand);

			if (entityAnimator?.Collectible != collObj)
			{
				entityAnimator = CreateAnimatorData(entityId, hand);

				modsystem.SetEntityHandAnimatorData(entityId, hand, entityAnimator);

				entityAnimator.Animator.OnFrame(entityAnimator.ActiveAnimationsByAnimCode, 0);
			}

			return entityAnimator;
		}

		public virtual AnimatableCollectibleEntityAnimator CreateAnimatorData(long entityId, EnumHand hand)
		{
			return new AnimatableCollectibleEntityAnimator()
			{
				Collectible = collObj,
				Animator = GetAnimator(capi, cacheKey, currentShape)
			};
		}*/

		public CollectibleAnimatorData GetEntityAnimatorData(Entity entity, EnumHand hand)
		{
			AnimatableCollectibleAnimatonManager animatonManager = entity.AnimManager as AnimatableCollectibleAnimatonManager;

			CollectibleAnimatorData entityAnimator = hand == EnumHand.Right ? animatonManager.rightHandAnimatorData : animatonManager.leftHandAnimatorData;

			if (entityAnimator?.Collectible != collObj)
			{
				entityAnimator = CreateAnimatorData();

				animatonManager.SetCollectibleAnimatorData(entityAnimator, hand);

				entityAnimator.Animator.OnFrame(entityAnimator.ActiveAnimationsByAnimCode, 0);
			}

			return entityAnimator;
		}

		public virtual CollectibleAnimatorData CreateAnimatorData()
		{
			return new CollectibleAnimatorData()
			{
				Collectible = collObj,
				Animator = GetAnimator(capi, cacheKey, currentShape)
			};
		}

		public void StartAnimation(Entity entity, EnumHand hand, AnimationMetaData metaData)
        {
			if (capi == null)
			{
				metaData.Init();

				modsystem.SendAnimationStartPacket(entity, hand, metaData.CodeCrc32, metaData.AnimationSpeed, true);
			}
			else
			{
				CollectibleAnimatorData entityAnimator = GetEntityAnimatorData(entity, hand);

				if (!entityAnimator?.ActiveAnimationsByAnimCode.ContainsKey(metaData.Code) ?? false)
				{
					entityAnimator.ActiveAnimationsByAnimCode[metaData.Code] = metaData;
				}
			}
        }

        public void StopAnimation(Entity entity, EnumHand hand, string code, bool forceImmediate = false)
        {
			if (capi == null)
			{
				AnimatableCollectibleAnimatonManager animatonManager = entity.AnimManager as AnimatableCollectibleAnimatonManager;

				AnimationMetaData metaData = null;

				if (animatonManager?.leftHandAnimatorData?.ActiveAnimationsByAnimCode.TryGetValue(code, out metaData) ?? false)
				{
					modsystem.SendAnimationStopPacket(entity, hand, metaData.CodeCrc32, true);
				}
			}
			else
			{
				CollectibleAnimatorData entityAnimator = GetEntityAnimatorData(entity, hand);

				if (entityAnimator?.ActiveAnimationsByAnimCode.ContainsKey(code) ?? false)
				{
					entityAnimator.ActiveAnimationsByAnimCode.Remove(code);

					if (forceImmediate)
					{
						RunningAnimation anim = Array.Find(entityAnimator.Animator.anims, (anim) => {return anim.Animation.Code == code;});
						anim.EasingFactor = 0f;
					}
				}
			}
        }

		/*public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
			if (capi.IsGamePaused || target != EnumItemRenderTarget.HandFp) return; // We don't get entity here, so only do it for the FP target

			AnimatableCollectibleEntityAnimator entityAnimator = GetEntityAnimatorData(capi.World.Player.Entity.EntityId, renderinfo.InSlot is ItemSlotOffhand ? EnumHand.Left : EnumHand.Right);

            if (entityAnimator != null && entityAnimator.ActiveAnimationsByAnimCode.Count > 0 || entityAnimator.Animator.ActiveAnimationCount > 0)
            {
				entityAnimator.Animator.OnFrame(entityAnimator.ActiveAnimationsByAnimCode, renderinfo.dt);
            }
        }*/

		public virtual void RenderHandFpPre(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, double posX, double posY, double posZ, float size, int color, bool rotate = false, bool showStackSize = true)
        {
			IShaderProgram prevProg = capi.Render.CurrentActiveShader;
			IShaderProgram prog = null;

			Entity entity = capi.World.Player.Entity;

			CollectibleAnimatorData animatorData = GetEntityAnimatorData(entity, renderInfo.InSlot is ItemSlotOffhand ? EnumHand.Left : EnumHand.Right);

			//AnimatableCollectibleEntityAnimator entityAnimator = GetEntityAnimatorData(capi.World.Player.Entity.EntityId, renderInfo.InSlot is ItemSlotOffhand ? EnumHand.Left : EnumHand.Right);

			RenderHandFp(inSlot, renderInfo, modelMat, ref prog, prevProg, animatorData);

			prog?.Stop();
			prevProg?.Use();
		}

		public virtual void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, ref IShaderProgram prog, IShaderProgram prevProg, CollectibleAnimatorData entityAnimator)
        {
			if (onlyWhenAnimating && entityAnimator.ActiveAnimationsByAnimCode.Count == 0)
			{
				capi.Render.RenderMesh(renderInfo.ModelRef);
			}
			else
			{
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
				// Update to 1.17
				//Mat4f.RotateY(array, array, (float)capi.World.Player.CameraYaw);
				//Mat4f.RotateX(array, array, (float)capi.World.Player.CameraPitch);
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
				
				prog.UniformMatrices(
					"elementTransforms",
					GlobalConstants.MaxAnimatedElements,
					entityAnimator.Animator.Matrices
				);

				capi.Render.RenderMesh(currentMeshRef);
			}
        }

		public virtual void RenderHandTpPre(ItemSlot slot, ItemRenderInfo renderInfo, Matrixf itemModelMat, EntityAgent entity, bool isRight, EnumAnimatableCollectibleRenderStage stage)
        {
			IShaderProgram prevProg = capi.Render.CurrentActiveShader;
			IShaderProgram prog = null;

			CollectibleAnimatorData animatorData = GetEntityAnimatorData(entity, isRight ? EnumHand.Right : EnumHand.Left);

			//AnimatableCollectibleEntityAnimator entityAnimator = GetEntityAnimatorData(entity.EntityId, !isRight ? EnumHand.Left : EnumHand.Right);

			RenderHandTp(slot, renderInfo, itemModelMat, entity, isRight, stage, ref prog, prevProg, animatorData);

			prog?.Stop();
			prevProg?.Use();
		}

		public virtual void RenderHandTp(ItemSlot slot, ItemRenderInfo renderInfo, Matrixf itemModelMat, EntityAgent entity, bool isRight, EnumAnimatableCollectibleRenderStage stage, ref IShaderProgram prog, IShaderProgram prevProg, CollectibleAnimatorData entityAnimator)
        {
			/*if (stage == EnumAnimatableCollectibleRenderStage.Standard && (entityAnimator.ActiveAnimationsByAnimCode.Count > 0 || entityAnimator.Animator.ActiveAnimationCount > 0))
			{
				entityAnimator.Animator.OnFrame(entityAnimator.ActiveAnimationsByAnimCode, renderInfo.dt);
			}*/

			if (onlyWhenAnimating && entityAnimator.ActiveAnimationsByAnimCode.Count == 0)
			{
				capi.Render.RenderMesh(renderInfo.ModelRef);
			}
			else
			{
				IRenderAPI rapi = capi.Render;
				ItemStack stack = slot.Itemstack;

				if (stage == EnumAnimatableCollectibleRenderStage.Standard)
				{
					prevProg?.Stop();
					prog = modsystem.AnimatedStandardShaderProgram;

					prog.Use();
					prog.Uniform("dontWarpVertices", 0);
					prog.Uniform("addRenderFlags", 0);
					prog.Uniform("normalShaded", 1);
					prog.BindTexture2D("tex", renderInfo.TextureId, 0);
					prog.Uniform("rgbaTint", ColorUtil.WhiteArgbVec);
					prog.Uniform("alphaTest", renderInfo.AlphaTest);

					prog.Uniform("overlayOpacity", renderInfo.OverlayOpacity);
					if (renderInfo.OverlayTexture != null && renderInfo.OverlayOpacity > 0)
					{
						prog.Uniform("tex2dOverlay", renderInfo.OverlayTexture.TextureId);
						prog.BindTexture2D("tex2dOverlay", renderInfo.OverlayTexture.TextureId, 1);
						prog.Uniform("overlayTextureSize", new Vec2f(renderInfo.OverlayTexture.Width, renderInfo.OverlayTexture.Height));
						prog.Uniform("baseTextureSize", new Vec2f(renderInfo.TextureSize.Width, renderInfo.TextureSize.Height));
						TextureAtlasPosition texPos = rapi.GetTextureAtlasPosition(stack);
						prog.Uniform("baseUvOrigin", new Vec2f(texPos.x1, texPos.y1));
					}
					
					Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs((int)(entity.Pos.X + entity.SelectionBox.X1 - entity.OriginSelectionBox.X1), (int)entity.Pos.Y, (int)(entity.Pos.Z + entity.SelectionBox.Z1 - entity.OriginSelectionBox.Z1));
					int temp = (int)stack.Collectible.GetTemperature(capi.World, stack);
					float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(temp);
					lightrgbs[0] += glowColor[0];
					lightrgbs[1] += glowColor[1];
					lightrgbs[2] += glowColor[2];

					prog.Uniform("extraGlow", GameMath.Clamp((temp - 500) / 3, 0, 255));
					prog.Uniform("rgbaAmbientIn", rapi.AmbientColor);
					prog.Uniform("rgbaLightIn", lightrgbs);
					prog.Uniform("rgbaFogIn", rapi.FogColor);
					prog.Uniform("fogMinIn", rapi.FogMin);
					prog.Uniform("fogDensityIn", rapi.FogDensity);
					prog.Uniform("normalShaded", renderInfo.NormalShaded ? 1 : 0);

					prog.UniformMatrix("projectionMatrix", rapi.CurrentProjectionMatrix);
					prog.UniformMatrix("viewMatrix", rapi.CameraMatrixOriginf);
					prog.UniformMatrix("modelMatrix", itemModelMat.Values);
				}
				else
				{
					prevProg?.Stop();
					prog = rapi.GetEngineShader(EnumShaderProgram.Shadowmapentityanimated);
					prog.Use();

					Mat4f.Mul(tmpMvMat, capi.Render.CurrentModelviewMatrix, itemModelMat.Values);
                	capi.Render.CurrentActiveShader.UniformMatrix("modelViewMatrix", tmpMvMat);
					capi.Render.CurrentActiveShader.UniformMatrix("projectionMatrix", rapi.CurrentProjectionMatrix);
					capi.Render.CurrentActiveShader.BindTexture2D("entityTex", capi.ItemTextureAtlas.AtlasTextureIds[0], 0);
					capi.Render.CurrentActiveShader.Uniform("addRenderFlags", 0);
				}

				prog.UniformMatrices(
					"elementTransforms",
					GlobalConstants.MaxAnimatedElements,
					entityAnimator.Animator.Matrices
				);

				capi.Render.RenderMesh(currentMeshRef);
			}
		}
    }
}
