using System.Collections.Generic;
using UnityEngine;

namespace Firefly
{
	public class ModLoadError
	{
		public static string BadConfigAdvice = "Check if it has all the required values, and if it's formatted correctly.";
		public static string OutdatedConfigAdvice = "The config is outdated, this is likely caused by a planet pack author not having updated their configs to the latest standard.";
		public static string OutdatedFireflyAdvice = "The config is made for a newer version of Firefly, please update Firefly to the latest version.";

		public static int SeriousErrorCount = 0;

		public enum ProbableCause
		{
			IncorrectInstall,
			ConfigVersionMismatch,
			BadConfig,
			Other
		}

		public ProbableCause cause = ProbableCause.Other;
		public bool isSerious = false;
		public string sourcePath = "";
		public string description = "";

		public ModLoadError(ProbableCause cause, bool isSerious, string sourcePath, string description)
		{
			this.cause = cause;
			this.isSerious = isSerious;
			this.sourcePath = sourcePath;
			this.description = description;

			if (isSerious) SeriousErrorCount++;
		}
	}

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
