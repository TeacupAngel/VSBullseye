#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using ProtoBuf;

namespace Bullseye
{
	public class BullseyeSystemDebug : ModSystem
	{
		private EntityProjectile currentArrow;
		private EntityPos arrowLaunchPos;
		private EntityPlayer arrowPlayer;
		private long arrowStartTime;

		public void SetFollowArrow(EntityProjectile arrow, EntityPlayer entityPlayer)
		{
			if (followArrowTickListenerId >= 0)
			{
				if (currentArrow != null && arrowPlayer != null)
				{
					(arrowPlayer.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, "Tracking new arrow (cannot track two arrows at once!)", EnumChatType.Notification);
				}

				currentArrow = arrow;
				arrowPlayer = entityPlayer;
				arrowLaunchPos = arrow.ServerPos.Copy();
				arrowStartTime = arrow.World.ElapsedMilliseconds; 
			}
		}

		private void FollowArrow(float dt)
		{
			if (currentArrow != null) 
			{
				if ((currentArrow.ServerPos.Motion.X == 0 && currentArrow.ServerPos.Motion.Y == 0 && currentArrow.ServerPos.Motion.Z == 0)
					|| !currentArrow.Alive)
				{
					double arrowDistance = arrowLaunchPos.DistanceTo(currentArrow.ServerPos.XYZ);
					float arrowFlightTime = (currentArrow.World.ElapsedMilliseconds - arrowStartTime) / 1000f;

					(arrowPlayer.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, String.Format("Arrow landed at distance {0:0.##}, flight time: {1}", arrowDistance, arrowFlightTime), EnumChatType.Notification);

					currentArrow = null;
				}
			}
		}

		long followArrowTickListenerId = -1;

		private void CommandTrackArrows(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
		{
			if (followArrowTickListenerId == -1)
			{
				currentArrow = null;
				followArrowTickListenerId = sapi.Event.RegisterGameTickListener(FollowArrow, 100);
				player.SendMessage(groupId, "Bullseye: now tracking arrows", EnumChatType.CommandSuccess);
			}
			else
			{
				sapi.Event.UnregisterGameTickListener(followArrowTickListenerId);
				followArrowTickListenerId = -1;
				player.SendMessage(groupId, "Bullseye: no longer tracking arrows", EnumChatType.CommandSuccess);
			}
		}

		private void CommandSpawnArcherbot(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
		{
			EntityProperties type = sapi.World.GetEntityType(new AssetLocation("playerbot"));
			EntityPlayerBot playerBot = player.Entity.World.Api.ClassRegistry.CreateEntity(type) as EntityPlayerBot;
			playerBot.Pos.SetFrom(player.Entity.ServerPos);
			playerBot.Pos.Pitch = 0f;
			playerBot.Pos.Yaw = 0f;
			playerBot.ServerPos.SetFrom(playerBot.Pos);
			playerBot.World = sapi.World;
			sapi.World.SpawnEntity(playerBot);

			playerBot.RightHandItemSlot.Itemstack = new ItemStack(sapi.World.SearchItems(new AssetLocation("bow-simple"))[0], 1);
		}

		private void CommandGiveExtraTrait(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
		{
			string traitName = args.PopWord();

			if (string.IsNullOrEmpty(traitName))
			{
				player.SendMessage(groupId, "Bullseye: trait name required", EnumChatType.CommandError);
				return;
			}

			List<string> extraTraits = player.Entity.WatchedAttributes.GetStringArray("extraTraits", Array.Empty<string>()).ToList();

			if (extraTraits.Contains(traitName))
			{
				player.SendMessage(groupId, "Bullseye: player already has this trait", EnumChatType.CommandError);
				return;
			}
			extraTraits.Add(traitName);

			player.Entity.WatchedAttributes.SetStringArray("extraTraits", extraTraits.ToArray());
		}

		private void CommandResetTraits(ICoreServerAPI sapi, IServerPlayer player, int groupId, CmdArgs args)
		{
			player.Entity.WatchedAttributes.SetStringArray("extraTraits", Array.Empty<string>());
		}

		public override void StartServerSide(ICoreServerAPI sapi)
		{
			sapi.RegisterCommand("bsedbg", "", "", (IServerPlayer player, int groupId, CmdArgs args) => {
				if (args.Length > 0)
				{
					string cmd = args.PopWord();

					switch (cmd)
					{
						case "track":
							CommandTrackArrows(sapi, player, groupId, args);
							return;
						case "archerbot":
							CommandSpawnArcherbot(sapi, player, groupId, args);
							return;
						case "givetrait":
							CommandGiveExtraTrait(sapi, player, groupId, args);
							return;
						case "resettraits":
							CommandResetTraits(sapi, player, groupId, args);
							return;
					}
				}

				player.SendMessage(groupId, "/bsedbg [track, archerbot]", EnumChatType.CommandError);
			}, Privilege.controlserver);
		}

		// Clientside
		private float autofireActionTime;
		private int autofireStage = 0;
		private int autofireAmmoType = -1;
		private ICoreClientAPI capi;
		private void AutofireTick(float dt)
		{
			bool nextStage = false;

			switch (autofireStage)
			{
				case 0:
					if (autofireActionTime == 0f)
					{
						if (autofireSwitchAmmo)
						{
							BullseyeSystemRangedWeapon rangedWeaponSystem = capi.ModLoader.GetModSystem<BullseyeSystemRangedWeapon>();

							if (capi.World.Player.Entity?.RightHandItemSlot.Itemstack?.Collectible is BullseyeItemRangedWeapon rangedWeaponItem)
							{
								List<ItemStack> ammoTypes = rangedWeaponItem.GetAvailableAmmoTypes(capi.World.Player.Entity.RightHandItemSlot, capi.World.Player);

								if (++autofireAmmoType >= ammoTypes.Count)
								{
									autofireAmmoType = 0;
								}

								rangedWeaponSystem.EntitySetAmmoType(capi.World.Player.Entity, rangedWeaponItem.AmmoType, ammoTypes[autofireAmmoType]);
								rangedWeaponSystem.SendRangedWeaponAmmoSelectPacket(rangedWeaponItem.AmmoType, ammoTypes[autofireAmmoType]);
							}
						}
					}
					else if (autofireActionTime > 0.1f)
					{
						nextStage = true;
					}
					break;
				case 1:
					if (autofireActionTime < 3f)
					{
						capi.Input.InWorldMouseButton.Right = true;
					}
					else
					{
						nextStage = true;
					}
					break;
				case 2:
					if (autofireActionTime < 1f)
					{
						capi.Input.InWorldMouseButton.Right = false;
					}
					else
					{
						nextStage = true;
					}
					break;
			}

			if (nextStage)
			{
				autofireActionTime = 0;
				autofireStage = autofireStage >= 2 ? 0 : autofireStage + 1;
			}
			else
			{
				autofireActionTime += dt;
			}
		}

		private long autofireListenerId = -1;
		private bool autofireSwitchAmmo = false;
		private void CommandAutofire(ICoreClientAPI api, IClientPlayer player, int groupId, CmdArgs args)
		{
			capi = api;

			if (autofireListenerId == -1)
			{
				autofireListenerId = capi.Event.RegisterGameTickListener(AutofireTick, 0);
			}
			else
			{
				capi.Event.UnregisterGameTickListener(autofireListenerId);
				autofireListenerId = -1;
			}

			autofireSwitchAmmo = args.PopBool() ?? false;
		}

		public override void StartClientSide(ICoreClientAPI capi)
		{
			capi.RegisterCommand("bsedbg", "", "", (int groupId, CmdArgs args) => {
				if (args.Length > 0)
				{
					string cmd = args.PopWord();

					switch (cmd)
					{
						case "autofire":
							CommandAutofire(capi, capi.World.Player, groupId, args);
							return;
					}
				}

				capi.ShowChatMessage(".bsedbg [autofire]");
			});
		}

		public override void Dispose()
		{
			currentArrow = null;
			arrowLaunchPos = null;
			arrowPlayer = null;

			capi = null;
		}
	}
}
#endif