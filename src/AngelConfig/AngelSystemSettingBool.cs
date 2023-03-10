using System;
using System.Reflection;
using System.Linq;

namespace AngelConfig
{	
	public class AngelConfigSettingBool : AngelConfigSetting
	{
		private static string[] trueAliases = new string[] {"on", "yes", "true", "1"};
		private static string[] falseAliases = new string[] {"off", "no", "false", "0"};

		public AngelConfigSettingBool(string code, string name) : base(code, name, null, null)
		{
			Get = () => {return Config.GetType().GetProperty(code).GetValue(Config).ToString();};
			Set = (args) => 
				{
					if (!(args.Length > 0)) throw new AngelConfigArgumentException("1 bool parameter required");

					string param = args.PopWord();

					if (String.IsNullOrEmpty(param)) throw new AngelConfigArgumentException("Parameter cannot be empty");

					param = param.ToLower();

					bool boolValue;

					if (trueAliases.Contains(param)) boolValue = true;
					else if (falseAliases.Contains(param)) boolValue = false;
					else throw new AngelConfigArgumentException("Parameter is invalid. Choose 'on' or 'off' (or 'yes/no', 'true/false', '1/0')");

					PropertyInfo propertyInfo = Config.GetType().GetProperty(code);

					propertyInfo.SetValue(Config, boolValue);
					return propertyInfo.GetValue(Config).ToString();
				};
		}
	}
}