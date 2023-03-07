using System;
using Vintagestory.API.MathTools;
using System.Reflection;

using Cairo;

namespace AngelConfig
{	
	public class AngelConfigSettingFloat : AngelConfigSetting
	{
		public AngelConfigSettingFloat(string code, string name, float minValue = float.MinValue, float maxValue = float.MaxValue) : base(code, name, null, null)
		{
			Get = () => {return Config.GetType().GetProperty(code).GetValue(Config).ToString();};
			Set = (args) => 
				{
					if (!(args.Length > 0)) throw new AngelConfigArgumentException("1 float parameter required");

					float? param = args.PopFloat();

					if (!param.HasValue) throw new AngelConfigArgumentException("Parameter is not a proper float");
						
					PropertyInfo propertyInfo = Config.GetType().GetProperty(code);

					propertyInfo.SetValue(Config, GameMath.Clamp(param.Value, minValue, maxValue));
					return propertyInfo.GetValue(Config).ToString();
				};
		}
	}
}