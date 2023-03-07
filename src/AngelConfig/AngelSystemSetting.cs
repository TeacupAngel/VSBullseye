using System;
using Vintagestory.API.Common;

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
		// Replace with proper delegates
		public System.Func<string> Get {get; protected set;}
		public System.Func<CmdArgs, string> Set {get; protected set;}

		public AngelConfigBase Config {get; set;}
	}
}