using UnityEngine;

namespace Firefly.GUI
{
    class FireflyWindow : Window
    {
		// toggling effects
		public bool tgl_EffectToggle = true;

		// timer for clicking the reload button
		float reloadBtnTime = 0f;

		public FireflyWindow() : base("")
		{
			title = $"Firefly {Versioning.Version(this)}";
			windowRect = new Rect(0, 100, 300, 100);

			Show();
		}

		public override void Draw(int id)
		{
			// effect editor open/close
			if (GUILayout.Button($"{(EffectEditor.Instance.show ? "Close" : "Open")} config editor"))
			{
				if (!EffectEditor.Instance.show) EffectEditor.Instance.Show();
				else EffectEditor.Instance.Hide();
			}

			// particle editor open/close
			if (GUILayout.Button($"{(ParticleEditor.Instance.show ? "Close" : "Open")} particle editor"))
			{
				if (!ParticleEditor.Instance.show) ParticleEditor.Instance.Show();
				else ParticleEditor.Instance.Hide();
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
				}
				else
				{
					GUILayout.Label("FX are not loaded for the active vessel");
				}

				GUILayout.Label("Not showing info and quick action sections");
				GUILayout.EndVertical();
				UnityEngine.GUI.DragWindow();
				return;
			}

			if (fxModule.overridePhysics)
			{
				GUILayout.Label("Physics override on.");
				if (EffectEditor.Instance.show) GUILayout.Label("Effect editor open.");
			}

			// info
			DrawInfo(vessel, fxModule);
			GUILayout.Space(40);

			// quick actions
			DrawQuickActions(fxModule);

			// end
			UnityEngine.GUI.DragWindow();
		}

		void DrawInfo(Vessel vessel, AtmoFxModule fxModule)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Info:");

			GUILayout.Label($"All assets loaded? {AssetLoader.Instance.allAssetsLoaded}");
			GUILayout.Label($"Current config is {fxModule.currentBody.bodyName}");
			GUILayout.Label($"Active vessel is {vessel.vesselName}");
			GUILayout.Label($"Vessel radius is {fxModule.fxVessel.vesselBoundRadius}");
			if (!fxModule.overridePhysics)
			{
				GUILayout.Label($"Entry strength is {fxModule.GetEntryStrength()}");
				GUILayout.Label($"Dynamic pressure [kPa] {vessel.dynamicPressurekPa}");
			}

			GUILayout.EndVertical();
		}

		void DrawSettings()
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Settings:");
			GUILayout.Label("Fields that need a reload to update are marked with *");

			// draw config fields
			foreach (string key in ModSettings.I.fields.Keys)
			{
				ModSettings.Field field = ModSettings.I.fields[key];

				if (field.valueType == ModSettings.ValueType.Boolean) GuiUtils.DrawConfigFieldBool(key, ModSettings.I.fields);
				else if (field.valueType == ModSettings.ValueType.Float) GuiUtils.DrawConfigFieldFloat(key, ModSettings.I.fields);
			}

			if (GUILayout.Button("Save overrides to file")) SettingsManager.Instance.SaveModSettings();

			GUILayout.EndVertical();
		}

		void DrawQuickActions(AtmoFxModule fxModule)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Quick actions:");

			// check if at least a second has passed since last reload click
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
