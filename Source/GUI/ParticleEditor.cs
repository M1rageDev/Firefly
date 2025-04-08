using UnityEngine;
using System.IO;

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

		GuiUtils.ConfirmingButton ui_deleteButton = new GuiUtils.ConfirmingButton("Delete selected config");
		GuiUtils.ConfirmingButton ui_saveButton = new GuiUtils.ConfirmingButton("Save all to file");

		Popup ui_createPopup;

		public ParticleEditor() : base("Particle Editor")
		{
			windowRect = new Rect(900f, 100f, 600f, 300f);

			Instance = this;

			ui_createPopup = new Popup("Create from preset", new Vector2(300f, 100f), CreatePopupCallback, CreatePopupDraw);
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

			GUILayout.Label("Select a config:", GUILayout.Width(300f));
			DrawConfigSelector();

			GUILayout.Space(20f);

			if (GUILayout.Button("Create new config from preset", GUILayout.Width(300f)))
			{
				// initialize and show popup
				ui_createPopup.customData.Clear();
				ui_createPopup.customData["listPosition"] = Vector2.zero;
				ui_createPopup.customData["newName"] = "";
				ui_createPopup.windowRect.position = this.windowRect.position + new Vector2(0f, this.windowRect.height);  // position below the window
				ui_createPopup.Show();
			}
			if (ui_saveButton.Draw(Time.time, GUILayout.Width(300f)))
			{
				SaveAllToCfg();
			}
			if (ui_deleteButton.Draw(Time.time, GUILayout.Width(300f)))
			{
				DeleteSelected();
			}

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
				GuiUtils.DrawStringInput("Unity bundle prefab name", ref ui_prefabName, GUILayout.Width(300f));
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

		private void CreatePopupDraw()
		{
			GUILayout.Label("Pick a preset particle system to create a new config from");

			// if selected something, tell what
			if (ui_createPopup.customData.ContainsKey("selection")) 
				GUILayout.Label($"Currently selected: {ui_createPopup.customData["selection"]}");

			// selection list
			ui_createPopup.customData["listPosition"] = GUILayout.BeginScrollView((Vector2)ui_createPopup.customData["listPosition"], GUILayout.Width(300f));
			foreach (string key in ConfigManager.Instance.particleConfigs.Keys)
			{
				if (GUILayout.Button(key))
				{
					ui_createPopup.customData["selection"] = key;
				}
			}
			GUILayout.EndScrollView();

			// new name
			string newConfigName = (string)ui_createPopup.customData["newName"];
			GuiUtils.DrawStringInput("New config name", ref newConfigName);
			ui_createPopup.customData["newName"] = newConfigName;
		}

		private void CreatePopupCallback(Popup.CallbackType type)
		{
			// if pressed ok, create a new config from the selected preset
			if (type == Popup.CallbackType.Ok)
			{
				CreateFromPreset((string)ui_createPopup.customData["selection"], (string)ui_createPopup.customData["newName"]);
			}
		}

		// updates the ui values from the current config
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

		// updates the current config with the ui values
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

		void CreateFromPreset(string presetKey, string newName)
		{
			// create a new config from the preset
			ParticleConfig preset = new ParticleConfig(ConfigManager.Instance.particleConfigs[presetKey]);
			preset.name = newName;

			// add to configman
			ConfigManager.Instance.particleConfigs[newName] = preset;

			// set as current
			currentConfig = preset;
			currentConfigName = newName;
			UpdateUiValues();
		}

		void DeleteSelected()
		{
			if (currentConfig == null) return;

			// remove from config manager
			ConfigManager.Instance.particleConfigs.Remove(currentConfigName);

			// reset
			currentConfig = null;
			currentConfigName = "";
			ConfigManager.Instance.RefreshTextureList();
			AssetLoader.Instance.ReloadAssets();
			fxModule?.ReloadVessel();
		}

		void SaveAllToCfg()
		{
			string path = Path.Combine(KSPUtil.ApplicationRootPath, ConfigManager.ParticleConfigPath);

			// create a dummy parent node
			ConfigNode parent = new ConfigNode("ATMOFX_PARTICLES");

			// create the actual node
			ConfigNode node = new ConfigNode("ATMOFX_PARTICLES");
			node.AddValue("name", ConfigManager.DefaultParticleNodeName);

			foreach (string key in ConfigManager.Instance.particleConfigs.Keys)
			{
				ParticleConfig config = ConfigManager.Instance.particleConfigs[key];

				// create a separate node for each particle system
				ConfigNode configNode = new ConfigNode(config.name);
				config.SaveToNode(configNode);
				node.AddNode(configNode);
			}

			// add to parent and save
			parent.AddNode(node);
			parent.Save(path);

			ScreenMessages.PostScreenMessage("Saved all configs to file", 5f, ScreenMessageStyle.UPPER_CENTER);
			Logging.Log($"Saved particle config {ConfigManager.ParticleConfigPath}");
		}

		public override void Show()
		{
			base.Show();

			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			fxModule = vessel.FindVesselModuleImplementing<AtmoFxModule>();
		}
	}
}
