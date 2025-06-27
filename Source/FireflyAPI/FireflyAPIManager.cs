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

		static FireflyAPIManager()
		{
			IsFireflyInstalled = AssemblyLoader.loadedAssemblies.Contains("Firefly");
			if (!IsFireflyInstalled) return;

			FireflyAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name.Equals("Firefly", StringComparison.OrdinalIgnoreCase)).assembly;
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
