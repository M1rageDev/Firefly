using UnityEngine;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class EventManager : MonoBehaviour
	{
		public void Start()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			GameEvents.onVesselPartCountChanged.Add(OnVesselPartCountChanged);
			GameEvents.onVesselSOIChanged.Add(OnVesselSOIChanged);
			GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);
		}

		public void OnDestroy()
		{
			GameEvents.onVesselPartCountChanged.Remove(OnVesselPartCountChanged);
			GameEvents.onVesselSOIChanged.Remove(OnVesselSOIChanged);
			GameEvents.OnGameSettingsApplied.Remove(OnGameSettingsApplied);
		}

		/// <summary>
		/// Fires everytime a vessel is modified, sends a reload event
		/// </summary>
		void OnVesselPartCountChanged(Vessel vessel)
		{
			Logging.Log($"Modified vessel {vessel.name}");

			var module = vessel.FindVesselModuleImplementing<AtmoFxModule>();

			if (module != null) module.OnVesselPartCountChanged();
			else Logging.Log("FX instance not registered");
		}

		/// <summary>
		/// Fires everytime a vessel changes it's SOI
		/// </summary>
		void OnVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> action)
		{
			var module = action.host.FindVesselModuleImplementing<AtmoFxModule>();

			if (module != null) module.OnVesselSOIChanged(action.to);
		}

		void OnGameSettingsApplied()
		{
			if (GameSettings.AERO_FX_QUALITY > 0)
			{
				// user or something else changed the fx quality to bigger than 0
				// this means that the stock and Firefly effects will get mixed
				// make sure to show a message to the user informing them of thath
				GUI.WindowManager.Instance.stockEffectsWindow.Show();
			}
		}
	}
}
