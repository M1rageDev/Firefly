using UnityEngine;

namespace FireflyInstallChecker
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
	class FireflyInstallChecker : MonoBehaviour
    {
        public void Awake()
        {
			if (!AssemblyLoader.loadedAssemblies.Contains("Firefly") || !AssemblyLoader.loadedAssemblies.Contains("FireflyAPI"))
            {
				// no api???
				Debug.LogError("[FireflyInstallChecker] Firefly/FireflyAPI not found! Please ensure that Firefly AND Firefly API are installed correctly.");

				PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "Firefly Install Error", 
					"<color=red>Incorrect Firefly install</color>",
					"Firefly/FireflyAPI not found! Please ensure that Firefly AND Firefly API are installed correctly.",
					"Fuck right off",
					true, HighLogic.UISkin, true, string.Empty
				);
			}
		}
    }
}
