using System;
using System.Collections.Generic;
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

using AngelConfig;

using Cairo;

namespace Bullseye
{
	public class BullseyeSystemConfig : AngelSystemConfig
	{
		public override bool ShouldLoad(EnumAppSide forSide)
		{
			return true;
		}

		public override void LoadConfigs(ICoreAPI api)
		{
			SyncedConfig = LoadConfig<BullseyeConfigSynced>(api);
			ClientConfig = LoadConfig<BullseyeConfigClient>(api);
		}

		public BullseyeConfigSynced GetSyncedConfig()
		{
			return (BullseyeConfigSynced)SyncedConfig;
		}
	}
}