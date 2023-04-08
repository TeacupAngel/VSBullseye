using System;
using System.Text;
using System.Collections.Generic;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Bullseye
{
	public class BullseyeCollectibleBehaviorThrowableStone : BullseyeCollectibleBehaviorThrowable
	{
		public BullseyeCollectibleBehaviorThrowableStone(CollectibleObject collObj) : base(collObj) {}

		protected override bool CanStartAiming(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if (byEntity.Controls.ShiftKey) return false;

			return base.CanStartAiming(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
		}
	}
}
