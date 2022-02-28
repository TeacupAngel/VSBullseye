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

namespace Bullseye
{
    public class CollectibleBehaviorStoneDescription : CollectibleBehavior
    {
		// CollectibleBehaviorAnimatable
        public CollectibleBehaviorStoneDescription(CollectibleObject collObj) : base(collObj)
        {
        }

		public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			float dmg = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0);
            if (dmg != 0) dsc.AppendLine(Lang.Get("bullseye:damage-with-sling", dmg));
		}
    }
}
