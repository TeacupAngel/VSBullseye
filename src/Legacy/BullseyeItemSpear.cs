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
	[Obsolete("Use BullseyeCollectibleBehaviorSpear instead")]
	public class BullseyeItemSpear : BullseyeItemRangedWeapon
	{
		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);

			WeaponStats.weaponType = BullseyeRangedWeaponType.Throw;
		}

		public override void OnAimingStart(ItemSlot slot, EntityAgent byEntity)
		{
			byEntity.StartAnimation("aim");
		}

		public override void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
		{
			if (byEntity.World is IClientWorldAccessor)
			{
				ModelTransform tf = new ModelTransform();
				tf.EnsureDefaultValues();

				float offset = GameMath.Serp(0, 2, GameMath.Clamp(secondsUsed * 4f, 0, 2f) / 2f);

				tf.Translation.Set(0, offset / 5, offset / 3);
				tf.Rotation.Set(offset * 10, 0, 0);
				byEntity.Controls.UsingHeldItemTransformAfter = tf;
			}
		}

		public override void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) 
		{
			byEntity?.StopAnimation("aim");
		}

		public override List<ItemStack> GetAvailableAmmoTypes(ItemSlot slot, IClientPlayer forPlayer)
		{
			return null;
		}

		public override ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot, bool isStartCheck = false)
		{
			return weaponSlot;
		}

		public override float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			float damage = weaponSlot.Itemstack?.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;
			damage *= ConfigSystem?.GetSyncedConfig()?.SpearDamage ?? 1f;

			return damage;
		}

		public override float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return 0.3f;
		}

		public override int GetProjectileDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return 3;
		}

		public override EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			// Accept either Bullseye entityCode, or vanilla spearEntityCode
			string entityCode = Attributes["entityCode"].AsString() ?? Attributes["spearEntityCode"].AsString();

			return byEntity.World.GetEntityType(new AssetLocation(entityCode));
		}

		public override void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity) 
		{
			if (byEntity is EntityPlayer) RefillSlotIfEmpty(slot, byEntity, (itemstack) => itemstack.Collectible is BullseyeItemSpear);

			byEntity.StopAnimation("aim");

			(api as ICoreClientAPI)?.World.AddCameraShake(0.17f);

			IPlayer byPlayer = null;
			if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
			byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

			byEntity.StartAnimation("throw");

			float pitch = (byEntity as EntityPlayer)?.talkUtil.pitchModifier ?? 1f;
            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, pitch * 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);
		}

		// Spear melee specific
		public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
		{
			base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);

			byEntity.Attributes.SetInt("didattack", 0);

			byEntity.World.RegisterCallback((dt) =>
			{
				IPlayer byPlayer = (byEntity as EntityPlayer).Player;
				if (byPlayer == null) return;

				if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemAttack)
				{
					float pitch = (byEntity as EntityPlayer).talkUtil.pitchModifier;
					byPlayer.Entity.World.PlaySoundAt(new AssetLocation("sounds/player/strike"), byPlayer.Entity, byPlayer, pitch * 0.9f + (float)api.World.Rand.NextDouble() * 0.2f, 16, 0.35f);
				}
			}, 464);

			handling = EnumHandHandling.PreventDefault;
		}

		public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
		{
			return base.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSelection, entitySel, cancelReason);
		}

		public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
		{
			base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSelection, entitySel);

			float backwards = -Math.Min(0.8f, 3 * secondsPassed);
			float stab = Math.Min(1.2f, 20 * Math.Max(0, secondsPassed - 0.25f));

			if (byEntity.World.Side == EnumAppSide.Client)
			{
				IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
				ModelTransform tf = new ModelTransform();
				tf.EnsureDefaultValues();

			  
				float sum = stab + backwards;
				float ztranslation = Math.Min(0.2f, 1.5f * secondsPassed);
				float easeout = Math.Max(0, 2 * (secondsPassed - 1));

				if (secondsPassed > 0.4f) sum = Math.Max(0, sum - easeout);
				ztranslation = Math.Max(0, ztranslation - easeout);

				tf.Translation.Set(-0.5f * sum, ztranslation * 0.4f, -sum * 0.8f * 2.6f);
				tf.Rotation.Set(-sum * 9, sum * 10, -sum*10);

				byEntity.Controls.UsingHeldItemTransformAfter = tf;
				

				if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0)
				{
					world.TryAttackEntity(entitySel);
					byEntity.Attributes.SetInt("didattack", 1);
					world.AddCameraShake(0.25f);
				}
			} 
			else
			{
				if (stab > 1.15f && byEntity.Attributes.GetInt("didattack") == 0 && entitySel != null)
				{
					byEntity.Attributes.SetInt("didattack", 1);

					bool canhackEntity =
						entitySel.Entity.Properties.Attributes?["hackedEntity"].Exists == true
						&& slot.Itemstack.ItemAttributes.IsTrue("hacking") == true && api.ModLoader.GetModSystem<CharacterSystem>().HasTrait((byEntity as EntityPlayer).Player, "technical")
					;
					ICoreServerAPI sapi = api as ICoreServerAPI;

					if (canhackEntity)
					{
						sapi.World.PlaySoundAt(new AssetLocation("sounds/player/hackingspearhit.ogg"), entitySel.Entity, null);
					}

					if (api.World.Rand.NextDouble() < 0.15 && canhackEntity)
					{
						SpawnEntityInPlaceOf(entitySel.Entity, entitySel.Entity.Properties.Attributes["hackedEntity"].AsString(), byEntity);
						sapi.World.DespawnEntity(entitySel.Entity, new EntityDespawnData() { Reason = EnumDespawnReason.Removed });
					}
				}
			}

			return secondsPassed < 1.2f;
		}

		private void SpawnEntityInPlaceOf(Entity byEntity, string code, EntityAgent causingEntity)
		{
			AssetLocation location = AssetLocation.Create(code, byEntity.Code.Domain);
			EntityProperties type = byEntity.World.GetEntityType(location);
			if (type == null)
			{
				byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
				if (api.World.Side == EnumAppSide.Client)
				{
					(api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", string.Format("No such entity loaded - '{0}'.", location));
				}
				return;
			}

			Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

			if (entity != null)
			{
				entity.ServerPos.X = byEntity.ServerPos.X;
				entity.ServerPos.Y = byEntity.ServerPos.Y;
				entity.ServerPos.Z = byEntity.ServerPos.Z;
				entity.ServerPos.Motion.X = byEntity.ServerPos.Motion.X;
				entity.ServerPos.Motion.Y = byEntity.ServerPos.Motion.Y;
				entity.ServerPos.Motion.Z = byEntity.ServerPos.Motion.Z;
				entity.ServerPos.Yaw = byEntity.ServerPos.Yaw;

				entity.Pos.SetFrom(entity.ServerPos);
				entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

				entity.Attributes.SetString("origin", "playerplaced");
				
				entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
				if (causingEntity is EntityPlayer eplr)
				{
					entity.WatchedAttributes.SetString("guardedPlayerUid", eplr.PlayerUID);
				}

				byEntity.World.SpawnEntity(entity);
			}
		}

		public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
		{
			base.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSelection, entitySel);
		}

		public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
		{
			base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
			if (inSlot.Itemstack.Collectible.Attributes == null) return;

			float damage = 0f;

			if (inSlot.Itemstack.Collectible.Attributes != null)
			{
				damage = inSlot.Itemstack.Collectible.Attributes["damage"].AsFloat(0) * ConfigSystem?.GetSyncedConfig()?.SpearDamage ?? 1f;
			}

			dsc.AppendLine(damage + Lang.Get("piercing-damage-thrown"));
		}
	}
}
