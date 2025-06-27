using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FireflyAPI
{
	public static class FireflyAPIManager
	{
		public static bool IsFireflyInstalled { get; set; }
		public static Assembly FireflyAssembly { get; set; }
		public static IConfigManager ConfigManager { get; set; }

		static FireflyAPIManager()
		{
			IsFireflyInstalled = AssemblyLoader.loadedAssemblies.Contains("Firefly");
			if (!IsFireflyInstalled) return;

			FireflyAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name.Equals("Firefly", StringComparison.OrdinalIgnoreCase)).assembly;

			Type mgrType = FireflyAssembly.GetType("Firefly.ConfigManager");
			object mgr = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
			ConfigManager = mgr as IConfigManager;
		}

		public static bool TryFindModule(Vessel vessel, out IFireflyModule module)
		{
			if (vessel == null)
			{
				module = null;
				return false;
			}

			module = vessel.FindVesselModuleImplementing<IFireflyModule>();
			return module == null;
		}
	}
}
