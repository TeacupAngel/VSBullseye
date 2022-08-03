using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
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
using Newtonsoft.Json;

using Cairo;

namespace AngelConfig
{	
	public class AngelConfigSetting
	{
		public AngelConfigSetting(string code, string name, System.Func<string> get, System.Func<CmdArgs, string> set)
		{
			Code = code;
			Name = name;
			Get = get;
			Set = set;
		}

		public string Code {get; protected set;}
		public string Name {get; protected set;}
		public System.Func<string> Get {get; protected set;}
		public System.Func<CmdArgs, string> Set {get; protected set;}

		public AngelConfigBase Config {get; set;}
	}

	public class AngelConfigSettingSimpleFloat : AngelConfigSetting
	{
		public AngelConfigSettingSimpleFloat(string code, string name, string propertyName, float minValue = float.MinValue, float maxValue = float.MaxValue) : base(code, name, null, null)
		{
			Get = () => {return Config.GetType().GetProperty(propertyName).GetValue(Config).ToString();};
			Set = (args) => 
				{
					if (args.Length > 0)
					{
						float? param = args.PopFloat();

						if (param.HasValue)
						{
						    PropertyInfo propertyInfo = Config.GetType().GetProperty(propertyName);

							propertyInfo.SetValue(Config, GameMath.Clamp(param.Value, minValue, maxValue));
							return propertyInfo.GetValue(Config).ToString();
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
				};
		}
	}
}