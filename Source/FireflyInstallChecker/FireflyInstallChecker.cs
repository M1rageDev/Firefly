using UnityEngine;

namespace FireflyInstallChecker
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
	class FireflyInstallChecker : MonoBehaviour
    {
        public void Awake()
        {
			if (!AssemblyLoader.loadedAssemblies.Contains("FireflyAPI"))
            {
				// no api???
				Debug.LogError("[FireflyInstallChecker] FireflyAPI not found! Please ensure that FireflyAPI is installed correctly.");

				PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "Firefly Install Error", 
					"<color=red>Incorrect Firefly install</color>",
					"FireflyAPI not found! Please ensure that FireflyAPI is installed correctly.",
					"OK",
					true, HighLogic.UISkin, true, string.Empty
				);
			}
		}
    }
}
