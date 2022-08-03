using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using AngelConfig;

namespace Bullseye
{
	public class BullseyeConfigSynced : AngelConfigBase
	{
		public override string ConfigName {get;} = "BullseyeSyncedConfig";
		public override EnumAngelConfigType ConfigType {get;} = EnumAngelConfigType.Synced;

		// Config data
		public float GlobalAccuracy {get;} = -1f;
		public float AimDifficulty {get; set;} = 1f;
		public float ArrowDamage {get; set;} = 1f;
		public float SpearDamage {get; set;} = 1f;
		public float SlingDamage {get; set;} = 1f;

		public override AngelConfigSetting[] GetConfigSettings()
		{
			return new AngelConfigSetting[] 
			{
				new AngelConfigSetting("AimDifficulty", "Aim Difficulty", () => {return AimDifficulty.ToString();}, (args) => 
				{
					if (args.Length > 0)
					{
						float? param = args.PopFloat();

						if (param.HasValue)
						{
							AimDifficulty = GameMath.Clamp(param.Value, 0f, 1000f);
							return AimDifficulty.ToString();
						}
						else 
						{
							throw new AngelConfigArgumentException("Parameter is not a proper float");
						}
					}
					else
					{
						throw new AngelConfigArgumentException("1 float parameter required");
					}
				}),

				new AngelConfigSetting("AllDamage", null, () => {throw new AngelConfigArgumentException("This command is only a shortcut. Please use ArrowDamage, SpearDamage, and SlingDamage instead");}, (args) => 
				{
					if (args.Length > 0)
					{
						float? param = args.PopFloat();

						if (param.HasValue)
						{
							ArrowDamage = GameMath.Clamp(param.Value, 0f, 1000f);
							SpearDamage = GameMath.Clamp(param.Value, 0f, 1000f);
							SlingDamage = GameMath.Clamp(param.Value, 0f, 1000f);
							return GameMath.Clamp(param.Value, 0f, 1000f).ToString();
						}
						else 
						{
							throw new AngelConfigArgumentException("Parameter is not a proper float");
						}
					}
					else
					{
						throw new AngelConfigArgumentException("1 float parameter required");
					}
				}),
			};
		}
	}
}