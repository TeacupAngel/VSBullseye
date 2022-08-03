using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using Vintagestory.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using ProtoBuf;
using System.Reflection;
using Newtonsoft.Json;

using Cairo;

namespace AngelConfig
{
	public class AngelConfigArgumentException : ArgumentException 
	{
		public AngelConfigArgumentException(string message) : base(message) {}
	}

	public abstract class AngelSystemConfig : ModSystem
	{
		[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
		public class AngelSyncedConfigPacket
		{
			public string ModName {get; set;}
			public string Data {get; set;}
		}

		// Common
		public AngelConfigBase ServerConfig {get; protected set;}
		public AngelConfigBase SyncedConfig {get; protected set;}
		public AngelConfigBase ClientConfig {get; protected set;}

		private Dictionary<string, AngelConfigSetting> registeredSettings = new Dictionary<string, AngelConfigSetting>();

		public override double ExecuteOrder()
		{
			return 0.05;
		}

		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return false;
		}

		public abstract void LoadConfigs(ICoreAPI api);

		public T LoadConfig<T>(ICoreAPI api) where T : AngelConfigBase, new()
		{
			T config = new T();

			if (config.ConfigType == EnumAngelConfigType.Client && api.Side == EnumAppSide.Server) return null;
			if ((config.ConfigType == EnumAngelConfigType.Server) && api.Side == EnumAppSide.Client) return null;

			if (config.ConfigType == EnumAngelConfigType.Synced)
			{
				CreateNetworkChannels(api);
			}

			// Synced configs are loaded only serverside and then sent to clients; the client loads a default until server values can be sent
			if (!(config.ConfigType == EnumAngelConfigType.Synced && api.Side == EnumAppSide.Client))
			{
				bool saveConfig = true;

				try
				{
					T newConfig = api.LoadModConfig<T>($"{config.ConfigName}.json");

					if (newConfig is not null)
					{
						config = newConfig;
						saveConfig = false;
					}
					else
					{
						config.MakeLatest(Mod.Info);
					}
				}
				catch
				{
					config.MakeLatest(Mod.Info);

					ConfigLoadError($"{Mod.Info.Name}: Failed to load {GetConfigTypeString(config.ConfigType)} config file {config.ConfigName}.json! Default values restored", config, api);
				}

				try
				{
					saveConfig |= config.ApplyMigrations(api, Mod.Info);
				}
				catch
				{
					ConfigLoadError($"{Mod.Info.Name}: Failed to migrate {GetConfigTypeString(config.ConfigType)} config file {config.ConfigName}.json to newest version. Could migrate up to {config.Version.ToString()}", config, api);
				}

				if (saveConfig)
				{
					config.Save(api);
				}
			}

			foreach (AngelConfigSetting setting in config.GetConfigSettings())
			{
				RegisterConfigSetting(setting, config, api);
			}

			return config;
		}

		private void ConfigLoadError(string message, AngelConfigBase config, ICoreAPI api)
		{
			if (api is ICoreServerAPI serverAPI)
			{
				serverAPI.Logger.Log(EnumLogType.Warning, message);

				if (serverAPI.World.AllOnlinePlayers.Length > 0)
				{
					serverAPI.SendMessageToGroup(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
				}
				else
				{
					serverAPI.Event.PlayerNowPlaying += (IServerPlayer byPlayer) => 
					{
						if (byPlayer.HasPrivilege("controlserver"))
						{
							byPlayer.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
						}
					};
				}
			}
			else
			{
				(api as ICoreClientAPI).ShowChatMessage(message);
			}
		}

		private string GetConfigTypeString(EnumAngelConfigType configType)
		{
			return configType switch
			{
				EnumAngelConfigType.Server => "server",
				EnumAngelConfigType.Synced => "synced",
				EnumAngelConfigType.Client => "client",
				_ => "unknown type of"
			};
		}

		public void RegisterConfigSetting(AngelConfigSetting setting, AngelConfigBase config, ICoreAPI api)
		{
			if (config.ConfigType == EnumAngelConfigType.Client && api.Side == EnumAppSide.Server) return;
			if (config.ConfigType == EnumAngelConfigType.Server && api.Side == EnumAppSide.Client) return;

			setting.Config = config;

			try
			{
				registeredSettings.Add(setting.Code.ToLowerInvariant(), setting);
			}
			catch
			{
				string message = $"{Mod.Info.Name}: Can't register {GetConfigTypeString(config.ConfigType)} config setting {setting.Code}, it probably already exists!";

				sapi?.Logger.Warning(message);
				capi?.Logger.Warning(message);
			}
		}

		private void CreateNetworkChannels(ICoreAPI api)
		{
			if (ServerNetworkChannel is null && api is ICoreServerAPI serverAPI)
			{
				ServerNetworkChannel = serverAPI.Network.GetChannel("angelconfigserver");

				if (ServerNetworkChannel is null) 
				{
					ServerNetworkChannel = serverAPI.Network.RegisterChannel("angelconfigserver")
					.RegisterMessageType<AngelSyncedConfigPacket>();
				}

				return;
			}

			if (ClientNetworkChannel is null && api is ICoreClientAPI clientAPI)
			{
				ClientNetworkChannel = clientAPI.Network.GetChannel("angelconfigserver");

				if (ClientNetworkChannel is null) 
				{
					ClientNetworkChannel = clientAPI.Network.RegisterChannel("angelconfigserver")
					.RegisterMessageType<AngelSyncedConfigPacket>()
					.SetMessageHandler<AngelSyncedConfigPacket>(OnSyncedConfigPacket);
				}
			}
		}

		private void CommandSet(ICoreAPI api, CmdArgs args, IServerPlayer player = null, int groupId = 0)
		{
			if (args.Length > 0)
			{
				string code = args.PopWord();

				if (registeredSettings.TryGetValue(code.ToLowerInvariant(), out AngelConfigSetting setting))
				{
					// No error message needed for these - client shouldn't have server settings (and vice versa) in the first place
					if (setting.Config.ConfigType == EnumAngelConfigType.Client && api.Side == EnumAppSide.Server) return;
					if (setting.Config.ConfigType == EnumAngelConfigType.Server && api.Side == EnumAppSide.Client) return;

					if (setting.Config.ConfigType == EnumAngelConfigType.Synced && api.Side == EnumAppSide.Client)
					{
						CommandUniversalMessage($"{Mod.Info.Name}: this setting cannot be set on the client", EnumChatType.CommandError, api, player, groupId);
						return;
					}

					if (setting.Set is null)
					{
						CommandUniversalMessage($"{Mod.Info.Name}: cannot use set with '{code}'", EnumChatType.CommandSuccess, api, player, groupId);
						return;
					}

					bool success = false;
					string result = null;

					try
					{
						result = setting.Set(args);
						
						success = true;
					}
					catch (AngelConfigArgumentException exception)
					{
						CommandUniversalMessage($"{Mod.Info.Name}: error setting '{setting.Code}': {exception.Message}", EnumChatType.CommandSuccess, api, player, groupId);
					}

					if (success)
					{
						if (result is not null)
						{
							CommandUniversalMessage($"{Mod.Info.Name}: set '{setting.Code}' to {result}", EnumChatType.CommandSuccess, api, player, groupId);
						}

						setting.Config.Save(api);

						if (setting.Config.ConfigType == EnumAngelConfigType.Synced)
						{
							OnSyncedConfigChanged(api as ICoreServerAPI);
						}
					}
				}
				else
				{
					CommandUniversalMessage($"{Mod.Info.Name}: unknown setting '{code}'", EnumChatType.CommandSuccess, api, player, groupId);
				}
			}
			else
			{
				CommandUniversalMessage($"{Mod.Info.Name}: this command requires the name of a setting and at least 1 argument", EnumChatType.CommandError, api, player, groupId);
			}
		}

		// Serverside variant
		public void CommandSet(ICoreServerAPI api, IServerPlayer player, int groupId, CmdArgs args)
		{
			CommandSet(api, args, player, groupId);
		}

		// Clientside variant
		public void CommandSet(ICoreClientAPI api, int groupId, CmdArgs args)
		{
			CommandSet(api, args);
		}

		private void CommandGet(ICoreAPI api, CmdArgs args, IServerPlayer player = null, int groupId = 0)
		{
			if (args.Length > 0)
			{
				string code = args.PopWord();

				if (registeredSettings.TryGetValue(code.ToLowerInvariant(), out AngelConfigSetting setting))
				{
					if (setting.Config.ConfigType == EnumAngelConfigType.Client && api.Side == EnumAppSide.Server) return;
					if (setting.Config.ConfigType == EnumAngelConfigType.Server && api.Side == EnumAppSide.Client) return;

					if (setting.Get is null)
					{
						CommandUniversalMessage($"{Mod.Info.Name}: cannot use get with'{code}'", EnumChatType.CommandSuccess, api, player, groupId);

						return;
					}

					string result = null;

					try
					{
						result = setting.Get();
					}
					catch (AngelConfigArgumentException exception)
					{
						CommandUniversalMessage($"{Mod.Info.Name}: error reading '{setting.Code}': {exception.Message}", EnumChatType.CommandSuccess, api, player, groupId);
					}

					if (result is not null)
					{
						CommandUniversalMessage($"{Mod.Info.Name}: '{setting.Code}' is currently {result}", EnumChatType.CommandSuccess, api, player, groupId);
					}
				}
				else
				{
					CommandUniversalMessage($"{Mod.Info.Name}: unknown setting '{code}'", EnumChatType.CommandSuccess, api, player, groupId);
				}
			}
			else
			{
				CommandUniversalMessage($"{Mod.Info.Name}: this command requires the name of a setting", EnumChatType.CommandSuccess, api, player, groupId);
			}
		}

		// Serverside variant
		public void CommandGet(ICoreServerAPI api, IServerPlayer player, int groupId, CmdArgs args)
		{
			CommandGet(api, args, player, groupId);
		}

		// Clientside variant
		public void CommandGet(ICoreClientAPI api, int groupId, CmdArgs args)
		{
			CommandGet(api, args);
		}

		private void CommandUniversalMessage(string message, EnumChatType messageType, ICoreAPI api, IServerPlayer player = null, int groupId = 0)
		{
			if (player is not null)
			{
				player.SendMessage(groupId, message, messageType);
			}
			else if (api is ICoreClientAPI clientAPI)
			{
				capi.ShowChatMessage(message);
			}
		}

		// Server
		protected ICoreServerAPI sapi {get; private set;}
		protected IServerNetworkChannel ServerNetworkChannel {get; private set;}

		public override void Start(ICoreAPI api)
		{
			LoadConfigs(api);
		}

		public override void StartServerSide(ICoreServerAPI api)
		{
			sapi = api;

			api.Event.PlayerJoin += OnPlayerJoin;
		}

		private void OnPlayerJoin(IServerPlayer player)
		{
			SendSyncedConfigToPlayer(player);
		}

		public void OnSyncedConfigChanged(ICoreServerAPI api)
		{
			foreach (IServerPlayer player in api.World.AllOnlinePlayers)
			{
				SendSyncedConfigToPlayer(player);
			}
		}

		public class AngelConfigSerializationBinder : SerializationBinder
		{
			public AngelConfigSerializationBinder() {}

			public override Type BindToType(string assemblyName, string typeName)
			{
				Type returnType = Assembly.GetExecutingAssembly().GetType(typeName);

				if (!typeof(AngelConfigBase).IsAssignableFrom(returnType))
				{
					throw new Exception("Tried to pass unallowed class as config");
				}

				return returnType;
			}

			public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
			{
				assemblyName = "#dll"; // The things we have to do because the Newtonsoft.Json version used is 7 years old :/
				typeName = serializedType.FullName;
			}
		}

		private void SendSyncedConfigToPlayer(IServerPlayer player)
		{			
			AngelSyncedConfigPacket packet = new AngelSyncedConfigPacket() {
				ModName = Mod.Info.Name,
				Data = JsonConvert.SerializeObject(SyncedConfig, new JsonSerializerSettings() {
					TypeNameHandling = TypeNameHandling.All,
					Binder = new AngelConfigSerializationBinder()
				})
			};

			ServerNetworkChannel.SendPacket(packet, player);
		}

		// Client
		private ICoreClientAPI capi {get; set;}
		private IClientNetworkChannel ClientNetworkChannel {get; set;}

		public override void StartClientSide(ICoreClientAPI api)
		{
			capi = api;
		}

		private void OnSyncedConfigPacket(AngelSyncedConfigPacket packet)
		{
			if (packet.ModName != Mod.Info.Name) return;
			
			AngelConfigBase incomingConfig;

			try
			{
				incomingConfig = JsonConvert.DeserializeObject<AngelConfigBase>(packet.Data.Replace("#dll", Assembly.GetExecutingAssembly().GetName().FullName), new JsonSerializerSettings() 
				{
					TypeNameHandling = TypeNameHandling.All,
					Binder = new AngelConfigSerializationBinder()
				});
			}
			catch (Exception)
			{
				capi.ShowChatMessage($"Failed to deserialize synced {packet.ModName} config from server!");
				return;
			}
		
			if (incomingConfig is null)
			{
				capi.ShowChatMessage($"Failed to synchronise {packet.ModName} config from server!");
				return;
			}

			// Copy new values into existing config instance, so that we don't have to re-register settings; not the most elegant, but it happens only on sync anyway
			// In the future, we can introduce delta sharing where only the changed variables will be sent, instead of everything
			// There won't be any more need to send the type either, saving bandwidth and closing a potential security hole
			foreach (PropertyInfo property in SyncedConfig.GetType().GetProperties().Where(p => p.CanWrite))
			{
				property.SetValue(SyncedConfig, property.GetValue(incomingConfig, null), null);
			}
		}

		// Dispose
		public override void Dispose()
		{
			ServerConfig = null;
			SyncedConfig = null;
			ClientConfig = null;

			registeredSettings = null;

			sapi = null;
			ServerNetworkChannel = null;

			capi = null;
			ClientNetworkChannel = null;
		}
	}
}