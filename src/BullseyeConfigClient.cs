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
		public BullseyeEnumAimControlStyle AimStyle {get; set;} = BullseyeEnumAimControlStyle.Free;
		public bool ReticleScaling {get; set;} = true;

		public override AngelConfigSetting[] GetConfigSettings() => new AngelConfigSetting[] 
		{
			new AngelConfigSettingEnum<BullseyeEnumAimControlStyle>("AimStyle", "Aim Style"),
			new AngelConfigSettingBool("ReticleScaling", "Reticle Scaling"),
		};
	}
}