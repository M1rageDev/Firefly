using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FireflyAPI
{
	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class FireflyAPIManager : MonoBehaviour
    {
        public static FireflyAPIManager Instance { get; private set; }
		
		Assembly fireflyAssembly;
		IConfigManager configManager;

		public void Awake()
		{
			Instance = this;
		}

		public static bool IsFireflyInstalled()
		{
			return AssemblyLoader.loadedAssemblies.Contains("Firefly");
		}

		public static bool TryFindModule(Vessel vessel, out IFireflyModule module)
		{
			if (IsFireflyInstalled() && vessel != null)
			{
				module = vessel.FindVesselModuleImplementing<IFireflyModule>();
				return true;
			}

			module = null;
			return false;
		}

		public static Assembly GetFireflyAssembly()
		{
			if (Instance.fireflyAssembly == null)
			{
				Instance.fireflyAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name.Equals("Firefly", StringComparison.OrdinalIgnoreCase)).assembly;
			}

			return Instance.fireflyAssembly;
		}

		public static IConfigManager GetConfigManager()
		{
			if (Instance.configManager == null)
			{
				Type mgrType = GetFireflyAssembly().GetType("Firefly.ConfigManager");
				object mgr = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
				Instance.configManager = mgr as IConfigManager;
			}

			return Instance.configManager;
		}
	}
}
