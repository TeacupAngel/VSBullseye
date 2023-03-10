using System;
using System.Reflection;

using Cairo;

namespace AngelConfig
{	
	public class AngelConfigSettingEnum<T> : AngelConfigSetting where T : struct, Enum
	{
		public AngelConfigSettingEnum(string code, string name) : base(code, name, null, null)
		{
			Get = () => {return Config.GetType().GetProperty(code).GetValue(Config).ToString();};
			Set = (args) => 
				{
					if (!(args.Length > 0)) throw new AngelConfigArgumentException("1 string parameter required");

					string param = args.PopWord();

					if (String.IsNullOrEmpty(param)) throw new AngelConfigArgumentException("Parameter cannot be empty");

					if (!Enum.TryParse<T>(param, true, out T parsedEnum)) throw new AngelConfigArgumentException($"Parameter is invalid. Choose one from: {String.Join(", ", Enum.GetNames(typeof(T)))}");

					PropertyInfo propertyInfo = Config.GetType().GetProperty(code);

					propertyInfo.SetValue(Config, parsedEnum);
					return propertyInfo.GetValue(Config).ToString();
				};
		}
	}
}