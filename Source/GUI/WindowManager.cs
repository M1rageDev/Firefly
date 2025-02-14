using KSP.UI.Screens;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class WindowManager : MonoBehaviour
	{
		public static WindowManager Instance { get; private set; }

		ApplicationLauncherButton appButton = null;
		Rect windowPosition = new Rect(0, 100, 300, 100);

		public bool uiHidden = false;
		bool appToggle = false;

		// override toggle values
		public bool tgl_EffectToggle = true;

		// timer
		float reloadBtnTime = 0f;

		// effect editor
		EffectEditor effectEditor;
		Rect effectEditorPosition = new Rect(300, 100, 300, 100);
		bool effectEditorActive = false;

		// other windows
		ErrorListWindow errorListWindow;
		public StockEffectsWindow stockEffectsWindow;
		
		public void Awake()
		{
			Instance = this;

			effectEditor = new EffectEditor();
			errorListWindow = new ErrorListWindow();
			stockEffectsWindow = new StockEffectsWindow();
		}

		public void Start()
		{
			// only create the app if there are no serious errors
			if (ModLoadError.SeriousErrorCount < 1)
			{
				appButton = ApplicationLauncher.Instance.AddModApplication(
					OnApplicationTrue,
					OnApplicationFalse,
					null, null, null, null,
					ApplicationLauncher.AppScenes.FLIGHT,
					AssetLoader.Instance.loadedTextures["Icon"]
				);
			}

			GameEvents.onHideUI.Add(OnHideUi);
			GameEvents.onShowUI.Add(OnShowUi);

			if (GameSettings.AERO_FX_QUALITY > 0)
			{
				// inform users about conflicting effects when instantiating this class
				stockEffectsWindow.windowActive = true;
			}
		}

		public void OnDestroy()
		{
			// remove everything associated with the thing

			if (appButton != null) ApplicationLauncher.Instance.RemoveModApplication(appButton);

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
			// draw the error list window, even if the app is closed
			if (uiHidden || FlightGlobals.ActiveVessel == null) return;
			if (errorListWindow.windowActive) errorListWindow.windowPosition = GUILayout.Window(899, errorListWindow.windowPosition, errorListWindow.Gui, "Firefly error list");
			if (stockEffectsWindow.windowActive) stockEffectsWindow.windowPosition = GUILayout.Window(8991, stockEffectsWindow.windowPosition, stockEffectsWindow.Gui, "Firefly stock effects warning");

			// if the app is open, draw the rest of the windows
			if (!appToggle) return;

			windowPosition = GUILayout.Window(416, windowPosition, OnWindow, $"Firefly {Versioning.Version(this)}");

			// draw the effect editor and its dialogs
			if (effectEditorActive) effectEditorPosition = GUILayout.Window(512, effectEditorPosition, effectEditor.Gui, "Effect editor");
			if (effectEditorActive) effectEditor.colorPicker.Gui();
			if (effectEditorActive) effectEditor.createConfigPopup.Gui();
		}

		/// <summary>
		/// Window
		/// </summary>
		void OnWindow(int id)
		{
			if (ModLoadError.SeriousErrorCount > 0)
			{
				GUILayout.BeginVertical();
				GUILayout.Label("The mod detected at least one serious error while loading. Cannot show menu.");
				if (GUILayout.Button("Show error list")) errorListWindow.windowActive = true;
				GUILayout.EndVertical();
				GUI.DragWindow();
			}

			// effect editor
			if (GUILayout.Button($"{(effectEditorActive ? "Close" : "Open")} config editor"))
			{
				effectEditorActive = !effectEditorActive;
				if (effectEditorActive) effectEditor.Open();
				else effectEditor.Close();
			}

			// settings
			DrawSettings();
			GUILayout.Space(40);

			// init vessel and module
			Vessel vessel = FlightGlobals.ActiveVessel;
			var fxModule = vessel.FindVesselModuleImplementing<AtmoFxModule>();
			if (fxModule == null) return;

			if (!fxModule.isLoaded)
			{
				GUILayout.BeginVertical();

				if (!vessel.mainBody.atmosphere)
				{
					GUILayout.Label("Current body does not have an atmosphere, FX are unloaded.");
				}
				else if (vessel.altitude > vessel.mainBody.atmosphereDepth)
				{
					GUILayout.Label("Ship is not in atmosphere, FX are unloaded.");
				} else
				{
					GUILayout.Label("FX are not loaded for the active vessel");
				}
				
				GUILayout.Label("Not showing info and quick action sections");
				GUILayout.EndVertical();
				GUI.DragWindow();
				return;
			}

			if (fxModule.doEffectEditor)
			{
				GUILayout.Label("Effect editor is open.");
			}

			// info
			DrawInfo(vessel, fxModule);
			GUILayout.Space(40);

			// quick actions
			DrawQuickActions(fxModule);

			// end
			GUI.DragWindow();
		}

		/// <summary>
		/// Mod and vessel info
		/// </summary>
		/// <param name="fxModule"></param>
		void DrawInfo(Vessel vessel, AtmoFxModule fxModule)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Info:");

			GUILayout.Label($"All assets loaded? {AssetLoader.Instance.allAssetsLoaded}");
			GUILayout.Label($"Current config is {fxModule.currentBody.bodyName}");
			GUILayout.Label($"Active vessel is {vessel.vesselName}");
			GUILayout.Label($"Vessel radius is {fxModule.fxVessel.vesselBoundRadius}");
			if (!fxModule.doEffectEditor)
			{
				GUILayout.Label($"Entry strength is {fxModule.GetAdjustedEntrySpeed()}");
			}


			GUILayout.EndVertical();
		}

		/// <summary>
		/// Config and override
		/// </summary>
		void DrawSettings()
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Settings:");
			GUILayout.Label("Fields that need a reload to update are marked with *");

			// draw config fields
			for (int i = 0; i < ModSettings.I.fields.Count; i++)
			{
				KeyValuePair<string, ModSettings.Field> field = ModSettings.I.fields.ElementAt(i);

				if (field.Value.valueType == ModSettings.ValueType.Boolean) GuiUtils.DrawConfigFieldBool(field.Key, ModSettings.I.fields);
				else if (field.Value.valueType == ModSettings.ValueType.Float) GuiUtils.DrawConfigFieldFloat(field.Key, ModSettings.I.fields);
			}

			if (GUILayout.Button("Save overrides to file")) SettingsManager.Instance.SaveModSettings();

			GUILayout.EndVertical();
		}
		
		/// <summary>
		/// Quick actions
		/// </summary>
		void DrawQuickActions(AtmoFxModule fxModule)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Quick actions:");

			bool canReload = (Time.realtimeSinceStartup - reloadBtnTime) > 1f;
			if (GUILayout.Button("Reload Vessel") && canReload)
			{
				fxModule.ReloadVessel();
				reloadBtnTime = Time.realtimeSinceStartup;
			}
			if (GUILayout.Button($"Toggle effects {(tgl_EffectToggle ? "(TURN OFF)" : "(TURN ON)")}")) tgl_EffectToggle = !tgl_EffectToggle;
			if (GUILayout.Button($"Toggle debug vis {(fxModule.debugMode ? "(TURN OFF)" : "(TURN ON)")}")) fxModule.debugMode = !fxModule.debugMode;
			if (Versioning.IsDev && GUILayout.Button("Reload assetbundle")) AssetLoader.Instance.ReloadAssets();

			GUILayout.EndVertical();
		}
	}
}
