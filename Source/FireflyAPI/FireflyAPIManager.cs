using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FireflyAPI
{
	public static class FireflyAPIManager
	{
		static bool? _isFireflyInstalled = null;

		public static bool IsFireflyInstalled()
		{
			if (_isFireflyInstalled == null)
			{
				_isFireflyInstalled = AssemblyLoader.loadedAssemblies.Contains("Firefly");
			}

			return _isFireflyInstalled.Value;
		}

		public static bool TryFindModule(Vessel vessel, out IFireflyModule module)
		{
			module = vessel.FindVesselModuleImplementing<IFireflyModule>();

			return module == null;
		}

		public static Assembly FireflyAssembly
		{
			get
			{
				if (_fireflyAssembly == null)
				{
					_fireflyAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name.Equals("Firefly", StringComparison.OrdinalIgnoreCase)).assembly;
				}

				return _fireflyAssembly;
			}
		}
		static Assembly _fireflyAssembly;

		public static IConfigManager ConfigManager
		{
			get
			{
				if (_configManager == null)
				{
					Type mgrType = FireflyAssembly.GetType("Firefly.ConfigManager");
					object mgr = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
					_configManager = mgr as IConfigManager;
				}

				return _configManager;
			}
		}
		static IConfigManager _configManager;
	}
}
