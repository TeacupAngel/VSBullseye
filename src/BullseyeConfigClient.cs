using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Newtonsoft.Json;
using AngelConfig;

namespace Bullseye
{
	public class BullseyeConfigClient : AngelConfigBase
	{
		public override string ConfigName {get;} = "BullseyeClientConfig";
		public override EnumAngelConfigType ConfigType {get;} = EnumAngelConfigType.Client;

		// Config data
		public BullseyeAimControlStyle AimStyle {get; set;} = BullseyeAimControlStyle.Free;		

		public override AngelConfigSetting[] GetConfigSettings() => new AngelConfigSetting[] 
		{
			new AngelConfigSettingEnum<BullseyeAimControlStyle>("AimStyle", "Aim Style"),
		};
	}
}