using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

using Vintagestory.GameContent;

using Vintagestory.Client.NoObf;

namespace AnimatableCollectible
{
    public class CollectibleBehaviorMultiAnimatable : CollectibleBehaviorAnimatable
    {
        public CollectibleBehaviorMultiAnimatable(CollectibleObject collObj) : base(collObj)
        {
        }

		public class ShapeAnimationData
		{
			public Shape Shape;
			public MeshRef MeshRef;

			public AnimatorBase Animator;

			public Dictionary<string, AnimationMetaData> ActiveAnimationsByAnimCode = new Dictionary<string, AnimationMetaData>();
		}

		Dictionary<string, ShapeAnimationData> shapeAnimationDataByCode = new Dictionary<string, ShapeAnimationData>();

		public override void Initialize(JsonObject properties)
        {
            //animatedShapePath = properties["animatedShape"].AsString(null);
            //onlyWhenAnimating = properties["onlyWhenAnimating"].AsBool(true);

            base.Initialize(properties);
        }

		public override void InitAnimatable()
		{
		}

		public void AddShape(string shapeName, Shape shape)
		{
			Item item = (collObj as Item);

			string key = $"animatedCollectibleMeshes-{item.Code.ToShortString()}-{shapeName}";
			Vec3f rendererRot = new Vec3f(0f, 1f, 0f);

			MeshData meshData = InitializeMeshData(key, shape, capi.Tesselator.GetTextureSource(item));
			AnimatorBase animator = InitializeAnimator(key, meshData, shape, rendererRot);
			MeshRef meshRef = InitializeMeshRef(meshData);

			ShapeAnimationData shapeAnimationData = new ShapeAnimationData()
			{
				Shape = shape,
				MeshRef = meshRef,
				Animator = animator
			};

			shapeAnimationDataByCode[shapeName] = shapeAnimationData;
		}

		public bool SelectShape(string shapeName)
		{
			ShapeAnimationData shapeAnimationData;

			if (shapeAnimationDataByCode.TryGetValue(shapeName, out shapeAnimationData))
			{
				currentShape = shapeAnimationData.Shape;
				currentMeshRef = shapeAnimationData.MeshRef;
				//currentAnimator = shapeAnimationData.Animator;
			}

			return false;
		}
    }
}
