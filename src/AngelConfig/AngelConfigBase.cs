using System;
using System.Reflection;
using Vintagestory.API.Common;
using ProperVersion;
using Newtonsoft.Json;

namespace AngelConfig
{
	public enum EnumAngelConfigType
	{
		Server,
		Client,
		Synced,
		None
	}

	public struct AngelConfigMigration
	{
		public SemVer Version {get; private set;}
		public Action<AngelConfigBase, ICoreAPI> Action {get; private set;}
	}

	[JsonObject(MemberSerialization.OptOut)]
	public abstract class AngelConfigBase
	{
		[JsonIgnore]
		public abstract string ConfigName {get;}

		[JsonIgnore]
		public abstract EnumAngelConfigType ConfigType {get;}

		public string Version {get; set;}

		public bool ApplyMigrations(ICoreAPI api, ModInfo modInfo)
		{
			SemVer configVersion;

			if (Version is null)
			{
				api.Logger.Error($"Config version was null! Cannot migrate because data was lost, setting version to latest");
				MakeLatest(modInfo);
				return true;
			}

			if (!SemVer.TryParse(Version, out configVersion, out string configError))
			{
				api.Logger.Error($"Error trying to parse config version. Best guess: {configVersion} (error: {configError})");
			}

			bool saveConfig = false;

			AngelConfigMigration[] migrationList = GetMigrations();

			if (migrationList != null)
			{
				for (int i = migrationList.Length - 1; i >=0; i--)
				{
					AngelConfigMigration migration = migrationList[i];

					if (migration.Version > configVersion)
					{
						migration.Action(this, api);
						Version = migration.Version.ToString();
						saveConfig = true;

						continue;
					}
						
					break;
				}
			}

			// Version config doesn't get saved right now unless a migration happens. That's not the way it should be really
			// TODO: Save every time config file version doesn't match mod version
			// also split the MakeLatest and config saving into a separate method from ApplyMigrations
			MakeLatest(modInfo);

			return saveConfig;
		}

		public void Save(ICoreAPI api)
		{
			api.StoreModConfig(this, $"{ConfigName}.json");
		}

		public virtual AngelConfigMigration[] GetMigrations()
		{
			return null;
		}

		public abstract AngelConfigSetting[] GetConfigSettings();

		public void MakeLatest(ModInfo modInfo)
		{
			Version = modInfo.Version;
		}
	}
}