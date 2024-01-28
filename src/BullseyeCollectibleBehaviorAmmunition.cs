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
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using Newtonsoft.Json.Linq;

namespace Bullseye
{  
	public class BullseyeCollectibleBehaviorAmmunition : CollectibleBehavior
	{
		public BullseyeCollectibleBehaviorAmmunition(CollectibleObject collObj) : base(collObj) {}

		public virtual float GetDamage(ItemSlot inSlot, string weaponType, IWorldAccessor world)
		{
			if (!string.IsNullOrEmpty(weaponType) && inSlot.Itemstack.ItemAttributes != null)
			{
				JsonObject damageObject = inSlot.Itemstack.ItemAttributes["ammoTypes"][weaponType]["damage"];

				if (damageObject.Exists)
				{
					return damageObject.AsFloat(0f);
				}
			}

			return inSlot.Itemstack.ItemAttributes?["damage"].AsFloat(0) ?? 0f;
		}

		public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

			// ItemSlotAmmo displays data with relevant weapon and class bonuses applied, so we don't have to
			if (inSlot is BullseyeItemSlotAmmo) return;
			if (inSlot.Itemstack.ItemAttributes == null) return;

			float damage;

			if (inSlot.Itemstack.ItemAttributes["ammoTypes"].Exists && inSlot.Itemstack.ItemAttributes["ammoTypes"].Token is JObject ammoTypeJObject)
			{
				foreach (var ammoTypeToken in ammoTypeJObject)
				{
					string ammoType = ammoTypeToken.Key;
					JsonObject ammoTypeObject = inSlot.Itemstack.ItemAttributes["ammoTypes"][ammoType];
					
					if (!inSlot.Itemstack.ItemAttributes["ammoTypes"][ammoType]["langCode"].Exists) continue;

					damage = inSlot.Itemstack.ItemAttributes["ammoTypes"][ammoType]["damage"].AsFloat(0f);
					if (damage != 0) dsc.AppendLine(Lang.Get(inSlot.Itemstack.ItemAttributes["ammoTypes"][ammoType]["langCode"].AsString(), damage));
				}
			}

			damage = GetDamage(inSlot, null, world);
			string damageString = inSlot.Itemstack.ItemAttributes["damageLangCode"].AsString();
			if (damage != 0 && damageString != null) dsc.AppendLine(Lang.Get(damageString, damage));

			float averageLifetimeDamage = 0f;
			float breakChance = 0f;

			if (inSlot.Itemstack.ItemAttributes.KeyExists("averageLifetimeDamage"))
			{
				averageLifetimeDamage = inSlot.Itemstack.ItemAttributes["averageLifetimeDamage"].AsFloat();
				breakChance = damage / averageLifetimeDamage;
			}
			else
			{
				breakChance = inSlot.Itemstack.ItemAttributes["breakChanceOnImpact"].AsFloat(0);
			}

			if (breakChance == 0f) return;
			if (breakChance >= 1f)
			{
				dsc.AppendLine(Lang.Get("bullseye:projectile-always-breaks"));
				return;
			}

			dsc.AppendLine(Lang.Get("bullseye:projectile-break-chance", breakChance * 100f));

			// Disabled until I can find a way to make this not confusing for players
			/*if (averageLifetimeDamage > 0)
			{
				dsc.AppendLine($"({Lang.Get("bullseye:lifetime-projectile-damage", averageLifetimeDamage)})");
			}*/
		}
	}
}
