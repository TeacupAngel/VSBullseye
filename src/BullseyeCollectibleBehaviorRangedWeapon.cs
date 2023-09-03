using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

using System.Globalization;

namespace Bullseye
{
	public class BullseyeCollectibleBehaviorRangedWeapon : CollectibleBehavior
	{
		protected ICoreAPI api;
		
		protected BullseyeSystemClientAiming CoreClientSystem {get; private set;}
		protected BullseyeSystemCoreServer CoreServerSystem {get; private set;}
		protected BullseyeSystemConfig ConfigSystem {get; private set;}
		protected BullseyeSystemRangedWeapon RangedWeaponSystem {get; private set;}
		protected BullseyeRangedWeaponStats WeaponStats {get; private set;}

		protected ModelTransform DefaultFpHandTransform {get; private set;}

		protected LoadedTexture AimTexPartCharge;
		protected LoadedTexture AimTexFullCharge;
		protected LoadedTexture AimTexBlocked;

		private WorldInteraction[] interactions = Array.Empty<WorldInteraction>();

		public string AmmoType => WeaponStats.ammoType;

		public BullseyeCollectibleBehaviorRangedWeapon(CollectibleObject collObj) : base(collObj) {}

		public override void OnLoaded(ICoreAPI api)
		{
			base.OnLoaded(api);

			this.api = api;

			ConfigSystem = api.ModLoader.GetModSystem<BullseyeSystemConfig>();
			RangedWeaponSystem = api.ModLoader.GetModSystem<BullseyeSystemRangedWeapon>();

			DefaultFpHandTransform = collObj.FpHandTransform.Clone();

			WeaponStats = collObj.Attributes.KeyExists("bullseyeWeaponStats") ? collObj.Attributes?["bullseyeWeaponStats"].AsObject<BullseyeRangedWeaponStats>() : new BullseyeRangedWeaponStats();

			if (api.Side == EnumAppSide.Server)
			{
				CoreServerSystem = api.ModLoader.GetModSystem<BullseyeSystemCoreServer>();

				api.Event.RegisterEventBusListener(ServerHandleFire, 0.5, "bullseyeRangedWeaponFire");
			}
			else
			{
				CoreClientSystem = api.ModLoader.GetModSystem<BullseyeSystemClientAiming>();

				ICoreClientAPI capi = api as ICoreClientAPI;

				PrepareHeldInteractionHelp();

				AimTexPartCharge = new LoadedTexture(capi);
				AimTexFullCharge = new LoadedTexture(capi);
				AimTexBlocked = new LoadedTexture(capi);

				if (WeaponStats.aimTexPartChargePath != null) capi.Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexPartChargePath), ref AimTexPartCharge);
				if (WeaponStats.aimTexFullChargePath != null) capi.Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexFullChargePath), ref AimTexFullCharge);
				if (WeaponStats.aimTexBlockedPath != null) capi.Render.GetOrLoadTexture(new AssetLocation(WeaponStats.aimTexBlockedPath), ref AimTexBlocked);
			}
		}

		// Not available to CollectibleBehavior
		/*public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
		{
			return null;
		}*/

		public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
		{
			base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

			if (target == EnumItemRenderTarget.HandFp)
			{
				float transformFraction;

				if (!RangedWeaponSystem.HasEntityCooldownPassed(capi.World.Player.Entity.EntityId, WeaponStats.cooldownTime))
				{
					float cooldownRemaining = WeaponStats.cooldownTime - RangedWeaponSystem.GetEntityCooldownTime(capi.World.Player.Entity.EntityId);
					float transformTime = 0.25f;

					transformFraction = WeaponStats.weaponType != BullseyeRangedWeaponType.Throw ? 
						GameMath.Clamp((WeaponStats.cooldownTime - cooldownRemaining) / transformTime, 0f, 1f) : 1f;
					transformFraction -= GameMath.Clamp((transformTime - cooldownRemaining) / transformTime, 0f, 1f);
				}
				else
				{
					transformFraction = 0;
				}

				renderinfo.Transform.Translation.Y = DefaultFpHandTransform.Translation.Y - (float)(transformFraction * 1.5);

				if (CoreClientSystem.Aiming)
				{
					SetAimTransform(capi, renderinfo.Transform);
				}
				else
				{
					renderinfo.Transform.Rotation.Set(DefaultFpHandTransform.Rotation);
				}
			}
		}

		protected virtual bool CanStartAiming(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if (byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey) return false;

			if (!RangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, WeaponStats.cooldownTime))
			{
				handHandling = EnumHandHandling.PreventDefault;
				handling = EnumHandling.PreventDefault;
				return false;
			}

			return true;
		}

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
		{
			if (handHandling == EnumHandHandling.PreventDefault || handling == EnumHandling.PreventDefault) return;
			if (!CanStartAiming(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling)) return;

			ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, slot, true);
			if (ammoSlot == null) return;

			EntityProperties projectileEntity = GetProjectileEntityType(byEntity, slot, ammoSlot);
			if (projectileEntity == null) return;

			if (byEntity.World is IClientWorldAccessor)
			{
				CoreClientSystem.SetRangedWeaponStats(WeaponStats);
				CoreClientSystem.SetReticleTextures(AimTexPartCharge, AimTexFullCharge, AimTexBlocked);
			}

			RangedWeaponSystem.SetLastEntityRangedChargeData(byEntity.EntityId, slot);

			byEntity.GetBehavior<BullseyeEntityBehaviorAimingAccuracy>().SetRangedWeaponStats(WeaponStats);

			// Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
			byEntity.Attributes.SetInt("bullseyeAiming", 1);
			byEntity.Attributes.SetInt("bullseyeAimingCancel", 0);

			if (!WeaponStats.allowSprint) 
			{
				byEntity.Controls.Sprint = false;
				byEntity.ServerControls.Sprint = false;
			}

			OnAimingStart(slot, byEntity);
			handHandling = EnumHandHandling.PreventDefault;
			handling = EnumHandling.PreventDefault;
		}

		public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
		{
			if (!RangedWeaponSystem.HasEntityCooldownPassed(byEntity.EntityId, WeaponStats.cooldownTime))
			{
				handling = EnumHandling.PreventSubsequent;
				return false;
			}

			if (byEntity.Attributes.GetInt("bullseyeAiming") == 1)
			{
				OnAimingStep(secondsUsed, slot, byEntity);

				if (byEntity.World is IClientWorldAccessor)
				{
					// Show different reticle if we are ready to shoot
					// - Show white "full charge" reticle if the accuracy is fully calmed down, + a little leeway to let the reticle calm down fully
					// - Show yellow "partial charge" reticle if the bow is ready for a snap shot, but accuracy is still poor
					// --- OR if the weapon was held so long that accuracy is starting to get bad again, for weapons that have it
					// - Show red "blocked" reticle if the weapon can't shoot yet
					bool showBlocked = secondsUsed < GetEntityChargeTime(byEntity);
					bool showPartCharged = secondsUsed < WeaponStats.accuracyStartTime / byEntity.Stats.GetBlended("rangedWeaponsSpeed") + WeaponStats.aimFullChargeLeeway;
					showPartCharged = showPartCharged || secondsUsed > WeaponStats.accuracyOvertimeStart + WeaponStats.accuracyStartTime && WeaponStats.accuracyOvertime > 0;

					CoreClientSystem.WeaponReadiness = showBlocked ? BullseyeEnumWeaponReadiness.Blocked : 
														showPartCharged ? BullseyeEnumWeaponReadiness.PartCharge : BullseyeEnumWeaponReadiness.FullCharge;
				}

				handling = EnumHandling.PreventDefault;
			}

			return true;
		}

		public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
		{
			if (byEntity.Attributes.GetInt("bullseyeAimingCancel") == 1) return true;

			byEntity.Attributes.SetInt("bullseyeAiming", 0);

			if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
			{
				byEntity.Attributes.SetInt("bullseyeAimingCancel", 1);
			}

			OnAimingCancel(secondsUsed, slot, byEntity, cancelReason);

			handled = EnumHandling.PreventDefault;

			return true;
		}

		public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
		{
			if (byEntity.Attributes.GetInt("bullseyeAimingCancel") == 1) 
			{
				handling = EnumHandling.PreventDefault;
				return;
			}
			byEntity.Attributes.SetInt("bullseyeAiming", 0);

			EntityPlayer entityPlayer = byEntity as EntityPlayer;

			if (!byEntity.Alive || secondsUsed < GetChargeNeeded(api, byEntity))
			{
				OnAimingCancel(secondsUsed, slot, byEntity, !byEntity.Alive ? EnumItemUseCancelReason.Death : EnumItemUseCancelReason.ReleasedMouse);
				handling = EnumHandling.PreventDefault;
				return;
			}

			if (api.Side == EnumAppSide.Server) 
			{
				// Just to make sure animations etc. get stopped if a shot looks legit on the server but was stopped on the client
				api.Event.RegisterCallback((ms) => 
				{
					if (byEntity.Attributes.GetInt("bullseyeAiming") == 0)
				 	{ 
						OnAimingCancel(secondsUsed, slot, byEntity, !byEntity.Alive ? EnumItemUseCancelReason.Death : EnumItemUseCancelReason.ReleasedMouse);
					}
				}, 500);
			}
			else
			{
				Vec3d targetVec = CoreClientSystem.TargetVec;

				Shoot(slot, byEntity, targetVec);

				RangedWeaponSystem.SendRangedWeaponFirePacket(collObj.Id, targetVec);
			}

			handling = EnumHandling.PreventDefault;
		}

		public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
		{
			return interactions;
		}

		public override void OnUnloaded(ICoreAPI api)
		{
			base.OnUnloaded(api);

			AimTexFullCharge?.Dispose();
			AimTexPartCharge?.Dispose();
			AimTexBlocked?.Dispose();
		}

		protected virtual void SetAimTransform(ICoreClientAPI capi, ModelTransform transform)
		{
			Vec2f currentAim = CoreClientSystem.GetCurrentAim();

			transform.Rotation.X = DefaultFpHandTransform.Rotation.X - (currentAim.Y / 15f); 
			transform.Rotation.Y = DefaultFpHandTransform.Rotation.Y - (currentAim.X / 15f);
		}

		public virtual bool CanUseAmmoSlot(ItemSlot checkedSlot)
		{
			if (checkedSlot.Itemstack.ItemAttributes?["ammoTypes"]?[AmmoType]?.Exists ?? false) return true;

			return AmmoType == checkedSlot.Itemstack.ItemAttributes?["ammoType"].AsString();
		}

		public virtual List<ItemStack> GetAvailableAmmoTypes(ItemSlot slot, IClientPlayer forPlayer)
		{
			if (AmmoType == null)
			{
				return null;
			}

			List<ItemStack> ammoTypes = new List<ItemStack>();

			forPlayer.Entity.WalkInventory((invslot) =>
			{
				if (invslot is ItemSlotCreative) return true;

				if (invslot.Itemstack != null && CanUseAmmoSlot(invslot))
				{
					ItemStack ammoStack = ammoTypes.Find(itemstack => itemstack.Equals(api.World, invslot.Itemstack, GlobalConstants.IgnoredStackAttributes));

					if (ammoStack == null)
					{
						ammoStack = invslot.Itemstack.GetEmptyClone();
						ammoStack.StackSize = invslot.StackSize;
						ammoTypes.Add(ammoStack);
					}
					else
					{
						ammoStack.StackSize += invslot.StackSize;
					}
				}

				return true;
			});

			if (ammoTypes.Count <= 0)
			{
				return null;
			}

			ammoTypes.Sort((ItemStack X, ItemStack Y) => {
				float xDamage = X.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;
				float yDamage = Y.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0f;

				// Ascending sort by damage, or by name if damage is equal
				return (xDamage - yDamage) switch
				{
					> 0 => 1,
					< 0 => -1,
					_ => String.Compare(X.GetName(), Y.GetName())
				};
			});

			return ammoTypes;
		}

		public virtual ItemStack GetEntitySelectedAmmoType(EntityAgent entity)
		{
			if (AmmoType == null)
			{
				return null;
			}

			ITreeAttribute treeAttribute = entity.Attributes.GetTreeAttribute("bullseyeSelectedAmmo");

			ItemStack resultItemstack = treeAttribute?.GetItemstack(AmmoType, null);
			resultItemstack?.ResolveBlockOrItem(api.World);

			return resultItemstack;
		}

		public virtual void OnAimingStart(ItemSlot slot, EntityAgent byEntity) {}
		public virtual void OnAimingStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity) {}
		public virtual void OnAimingCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, EnumItemUseCancelReason cancelReason) {}
		public virtual void OnShot(ItemSlot slot, Entity projectileEntity, EntityAgent byEntity) {}

		public virtual ItemSlot GetNextAmmoSlot(EntityAgent byEntity, ItemSlot weaponSlot, bool isStartCheck = false)
		{
			if (AmmoType == null || byEntity == null || weaponSlot.Itemstack == null) return null;

			ItemSlot ammoSlot = null;
			ItemStack selectedAmmoType = GetEntitySelectedAmmoType(byEntity);

			byEntity.WalkInventory((invslot) =>
			{
				if (invslot == null || invslot is ItemSlotCreative) return true;

				if (invslot.Itemstack != null && CanUseAmmoSlot(invslot))
				{
					// If we found the selected ammo type or no ammo type is specifically selected, return the first one we find
					if (selectedAmmoType == null || invslot.Itemstack.Equals(api.World, selectedAmmoType, GlobalConstants.IgnoredStackAttributes))
					{
						ammoSlot = invslot;
						return false;
					}

					// Otherwise just get the first ammo stack we find
					if (ammoSlot == null)
					{
						ammoSlot = invslot;
					}
				}

				return true;
			});

			return ammoSlot;
		}

		public virtual float GetProjectileDamage(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			float damage = 0f;

			if (ammoSlot?.Itemstack?.Collectible != null) 
			{
				BullseyeCollectibleBehaviorAmmunition cbAmmunition = ammoSlot.Itemstack.Collectible.GetCollectibleBehavior<BullseyeCollectibleBehaviorAmmunition>(true);
				damage = cbAmmunition != null ? cbAmmunition.GetDamage(ammoSlot, WeaponStats.ammoType, byEntity.World) : ammoSlot.Itemstack.ItemAttributes?["damage"].AsFloat(0) ?? 0f;
			}

			// Weapon modifiers
			damage *= (1f + weaponSlot.Itemstack?.Collectible?.Attributes?["damagePercent"].AsFloat(0) ?? 0f);
			damage += weaponSlot.Itemstack?.Collectible?.Attributes?["damage"].AsFloat(0) ?? 0;
			damage *= byEntity.Stats.GetBlended("rangedWeaponsDamage");

			return damage;
		}

		public virtual float GetProjectileVelocity(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return (WeaponStats.projectileVelocity + (ammoSlot.Itemstack?.ItemAttributes?["velocityModifier"].AsFloat(0f) ?? 0f)) * byEntity.Stats.GetBlended("bowDrawingStrength");
		}

		public virtual float GetProjectileSpread(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			return WeaponStats.projectileSpread + (ammoSlot.Itemstack?.ItemAttributes?["spreadModifier"].AsFloat(0f) ?? 0f);
		}

		public virtual float GetProjectileDropChance(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0f;
		public virtual float GetProjectileWeight(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0.1f;
		public virtual int GetProjectileDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0;

		public virtual EntityProperties GetProjectileEntityType(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot)
		{
			throw new NotImplementedException($"[Bullseye] Ranged weapon CollectibleBehavior of {collObj.Code} has no implementation for GetProjectileEntityType()!");
		}

		public virtual int GetWeaponDurabilityCost(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot) => 0;

		protected float GetChargeNeeded(ICoreAPI api, EntityAgent entity)
		{
			// slightly longer charge on client, for safety in case of desync
			return api.Side == EnumAppSide.Server ? GetEntityChargeTime(entity) : GetEntityChargeTime(entity) + 0.1f;
		}

		public void Shoot(ItemSlot weaponSlot, EntityAgent byEntity, Vec3d targetVec)
		{
			byEntity.Attributes.SetInt("bullseyeAiming", 0);

			ItemSlot ammoSlot = GetNextAmmoSlot(byEntity, weaponSlot);
			if (ammoSlot == null) return;

			EntityProperties projectileEntityType = GetProjectileEntityType(byEntity, weaponSlot, ammoSlot);
			if (projectileEntityType == null) return;

			float damage = GetProjectileDamage(byEntity, weaponSlot, ammoSlot);
			float speed = GetProjectileVelocity(byEntity, weaponSlot, ammoSlot);
			float spread = GetProjectileSpread(byEntity, weaponSlot, ammoSlot);
			float dropChance = GetProjectileDropChance(byEntity, weaponSlot, ammoSlot);
			float weight = GetProjectileWeight(byEntity, weaponSlot, ammoSlot);

			int ammoDurabilityCost = GetProjectileDurabilityCost(byEntity, weaponSlot, ammoSlot);
			int weaponDurabilityCost = GetWeaponDurabilityCost(byEntity, weaponSlot, ammoSlot);

			// If we need to damage the projectile by more than 1 durability per shot, do it here, but leave at least 1 durability
			ammoDurabilityCost = DamageProjectile(byEntity, weaponSlot, ammoSlot, ammoDurabilityCost);

			Vec3d velocity = GetVelocityVector(byEntity, targetVec, speed, spread);

			ItemStack ammoStack = ammoSlot.TakeOut(1);
			ammoSlot.MarkDirty();

			Entity projectileEntity = CreateProjectileEntity(byEntity, projectileEntityType, ammoStack, damage, dropChance, weight, ammoDurabilityCost);
			if (projectileEntity == null)
			{
				api.Logger.Error($"[Bullseye] Ranged weapon {collObj.Code} tried to shoot, but failed to create the projectile entity!");
				return;
			}

			// Used in vanilla spears but feels awful, might redo later with proper offset to the right
			//projectileEntity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0));
			projectileEntity.ServerPos.SetPos(byEntity.SidedPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
			projectileEntity.ServerPos.Motion.Set(velocity);

			projectileEntity.Pos.SetFrom(projectileEntity.ServerPos);
			projectileEntity.World = byEntity.World;

			FinalizeProjectileEntity(projectileEntity, byEntity);

			byEntity.World.SpawnEntity(projectileEntity);

			RangedWeaponSystem.StartEntityCooldown(byEntity.EntityId);

			OnShot(weaponSlot, projectileEntity, byEntity);

			if (weaponDurabilityCost > 0)
			{
				weaponSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, weaponSlot, weaponDurabilityCost);
			}
		}

		protected int DamageProjectile(EntityAgent byEntity, ItemSlot weaponSlot, ItemSlot ammoSlot, int ammoDurabilityCost)
		{
			if (GetProjectileDurabilityCost(byEntity, weaponSlot, ammoSlot) > 1)
			{
				int durability = weaponSlot.Itemstack.Attributes.GetInt("durability", collObj.Durability);

				ammoDurabilityCost = ammoDurabilityCost >= durability ? durability : ammoDurabilityCost;

				weaponSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, weaponSlot, ammoDurabilityCost - 1);
			}

			return ammoDurabilityCost;
		}

		protected Vec3d GetVelocityVector(EntityAgent byEntity, Vec3d targetVec, float projectileSpeed, float spread)
		{
			// Might as well reuse these attributes for now
			double spreadAngle = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1);
			double spreadMagnitude = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1);

			// Code for zeroing and random spread - took some effort to get it right at all angles, including straight up
			// Zeroing
			Vec3d groundVec = new Vec3d(GameMath.Cos(byEntity.SidedPos.Yaw), 0, GameMath.Sin(targetVec.Z)).Normalize();
			Vec3d up = new Vec3d(0, 1, 0);

			Vec3d horizontalAxis = groundVec.Cross(up);

			double[] matrix = Mat4d.Create();
			Mat4d.Rotate(matrix, matrix, WeaponStats.zeroingAngle * GameMath.DEG2RAD, new double[] { horizontalAxis.X, horizontalAxis.Y, horizontalAxis.Z });
			double[] matrixVec = new double[] { targetVec.X, targetVec.Y, targetVec.Z, 0 };
			matrixVec = Mat4d.MulWithVec4(matrix, matrixVec);

			Vec3d zeroedTargetVec = new Vec3d(matrixVec[0], matrixVec[1], matrixVec[2]);

			// Random spread - uses an older, less elegant method, but I'm not touching this anymore :p
			Vec3d perp = BullseyeMathHelper.Vec3GetPerpendicular(zeroedTargetVec);
			Vec3d perp2 = zeroedTargetVec.Cross(perp);

			double angle = spreadAngle * (GameMath.PI * 2f);
			double offsetAngle = spreadMagnitude * spread * GameMath.DEG2RAD;

			double magnitude = GameMath.Tan(offsetAngle);

			Vec3d deviation = magnitude * perp * GameMath.Cos(angle) + magnitude * perp2 * GameMath.Sin(angle);
			Vec3d newAngle = (zeroedTargetVec + deviation) * (zeroedTargetVec.Length() / (zeroedTargetVec.Length() + deviation.Length()));

			Vec3d velocity = newAngle * projectileSpeed * GlobalConstants.PhysicsFrameTime;

			// What the heck? Server's SidedPos.Motion is somehow twice that of client's!
			velocity += api.Side == EnumAppSide.Client ? byEntity.SidedPos.Motion : byEntity.SidedPos.Motion / 2;

			if (byEntity.MountedOn is Entity mountedEntity)
			{
				velocity += api.Side == EnumAppSide.Client ? mountedEntity.SidedPos.Motion : mountedEntity.SidedPos.Motion / 2;
			}

			return velocity;
		}

		protected virtual Entity CreateProjectileEntity(EntityAgent byEntity, EntityProperties type, ItemStack ammoStack, float damage, float dropChance, float weight, int ammoDurabilityCost)
		{
			/* 
			/ Aaaaaa why don't EntityThrownStone and EntityThrownBeenade inherit from EntityProjectile
			/ Tyron pls
			/
			/ Anyways, we check for all vanilla entity types here just in case some mod tries to make beenades shootable from the sling, or something like that
			*/ 
			Entity projectileEntity = byEntity.World.ClassRegistry.CreateEntity(type);

			if (projectileEntity is EntityProjectile entityProjectile)
			{
				entityProjectile.FiredBy = byEntity;
				entityProjectile.Damage = damage;
				entityProjectile.ProjectileStack = ammoStack;
				entityProjectile.DropOnImpactChance = dropChance;
				entityProjectile.DamageStackOnImpact = ammoDurabilityCost > 0;
				entityProjectile.Weight = weight;
			}
			else if (projectileEntity is EntityThrownStone entityThrownStone)
			{
				entityThrownStone.FiredBy = byEntity;
				entityThrownStone.Damage = damage;
				entityThrownStone.ProjectileStack = ammoStack;
			}
			else if (projectileEntity is EntityThrownBeenade entityThrownBeenade)
			{
				entityThrownBeenade.FiredBy = byEntity;
				// Using reflection to set damage because it's internal
				// It's not worth bugging Tyron about because Bullseye is gonna get its own projectile class anyway
				FieldInfo fieldInfo = typeof(EntityThrownBeenade).GetField("Damage", BindingFlags.Instance | BindingFlags.NonPublic);
				fieldInfo.SetValue(entityThrownBeenade, damage);
				entityThrownBeenade.ProjectileStack = ammoStack;
			}

			return projectileEntity;
		}

		protected virtual Entity FinalizeProjectileEntity(Entity projectileEntity, EntityAgent byEntity)
		{
			if (projectileEntity is EntityProjectile entityProjectile)
			{
				entityProjectile.SetRotation();

#if DEBUG
				if (byEntity.World.Side == EnumAppSide.Server && byEntity is EntityPlayer entityPlayer)
				{
					api.ModLoader.GetModSystem<BullseyeSystemDebug>().SetFollowArrow(entityProjectile, entityPlayer);
				}
#endif
			}

			return projectileEntity;
		}

		private void ServerHandleFire(string eventName, ref EnumHandling handling, IAttribute data)
		{
			TreeAttribute tree = data as TreeAttribute;
			int itemId = tree.GetInt("itemId");

			if (itemId == collObj.Id)
			{
				long entityId = tree.GetLong("entityId");

				ItemSlot itemSlot = RangedWeaponSystem.GetLastEntityRangedItemSlot(entityId);
				EntityAgent byEntity =  api.World.GetEntityById(entityId) as EntityAgent;

				if (RangedWeaponSystem.GetEntityChargeStart(entityId) + GetEntityChargeTime(byEntity) < api.World.ElapsedMilliseconds / 1000f && byEntity.Alive && itemSlot != null)
				{
					Vec3d targetVec = new Vec3d(tree.GetDouble("aimX"), tree.GetDouble("aimY"), tree.GetDouble("aimZ"));

					Shoot(itemSlot, byEntity, targetVec);
					
					handling = EnumHandling.PreventSubsequent;
				}
			}
		}

		protected float GetEntityChargeTime(Entity entity)
		{
			return WeaponStats.chargeTime / entity.Stats.GetBlended("rangedWeaponsSpeed");
		}

		protected virtual void PrepareHeldInteractionHelp()
		{
			if (collObj.Attributes["interactionLangCode"].AsString() != null)
			{
				string interactionsKey = $"{collObj.Attributes["interactionCollectibleCode"].AsString("")}RangedInteractions";

				interactions = ObjectCacheUtil.GetOrCreate(api, interactionsKey, () =>
				{
					List<ItemStack> stacks = null;

					if (collObj.Attributes["interactionCollectibleCode"].AsString() != null)
					{
						stacks = new List<ItemStack>();

						foreach (CollectibleObject obj in api.World.Collectibles)
						{
							if (obj.Code.Path.StartsWith(collObj.Attributes["interactionCollectibleCode"].AsString()))
							{
								stacks.Add(new ItemStack(obj));
							}
						}
					}

					return new WorldInteraction[]
					{
						new WorldInteraction()
						{
							ActionLangCode = collObj.Attributes["interactionLangCode"].AsString(),
							MouseButton = EnumMouseButton.Right,
							Itemstacks = stacks?.ToArray()
						}
					};
				});
			}
		}

	}
}