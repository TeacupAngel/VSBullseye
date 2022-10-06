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

namespace AnimatableCollectible
{
	public class CollectibleAnimatorDataWithAttach : CollectibleAnimatorData
	{ 
		public ItemRenderInfo AttachedRenderInfo;
	}

    public class CollectibleBehaviorAnimatableWithAttach : CollectibleBehaviorAnimatable
    {
        public CollectibleBehaviorAnimatableWithAttach(CollectibleObject collObj) : base(collObj)
        {
        }

		//protected ItemRenderInfo AttachedRenderInfo;
		protected Matrixf AttachedMeshMat = new Matrixf();

		public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
        }

		/*public virtual void InitAnimatable()
		{
			Item item = (collObj as Item);

			AssetLocation loc = animatedShapePath != null ? new AssetLocation(animatedShapePath) : item.Shape.Base.Clone();
			loc = loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
			Shape shape = capi.Assets.TryGet(loc)?.ToObject<Shape>();

			string key = "animatedCollectibleMeshes-" + item.Code.ToShortString();
			Vec3f rendererRot = new Vec3f(0f, 1f, 0f);

			MeshData meshData = InitializeMeshData(key, shape, capi.Tesselator.GetTextureSource(item));
			currentAnimator = InitializeAnimator(key, meshData, shape, rendererRot);
			currentMeshRef = InitializeMeshRef(meshData);
		}*/

		public void SetAttachedRenderInfo(Entity entity, EnumHand hand, MeshRef mesh, ItemRenderInfo renderInfo)
		{
			if (capi == null) return; // Client-only for now 

			CollectibleAnimatorDataWithAttach animatorWithAttach = GetEntityAnimatorData(entity, hand) as CollectibleAnimatorDataWithAttach;

			animatorWithAttach.AttachedRenderInfo = renderInfo;
		}

		public override CollectibleAnimatorData CreateAnimatorData()
		{
			return new CollectibleAnimatorDataWithAttach()
			{
				Collectible = collObj,
				Animator = GetAnimator(capi, cacheKey, currentShape)
			};
		}

		public override void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, ref IShaderProgram prog, IShaderProgram prevProg, CollectibleAnimatorData entityAnimator)
        {
			base.RenderHandFp(inSlot, renderInfo, modelMat, ref prog, prevProg, entityAnimator);

			CollectibleAnimatorDataWithAttach animatorWithAttach = entityAnimator as CollectibleAnimatorDataWithAttach;

			if ((!onlyWhenAnimating || entityAnimator.ActiveAnimationsByAnimCode.Count > 0) && animatorWithAttach != null && animatorWithAttach.AttachedRenderInfo != null)
			{
				IRenderAPI rpi = capi.Render;

				AttachmentPointAndPose apap = entityAnimator.Animator.GetAttachmentPointPose("Arrow");
				AttachmentPoint ap = apap.AttachPoint;

				float originalArrowSize = 21f;
				float bowArrowSize = 15f;

				float arrowScale = bowArrowSize / originalArrowSize; // from 19 pixel long to 14 pixel long

				AttachedMeshMat = modelMat.Clone()
					.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
					.Mul(apap.AnimModelMatrix)
					.Translate(ap.PosX / 16f, ap.PosY / 16f, (ap.PosZ - bowArrowSize / 2f) / 16f)
					//.Translate(ap.PosX / 16f, ap.PosY / 16f, ap.PosZ / 16f)
					.Translate(renderInfo.Transform.Origin.X, renderInfo.Transform.Origin.Y, renderInfo.Transform.Origin.Z)
					.RotateX((float)(ap.RotationX) * GameMath.DEG2RAD)
					.RotateY((float)(ap.RotationY) * GameMath.DEG2RAD)
					.RotateZ((float)(ap.RotationZ) * GameMath.DEG2RAD)
					//.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
					.Scale(arrowScale, arrowScale, arrowScale)
					.Translate(-animatorWithAttach.AttachedRenderInfo.Transform.Origin.X / 16f, -animatorWithAttach.AttachedRenderInfo.Transform.Origin.Y / 16f, -animatorWithAttach.AttachedRenderInfo.Transform.Origin.Z / 16f)
					.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
				;

				prog.UniformMatrix("modelViewMatrix", AttachedMeshMat.Values);

				capi.Render.RenderMesh(animatorWithAttach.AttachedRenderInfo.ModelRef);
			}
        }

		public override void RenderHandTp(ItemSlot slot, ItemRenderInfo renderInfo, Matrixf itemModelMat, EntityAgent entity, bool isRight, EnumAnimatableCollectibleRenderStage stage, ref IShaderProgram prog, IShaderProgram prevProg, CollectibleAnimatorData entityAnimator)
        {
			base.RenderHandTp(slot, renderInfo, itemModelMat, entity, isRight, stage, ref prog, prevProg, entityAnimator);

			CollectibleAnimatorDataWithAttach animatorWithAttach = entityAnimator as CollectibleAnimatorDataWithAttach;

			if ((!onlyWhenAnimating || entityAnimator.ActiveAnimationsByAnimCode.Count > 0) && animatorWithAttach.AttachedRenderInfo != null && animatorWithAttach.AttachedRenderInfo != null)
			{
				IRenderAPI rpi = capi.Render;

				AttachmentPointAndPose apap = entityAnimator.Animator.GetAttachmentPointPose("Arrow");
				AttachmentPoint ap = apap.AttachPoint;

				float originalArrowSize = 21f;
				float bowArrowSize = 15f;

				float arrowScale = bowArrowSize / originalArrowSize; // from 19 pixel long to 14 pixel long

				AttachedMeshMat = itemModelMat.Clone()
					.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
					.Mul(apap.AnimModelMatrix)
					.Translate(ap.PosX / 16f, ap.PosY / 16f, (ap.PosZ - bowArrowSize / 2f) / 16f)
					//.Translate(ap.PosX / 16f, ap.PosY / 16f, ap.PosZ / 16f)
					.Translate(renderInfo.Transform.Origin.X, renderInfo.Transform.Origin.Y, renderInfo.Transform.Origin.Z)
					.RotateX((float)(ap.RotationX) * GameMath.DEG2RAD)
					.RotateY((float)(ap.RotationY) * GameMath.DEG2RAD)
					.RotateZ((float)(ap.RotationZ) * GameMath.DEG2RAD)
					//.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
					.Scale(arrowScale, arrowScale, arrowScale)
					.Translate(-animatorWithAttach.AttachedRenderInfo.Transform.Origin.X / 16f, -animatorWithAttach.AttachedRenderInfo.Transform.Origin.Y / 16f, -animatorWithAttach.AttachedRenderInfo.Transform.Origin.Z / 16f)
					.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
				;

				if (stage == EnumAnimatableCollectibleRenderStage.Standard)
				{
					prog.UniformMatrix("modelMatrix", AttachedMeshMat.Values);

					capi.Render.RenderMesh(animatorWithAttach.AttachedRenderInfo.ModelRef);
				}
			}
        }
    }
}
