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

namespace AnimatableCollectibleSimple
{
    public class CollectibleBehaviorAnimatableSimpleWithAttach : CollectibleBehaviorAnimatableSimple
    {
        public CollectibleBehaviorAnimatableSimpleWithAttach(CollectibleObject collObj) : base(collObj)
        {
        }

		protected ItemRenderInfo AttachedRenderInfo;
		protected Matrixf AttachedMeshMat = new Matrixf();

		public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
        }

		public void SetAttachedRenderInfo(ItemRenderInfo renderInfo)
		{
			if (capi == null) return; // Client-only for now 

			AttachedRenderInfo = renderInfo;
		}

		public override void RenderHandFp(ItemSlot inSlot, ItemRenderInfo renderInfo, Matrixf modelMat, double posX, double posY, double posZ, float size, int color, bool rotate = false, bool showStackSize = true)
        {
			base.RenderHandFp(inSlot, renderInfo, modelMat, posX, posY, posZ, size, color, rotate = false, showStackSize = true);

			if ((!onlyWhenAnimating || ActiveAnimationsByAnimCode.Count > 0) && AttachedRenderInfo != null)
			{
				IShaderProgram prog = capi.Render.CurrentActiveShader;

				AttachmentPointAndPose apap = Animator.GetAttachmentPointPose("Arrow");
				AttachmentPoint ap = apap.AttachPoint;

				float originalArrowSize = 21f;
				float bowArrowSize = 15f;

				float arrowScale = bowArrowSize / originalArrowSize; // from 19 pixel long to 14 pixel long

				AttachedMeshMat = modelMat.Clone()
					.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
					.Mul(apap.AnimModelMatrix)
					.Translate(ap.PosX / 16f, ap.PosY / 16f, (ap.PosZ - bowArrowSize / 2f) * AttachedRenderInfo.Transform.ScaleXYZ.X / 16f)
					//.Translate(ap.PosX / 16f, ap.PosY / 16f, ap.PosZ / 16f)
					.Translate(renderInfo.Transform.Origin.X, renderInfo.Transform.Origin.Y, renderInfo.Transform.Origin.Z)
					.RotateX((float)(ap.RotationX) * GameMath.DEG2RAD)
					.RotateY((float)(ap.RotationY) * GameMath.DEG2RAD)
					.RotateZ((float)(ap.RotationZ) * GameMath.DEG2RAD)
					//.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
					.Scale(arrowScale * AttachedRenderInfo.Transform.ScaleXYZ.X, arrowScale * AttachedRenderInfo.Transform.ScaleXYZ.Y, arrowScale * AttachedRenderInfo.Transform.ScaleXYZ.Z)
					.Translate(-AttachedRenderInfo.Transform.Origin.X / 16f, -AttachedRenderInfo.Transform.Origin.Y / 16f, -AttachedRenderInfo.Transform.Origin.Z / 16f)
					.Translate(-renderInfo.Transform.Origin.X, -renderInfo.Transform.Origin.Y, -renderInfo.Transform.Origin.Z)
				;

				prog.UniformMatrix("modelViewMatrix", AttachedMeshMat.Values);

				capi.Render.RenderMesh(AttachedRenderInfo.ModelRef);
			}
        }
    }
}
