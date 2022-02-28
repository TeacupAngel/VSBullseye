using System;

namespace Bullseye
{
    public class BullseyeServerConfig
    {
		// Config data
		public float globalAccuracy = -1f;
		public float aimDifficulty = 1f;

		// Versioning
		private const int CURRENT_VERSION = 2;

		public int version = 1;

		private static readonly Action<BullseyeServerConfig>[] migrationList = new Action<BullseyeServerConfig>[] {
			/* 1 */ (BullseyeServerConfig config) => 
			{
				config.aimDifficulty = config.globalAccuracy;
				config.globalAccuracy = -1f;
			}
		};

		public bool ApplyMigrations()
		{
			bool saveConfig = version < CURRENT_VERSION;

			while (version < CURRENT_VERSION)
			{
				migrationList[version - 1](this);
				version++;
			}

			return saveConfig;
		}

		public void MakeLatest()
		{
			version = CURRENT_VERSION;
		}
    }
}