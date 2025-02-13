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

		// error list window
		ErrorListWindow errorListWindow;
		
		public void Awake()
		{
			Instance = this;

			effectEditor = new EffectEditor();
			errorListWindow = new ErrorListWindow();
		}

		public void Start()
		{
			if (ModLoadError.SeriousErrorCount > 0)
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
			// effect editor
			if (GUILayout.Button($"{(effectEditorActive ? "Close" : "Open")} effect editor"))
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
				GUILayout.Label("FX are not loaded for the active vessel");
				GUILayout.Label("Not showing info and quick action sections");
				GUILayout.EndVertical();
				GUI.DragWindow();
				return;
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
			if (!fxModule.doEffectEditor) GUILayout.Label($"Entry strength is {fxModule.GetAdjustedEntrySpeed()}");

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
