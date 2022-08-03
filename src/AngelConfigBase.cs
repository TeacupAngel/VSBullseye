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

			if (migrationList is not null)
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

			MakeLatest(modInfo);

			return saveConfig;
		}

		public void Save(ICoreAPI api)
		{
			MethodInfo method = api.GetType().GetMethod("StoreModConfig", BindingFlags.Instance | BindingFlags.Public);
			MethodInfo genericMethod = method.MakeGenericMethod(GetType());

			genericMethod.Invoke(api, new object[] {this, $"{ConfigName}.json"});
		}

		public virtual AngelConfigMigration[] GetMigrations()
		{
			return null;
		}

		public virtual AngelConfigSetting[] GetConfigSettings()
		{
			return null;
		}

		public void MakeLatest(ModInfo modInfo)
		{
			Version = modInfo.Version;
		}
	}
}