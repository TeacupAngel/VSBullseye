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
		// Replace with proper delegates
		public System.Func<string> Get {get; protected set;}
		public System.Func<CmdArgs, string> Set {get; protected set;}

		public AngelConfigBase Config {get; set;}
	}
}