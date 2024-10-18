using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.Events;

namespace AtmosphericFx
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class WindowManager : MonoBehaviour
	{
		public static WindowManager Instance { get; private set; }

		ApplicationLauncherButton appButton;
		Rect windowPosition = new Rect(0, 100, 300, 100);
		Rect infoWindowPosition = new Rect(300, 100, 300, 100);

		bool uiHidden = false;
		bool appToggle = false;

		// override toggle values
		public bool tgl_Hdr = false;
		public bool tgl_UseColliders = false;
		public bool tgl_DisableParticles = false;

		public bool tgl_SpeedMethod = true;

		// timer
		float reloadBtnTime = 0f;
		
		public void Awake()
		{
			Instance = this;

			InitializeDefaultValues();
		}

		public void Start()
		{
			appButton = ApplicationLauncher.Instance.AddModApplication(
				OnApplicationTrue, 
				OnApplicationFalse, 
				null, null, null, null, 
				ApplicationLauncher.AppScenes.FLIGHT, 
				AssetLoader.Instance.iconTexture
			);

			GameEvents.onHideUI.Add(OnHideUi);
			GameEvents.onShowUI.Add(OnShowUi);
		}

		/// <summary>
		/// Initializes the default values of the toggles with the values loaded from the cfg file
		/// </summary>
		public void InitializeDefaultValues()
		{
			tgl_Hdr = ConfigManager.Instance.modSettings.hdrOverride;
			tgl_DisableParticles = ConfigManager.Instance.modSettings.disableParticles;
		}

		public void OnDestroy()
		{
			// remove everything associated with the thing

			ApplicationLauncher.Instance.RemoveModApplication(appButton);

			GameEvents.onHideUI.Remove(OnHideUi);
			GameEvents.onShowUI.Remove(OnShowUi);
		}

		void OnApplicationTrue()
		{
			appToggle = true;
		}

		void OnApplicationFalse()
		{
			appToggle = false;
		}

		void OnHideUi()
		{
			uiHidden = true;
		}

		void OnShowUi()
		{
			uiHidden = false;
		}

		public void Update()
		{
			
		}

		public void OnGUI()
		{
			if (uiHidden || !appToggle || FlightGlobals.ActiveVessel == null) return;

			windowPosition = GUILayout.Window(416, windowPosition, OnWindow, "Atmospheric Effects Configuration");
			infoWindowPosition = GUILayout.Window(410, infoWindowPosition, OnInfoWindow, "Atmospheric Effects Info");
		}

		/// <summary>
		/// Config and override window
		/// </summary>
		void OnWindow(int id)
		{
			// init
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (!EventManager.fxInstances.TryGetValue(vessel.id, out AtmoFxModule fxModule)) return;

			// drawing
			GUILayout.BeginVertical();

			bool canReload = (Time.realtimeSinceStartup - reloadBtnTime) > 1f;
			if (GUILayout.Button("Reload Vessel") && canReload)
			{
				fxModule.DoFullReload();
				reloadBtnTime = Time.realtimeSinceStartup;
			}
			if (DrawConfigField("HDR Override", ref tgl_Hdr)) CameraManager.Instance.OverrideHDR(tgl_Hdr);
			if (DrawConfigField("Disable particles", ref tgl_DisableParticles)) ConfigManager.Instance.modSettings.disableParticles = tgl_DisableParticles;
			DrawConfigField("Speed method", ref tgl_SpeedMethod);
			if (GUILayout.Button("Save overrides")) ConfigManager.Instance.SaveModSettings();

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Info window
		/// </summary>
		void OnInfoWindow(int id)
		{
			// init
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (!EventManager.fxInstances.TryGetValue(vessel.id, out AtmoFxModule fxModule)) return;

			// drawing
			GUILayout.BeginVertical();

			GUILayout.Label($"Current vessel loaded? {fxModule.isLoaded}");
			GUILayout.Label($"Mainbody atmo? {fxModule.bodyHasAtmo}");
			GUILayout.Label($"Mod version: beta-{Versioning.Version}. This is a testing-only build.");
			GUILayout.Label($"All assets loaded? {AssetLoader.Instance.allAssetsLoaded}");
			GUILayout.Space(20);

			GUILayout.Label($"Active vessel is {vessel.vesselName}");
			GUILayout.Label($"Vessel radius is {fxModule.fxVessel.vesselBoundRadius}");
			GUILayout.Label($"Effect length multiplier is {fxModule.fxVessel.lengthMultiplier}");
			GUILayout.Label($"Final entry speed is {fxModule.GetAdjustedEntrySpeed()}");
			GUILayout.Space(20);

			GUILayout.Label($"AeroFX scalar is {fxModule.AeroFX.FxScalar}");
			GUILayout.Label($"AeroFX state is {fxModule.AeroFX.state}");
			GUILayout.Label($"AeroFX airspeed is {fxModule.AeroFX.airSpeed}");
			GUILayout.Space(20);

			GUILayout.Label($"Current config is {fxModule.currentBody.bodyName}");
			GUILayout.Space(20);

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Draws a config field with a toggle switch and a button which applies it
		/// </summary>
		/// <param name="label">Label to show</param>
		/// <param name="tgl">The value of the toggle switch</param>
		/// <returns>The apply button state</returns>
		bool DrawConfigField(string label, ref bool tgl)
		{
			GUILayout.BeginHorizontal();
				tgl = GUILayout.Toggle(tgl, label);
				bool result = GUILayout.Button("Apply override");
			GUILayout.EndHorizontal();

			return result;
		}
	}
}
