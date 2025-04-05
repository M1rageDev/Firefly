using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static Targeting;

namespace Firefly.GUI
{
    class ParticleEditor : Window
	{
		public static ParticleEditor Instance { get; private set; }

		AtmoFxModule fxModule = null;

		string currentConfigName = "";
		ParticleConfig currentConfig = null;

		// ui values
		Vector2 ui_configListPosition = Vector2.zero;

		string ui_prefabName = "";
		string ui_mainTexPath = "";
		string ui_emissionTexPath = "";
		GuiUtils.UiObjectInput<float> ui_offset = new GuiUtils.UiObjectInput<float>(0f);
		bool ui_useHalfOffset = false;
		GuiUtils.UiObjectInput<FloatPair> ui_rateRange = new GuiUtils.UiObjectInput<FloatPair>(new FloatPair(), 2);
		GuiUtils.UiObjectInput<FloatPair> ui_lifetimeRange = new GuiUtils.UiObjectInput<FloatPair>(new FloatPair(), 2);
		GuiUtils.UiObjectInput<FloatPair> ui_velocityRange = new GuiUtils.UiObjectInput<FloatPair>(new FloatPair(), 2);

		public ParticleEditor() : base("Particle Editor")
		{
			windowRect = new Rect(900f, 100f, 600f, 300f);

			Instance = this;
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Use the Effect Editor's sim sliders to make the particles show");

			// split editor into 2 parts
			GUILayout.BeginHorizontal();

			// draw left part
			DrawLeftEditor();

			// draw right part
			DrawRightEditor();

			// margin
			GUILayout.Space(20f);

			// end window
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			UnityEngine.GUI.DragWindow();
		}

		void DrawLeftEditor()
		{
			GUILayout.BeginVertical();

			DrawConfigSelector();

			GUILayout.EndVertical();
		}

		void DrawRightEditor()
		{
			GUILayout.BeginVertical();

			DrawConfigEditor();	

			GUILayout.EndVertical();
		}

		void DrawConfigSelector()
		{
			GUILayout.Label("Select a config:", GUILayout.Width(300f));

			ui_configListPosition = GUILayout.BeginScrollView(ui_configListPosition, GUILayout.Width(300f));
			
			foreach (string key in ConfigManager.Instance.particleConfigs.Keys)
			{
				if (GUILayout.Button(key))
				{
					currentConfig = ConfigManager.Instance.particleConfigs[key];
					currentConfigName = key;
					UpdateUiValues();
				}
			}

			GUILayout.EndScrollView();
		}

		void DrawConfigEditor()
		{
			if (currentConfig != null)
			{
				GUILayout.Label($"Current config: {currentConfigName}", GUILayout.Width(300f));

				// draw all configuration options
				GuiUtils.DrawStringInput("Prefab name", ref ui_prefabName, GUILayout.Width(300f));
				GUILayout.Space(20f);
				GuiUtils.DrawStringInput("Main texture", ref ui_mainTexPath);
				GuiUtils.DrawStringInput("Emission texture", ref ui_emissionTexPath);
				GUILayout.Space(20f);
				GuiUtils.DrawFloatInput("Emitter offset", ref ui_offset);
				GuiUtils.DrawBoolInput("Use half offset", ref ui_useHalfOffset);
				GUILayout.Space(20f);
				GuiUtils.DrawFloatPairInput("Rate range", ref ui_rateRange);
				GuiUtils.DrawFloatPairInput("Lifetime range", ref ui_lifetimeRange);
				GuiUtils.DrawFloatPairInput("Velocity range", ref ui_velocityRange);

				// saves everything to config and to ConfigManager
				if (GUILayout.Button("Apply"))
				{
					ApplyConfigValues();
					ConfigManager.Instance.particleConfigs[currentConfigName] = currentConfig;
					fxModule?.ReloadVessel();
				}
			}
		}

		void UpdateUiValues()
		{
			ui_prefabName = currentConfig.prefab;
			ui_mainTexPath = currentConfig.mainTexture;
			ui_emissionTexPath = currentConfig.emissionTexture;
			ui_offset.Overwrite(currentConfig.offset);
			ui_useHalfOffset = currentConfig.useHalfOffset;
			ui_rateRange.Overwrite(currentConfig.rate);
			ui_lifetimeRange.Overwrite(currentConfig.lifetime);
			ui_velocityRange.Overwrite(currentConfig.velocity);
		}

		void ApplyConfigValues()
		{
			currentConfig.prefab = ui_prefabName;
			currentConfig.mainTexture = ui_mainTexPath;
			currentConfig.emissionTexture = ui_emissionTexPath;
			currentConfig.offset = ui_offset.GetValue();
			currentConfig.useHalfOffset = ui_useHalfOffset;
			currentConfig.rate = ui_rateRange.GetValue();
			currentConfig.lifetime = ui_lifetimeRange.GetValue();
			currentConfig.velocity = ui_velocityRange.GetValue();

			ConfigManager.Instance.RefreshTextureList();
			AssetLoader.Instance.ReloadAssets();
		}

		public override void Show()
		{
			base.Show();

			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			fxModule = vessel.FindVesselModuleImplementing<AtmoFxModule>();
		}

		public override void Hide()
		{
			base.Hide();
		}
	}
}
