using System;

namespace Launcher
{
	public class LauncherEntry
	{
		public static void Main(string[] args)
		{
			Console.WriteLine("Launcher: Running Vintagestory.exe");

			// Needed until this gets into production: https://github.com/dotnet/sdk/pull/30866
			args = new string[] {
				"--dataPath", Environment.GetEnvironmentVariable("VS_LAUNCH_DATAPATH"),
				"--addModPath", Environment.GetEnvironmentVariable("VS_LAUNCH_MODPATH"),
				"--playStyle", Environment.GetEnvironmentVariable("VS_LAUNCH_PLAYSTYLE"),
				"--openWorld", Environment.GetEnvironmentVariable("VS_LAUNCH_WORLDNAME"),
			};

			VintagestoryClientWindows.ClientWindows.Main(args);
		}
	}
}