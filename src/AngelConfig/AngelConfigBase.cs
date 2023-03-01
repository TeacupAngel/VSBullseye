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

		public string Version;

		[JsonIgnore]
		public SemVer SemVerVersion {get; private set;}

		public bool SetVersion(ICoreAPI api, string version, ModInfo modInfo)
		{
			SemVer configVersion;

			if (!SemVer.TryParse(version, out configVersion, out string configError))
			{
				api.Logger.Error($"{modInfo.Name}: Error trying to parse mod config version. Best guess: {configVersion} (error: {configError})");
			}

			bool wasUpdated = configVersion > SemVerVersion;
			SemVerVersion = configVersion;
			Version = version;
			return wasUpdated;
		}

		public void InitialiseVersion(ICoreAPI api, ModInfo modInfo)
		{
			SetVersion(api, Version, modInfo);
		}

		public bool ApplyMigrations(ICoreAPI api, ModInfo modInfo)
		{
			SemVer configVersion;

			if (Version == null)
			{
				api.Logger.Error($"Config version was null! Cannot migrate because data was lost, setting version to latest");
				SetVersion(api, modInfo.Version, modInfo);
				return true;
			}

			if (!SemVer.TryParse(Version, out configVersion, out string configError))
			{
				api.Logger.Error($"Error trying to parse config version. Best guess: {configVersion} (error: {configError})");
			}

			bool needsSave = false;

			AngelConfigMigration[] migrationList = GetMigrations();

			if (migrationList != null)
			{
				for (int i = migrationList.Length - 1; i >=0; i--)
				{
					AngelConfigMigration migration = migrationList[i];

					if (migration.Version > configVersion)
					{
						migration.Action(this, api);
						needsSave = true;

						continue;
					}
						
					break;
				}
			}

			needsSave |= SetVersion(api, modInfo.Version, modInfo);

			return needsSave;
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
	}
}