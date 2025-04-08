using System.Collections.Generic;
using UnityEngine;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	class ErrorManager : MonoBehaviour
    {
		public static ErrorManager Instance { get; private set; }

		public List<ModLoadError> errorList = new List<ModLoadError>();
		public List<ModLoadError> seriousErrors = new List<ModLoadError>();
		public bool anyInstallErrors = false;

		public ErrorManager()
		{
			Instance = this;
		}

		public void RegisterError(ModLoadError error)
		{
			errorList.Add(error);

			// serious errors
			if (error.isSerious)
			{
				seriousErrors.Add(error);
			}

			// install errors
			if (error.cause == ModLoadError.ProbableCause.IncorrectInstall)
			{
				anyInstallErrors = true;
			}
		}
	}
}
