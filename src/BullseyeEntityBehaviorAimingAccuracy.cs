using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Bullseye
{
	public class BullseyeEntityBehaviorAimingAccuracy : EntityBehavior
	{
		public Random Rand;
		public bool IsAiming;

		List<AccuracyModifier> modifiers = new List<AccuracyModifier>();

		private BullseyeRangedWeaponStats weaponStats = new BullseyeRangedWeaponStats();

		private BullseyeSystemRangedWeapon rangedWeaponSystem;
		private BullseyeSystemClientAiming clientAimingSystem;

		private EntityAgent agent;

		public BullseyeEntityBehaviorAimingAccuracy(Entity entity) : base(entity)
		{
			agent = entity as EntityAgent;

			rangedWeaponSystem = entity.Api.ModLoader.GetModSystem<BullseyeSystemRangedWeapon>();

			if (entity.Api.Side == EnumAppSide.Client)
			{
				clientAimingSystem = entity.Api.ModLoader.GetModSystem<BullseyeSystemClientAiming>();
				(entity.Api as ICoreClientAPI).Input.InWorldAction += Event_InWorldAction;
			}

			Rand = new Random((int)(entity.EntityId + entity.World.ElapsedMilliseconds));

			modifiers.Add(new BaseAimingAccuracy(agent, clientAimingSystem));
			modifiers.Add(new MovingAimingAccuracy(agent, clientAimingSystem));
			modifiers.Add(new MountedAimingAccuracy(agent, clientAimingSystem));
			modifiers.Add(new OnHurtAimingAccuracy(agent, clientAimingSystem));

			entity.Attributes.RegisterModifiedListener("bullseyeAiming", OnAimingChanged);
			entity.Stats.Set("walkspeed", "bullseyeaimmod", 0f);
		}

		public void Event_InWorldAction(EnumEntityAction action, bool on, ref EnumHandling handled)
		{
			if (IsAiming && !weaponStats.allowSprint && action == EnumEntityAction.Sprint && on)
			{
				handled = EnumHandling.PreventDefault;
			}
		}

		public void SetRangedWeaponStats(BullseyeRangedWeaponStats weaponStats)
		{
			this.weaponStats = weaponStats;
		}

		private void OnAimingChanged()
		{
			bool beforeAiming = IsAiming;
			IsAiming = entity.Attributes.GetInt("bullseyeAiming") > 0;

			if (beforeAiming == IsAiming) return;

			if (weaponStats.moveSpeedPenalty != 0)
			{
				entity.Stats.Set("walkspeed", "bullseyeaimmod", IsAiming ? -(weaponStats.moveSpeedPenalty * entity.Stats.GetBlended("walkspeed")) : 0f);
			}

			for (int i = 0; i < modifiers.Count; i++)
			{
				if (IsAiming)
				{
					modifiers[i].BeginAim();
				}
				else
				{
					modifiers[i].EndAim();
				}
			}

			if (entity.World is IServerWorldAccessor && IsAiming)
			{
				double rndpitch = Rand.NextDouble();
				double rndyaw = Rand.NextDouble();
				entity.WatchedAttributes.SetDouble("aimingRandPitch", rndpitch);
				entity.WatchedAttributes.SetDouble("aimingRandYaw", rndyaw);
			}
			else if (entity.World is IClientWorldAccessor cWorld && cWorld.Player.Entity.EntityId == entity.EntityId)
			{
				if (IsAiming) {	clientAimingSystem.StartAiming(); } else { clientAimingSystem.StopAiming(); }
			}
		}

		public override void OnGameTick(float deltaTime)
		{
			if (!IsAiming) return;

			if (!entity.Alive)
			{
				entity.Attributes.SetInt("bullseyeAiming", 0);
				return;
			}

			if (!weaponStats.allowSprint)
			{
				agent.CurrentControls &= ~EnumEntityActivity.SprintMode;
				agent.Controls.Sprint = false;
				agent.ServerControls.Sprint = false;
			}

			for (int i = 0; i < modifiers.Count; i++)
			{
				modifiers[i].Update(deltaTime, weaponStats);
			}
		}

		public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
		{
			base.OnEntityReceiveDamage(damageSource, ref damage);

			if (damageSource.Type == EnumDamageType.Heal) return;

			for (int i = 0; i < modifiers.Count; i++)
			{
				modifiers[i].OnHurt(damage);
			}
		}

		public override string PropertyName()
		{
			return "bullseye.aimingaccuracy";
		}
	}


	public class AccuracyModifier
	{
		internal EntityAgent entity;
		internal long aimStartMs;

		protected BullseyeSystemClientAiming clientAimingSystem;

		public float SecondsSinceAimStart
		{
			get { return (entity.World.ElapsedMilliseconds - aimStartMs) / 1000f; }
		}

		public AccuracyModifier(EntityAgent entity, BullseyeSystemClientAiming clientAimingSystem)
		{
			this.entity = entity;
			this.clientAimingSystem = clientAimingSystem;
		}

		public virtual void BeginAim()
		{
			aimStartMs = entity.World.ElapsedMilliseconds;
		}

		public virtual void EndAim()
		{

		}

		public virtual void OnHurt(float damage) { }

		public virtual void Update(float dt, BullseyeRangedWeaponStats weaponStats)
		{

		}
	}


	public class BaseAimingAccuracy : AccuracyModifier
	{
		public BaseAimingAccuracy(EntityAgent entity, BullseyeSystemClientAiming clientAimingSystem) : base(entity, clientAimingSystem)
		{
		}

		public override void Update(float dt, BullseyeRangedWeaponStats weaponStats)
		{
			float modspeed = entity.Stats.GetBlended("rangedWeaponsSpeed");

			// Linear inaccuracy from starting to aim - kept in reserve
			//float bullseyeAccuracy = GameMath.Max((weaponStats.accuracyStartTime - SecondsSinceAimStart * modspeed) / weaponStats.accuracyStartTime, 0f) * weaponStats.accuracyStart; // Linear inaccuracy from starting to aim
			
			// Squared inaccuracy when starting to aim
			float accMod = GameMath.Clamp((SecondsSinceAimStart * modspeed) / weaponStats.accuracyStartTime, 0f, 1f);
			accMod = 1f - (accMod * accMod); 
			float bullseyeAccuracy = accMod * weaponStats.accuracyStart;

			// Linear loss of accuracy from holding too long
			bullseyeAccuracy += GameMath.Clamp((SecondsSinceAimStart - weaponStats.accuracyOvertimeStart - weaponStats.accuracyStartTime) / weaponStats.accuracyOvertimeTime, 0f, 1f) * weaponStats.accuracyOvertime;

			if (clientAimingSystem != null)
			{
				clientAimingSystem.DriftMultiplier = 1 + bullseyeAccuracy;
				clientAimingSystem.TwitchMultiplier = 1 + (bullseyeAccuracy * 3f);
			}
		}
	}

	public class MovingAimingAccuracy : AccuracyModifier
	{
		private float walkAccuracyPenalty;
		private float sprintAccuracyPenalty;

		private float walkMaxPenaltyMod = 1f;
		private float sprintMaxPenaltyMod = 1.5f;

		private float penaltyRiseRate = 6.5f;
		private float penaltyDropRate = 2.5f;

		private float driftMod = 0.8f;
		private float twitchMod = 0.6f;

		public MovingAimingAccuracy(EntityAgent entity, BullseyeSystemClientAiming clientAimingSystem) : base(entity, clientAimingSystem)
		{
		}

		public override void BeginAim()
		{
			base.BeginAim();

			walkAccuracyPenalty = 0f;
			sprintAccuracyPenalty = 0f;
		}

		public override void Update(float dt, BullseyeRangedWeaponStats weaponStats)
		{
			walkAccuracyPenalty = GameMath.Clamp(entity.Controls.TriesToMove ? walkAccuracyPenalty + dt * penaltyRiseRate : walkAccuracyPenalty - dt * penaltyDropRate, 0, walkMaxPenaltyMod);
			sprintAccuracyPenalty = GameMath.Clamp(entity.Controls.TriesToMove && entity.Controls.Sprint ? sprintAccuracyPenalty + dt * penaltyRiseRate : sprintAccuracyPenalty - dt * penaltyDropRate, 0, sprintMaxPenaltyMod);

			if (clientAimingSystem != null)
			{
				clientAimingSystem.DriftMultiplier += ((walkAccuracyPenalty + sprintAccuracyPenalty) * driftMod * weaponStats.accuracyMovePenalty);
		   		clientAimingSystem.TwitchMultiplier += ((walkAccuracyPenalty + sprintAccuracyPenalty) * twitchMod * weaponStats.accuracyMovePenalty);
			}
		}
	}

	public class MountedAimingAccuracy : AccuracyModifier
	{
		private float walkAccuracyPenalty;
		private float sprintAccuracyPenalty;

		private float walkMaxPenaltyMod = 0.8f;
		private float sprintMaxPenaltyMod = 1.5f;

		private float penaltyRiseRate = 6.5f;
		private float penaltyDropRate = 2f;

		private float driftMod = 0.8f;
		private float twitchMod = 0.6f; 

		public MountedAimingAccuracy(EntityAgent entity, BullseyeSystemClientAiming clientAimingSystem) : base(entity, clientAimingSystem)
		{
		}

		public override void BeginAim()
		{
			base.BeginAim();

			walkAccuracyPenalty = 0f;
			sprintAccuracyPenalty = 0f;
		}

		public override void Update(float dt, BullseyeRangedWeaponStats weaponStats)
		{
			bool mountTriesToMove = entity.MountedOn?.Controls != null && entity.MountedOn.Controls.TriesToMove;
			bool mountTriesToSprint = mountTriesToMove && entity.MountedOn.Controls.Sprint;

			walkAccuracyPenalty = GameMath.Clamp(mountTriesToMove ? walkAccuracyPenalty + dt * penaltyRiseRate : walkAccuracyPenalty - dt * penaltyDropRate, 0, walkMaxPenaltyMod);
			sprintAccuracyPenalty = GameMath.Clamp(mountTriesToSprint ? sprintAccuracyPenalty + dt * penaltyRiseRate : sprintAccuracyPenalty - dt * penaltyDropRate, 0, sprintMaxPenaltyMod);

			if (clientAimingSystem != null)
			{
				clientAimingSystem.DriftMultiplier += (walkAccuracyPenalty + sprintAccuracyPenalty) * driftMod * weaponStats.accuracyMovePenalty;
		   		clientAimingSystem.TwitchMultiplier += (walkAccuracyPenalty + sprintAccuracyPenalty) * twitchMod * weaponStats.accuracyMovePenalty;
			}
		}
	}

	public class OnHurtAimingAccuracy : AccuracyModifier
	{
		float accuracyPenalty;

		public OnHurtAimingAccuracy(EntityAgent entity, BullseyeSystemClientAiming clientAimingSystem) : base(entity, clientAimingSystem)
		{
		}

		public override void Update(float dt, BullseyeRangedWeaponStats weaponStats)
		{
			accuracyPenalty = GameMath.Clamp(accuracyPenalty - dt / 3, 0, 0.4f);

			//accuracy -= accuracyPenalty;
		}

		public override void OnHurt(float damage)
		{
			if (damage > 3)
			{
				accuracyPenalty = -0.4f;
			}
		}
	}
	
}
