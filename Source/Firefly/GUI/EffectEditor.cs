using System.Linq;
using UnityEngine;
using FireflyAPI;

namespace Firefly.GUI
{
	internal class CreateConfigPopup : Window
	{
		public delegate void popupSaveDelg();
		public popupSaveDelg onPopupSave;

		string[] bodyConfigs;

		// public values
		public string selectedName;
		public string selectedTemplate;

		// ui
		string ui_cfgName;
		Vector2 ui_bodyListPosition;
		int ui_bodyChoice = 0;

		public CreateConfigPopup() : base("New config")
		{
			windowRect = new Rect(900f, 100f, 300f, 300f);
		}

		public void Init(string[] bodyConfigs)
		{
			ui_cfgName = "NewBody";
			ui_bodyListPosition = Vector2.zero;
			ui_bodyChoice = 0;

			this.bodyConfigs = bodyConfigs;

			Show();
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();

			// name input
			GuiUtils.DrawStringInput("Config name", ref ui_cfgName);

			// config selector
			GUILayout.Label("Select a template config");
			DrawConfigSelector();

			// cancel/done controls
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Cancel")) Hide();
			if (GUILayout.Button("Done")) Done();
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();
			UnityEngine.GUI.DragWindow();
		}

		void DrawConfigSelector()
		{
			ui_bodyListPosition = GUILayout.BeginScrollView(ui_bodyListPosition, GUILayout.Width(300f), GUILayout.Height(125f));

			ui_bodyChoice = GUILayout.SelectionGrid(ui_bodyChoice, bodyConfigs, Mathf.Min(bodyConfigs.Length, 3));

			GUILayout.EndScrollView();
		}

		void Done()
		{
			selectedName = ui_cfgName;
			selectedTemplate = bodyConfigs[ui_bodyChoice];

			onPopupSave();

			Hide();
		}
	}

	internal class EffectEditor : Window
	{
		public static EffectEditor Instance { get; private set; }

		public Vector3 effectDirection = -Vector3.up;

		public BodyConfig config;

		string[] bodyConfigs;
		string currentBody;

		AtmoFxModule fxModule = null;

		// gui
		GuiUtils.ConfirmingButton ui_removeConfigBtn = new GuiUtils.ConfirmingButton("Remove selected config");
		GuiUtils.ConfirmingButton ui_saveConfigBtn = new GuiUtils.ConfirmingButton("Save selected to cfg file");

		Vector2 ui_bodyListPosition;
		int ui_bodyChoice;

		// dialog windows
		public ColorPickerWindow colorPicker;
		string currentlyPicking;

		public CreateConfigPopup createConfigPopup;	

		public EffectEditor() : base("Effect editor")
		{
			windowRect = new Rect(300, 100, 300, 100);
			Instance = this;

			bodyConfigs = ConfigManager.Instance.bodyConfigs.Keys.ToArray();

			colorPicker = new ColorPickerWindow(900, 100, Color.red);
			colorPicker.onApplyColor = OnApplyColor;

			createConfigPopup = new CreateConfigPopup();
			createConfigPopup.onPopupSave = OnPopupSave;
		}

		public override void Show()
		{
			base.Show();

			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			fxModule = vessel.FindVesselModuleImplementing<AtmoFxModule>();
			if (fxModule == null) return;

			// select main body
			string mainBody = vessel.mainBody.bodyName;

			if (bodyConfigs.Contains(mainBody))
			{
				// only select if a config for the body exists
				ui_bodyChoice = bodyConfigs.IndexOf(mainBody);
				currentBody = mainBody;
				config = new BodyConfig(ConfigManager.Instance.bodyConfigs[currentBody]);
			} else
			{
				// otherwise, go with the default
				ui_bodyChoice = 0;
				currentBody = "Default";
				config = new BodyConfig(ConfigManager.Instance.DefaultConfig);
			}

			ResetFieldText();

			// load effects
			fxModule.OverridePhysics = true;
			fxModule.ResetOverride();
			fxModule.OverrideEffectStrength = (float)ModSettings.I["strength_base"];
			fxModule.OverrideEffectState = 1f;
			fxModule.OverridenBy = "Effect editor";
			if (!fxModule.isLoaded) fxModule.CreateVesselFx();
			ApplyShipDirection();
		}

		public override void Hide()
		{
			base.Hide();

			fxModule.OverridePhysics = false;
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Note: the effect editor should not be open during normal gameplay.");

			// split editor into 2 parts
			GUILayout.BeginHorizontal();

			// draw left part
			DrawLeftEditor();

			// draw right part
			DrawRightEditor();

			// end window
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			UnityEngine.GUI.DragWindow();

			// apply stuff
			fxModule.OverrideBodyConfig = config;
			fxModule.OverrideEntryDirection = GetWorldDirection();

			// 3d
			if (fxModule == null || fxModule.fxVessel == null) return;
			Transform camTransform = fxModule.fxVessel.airstreamCamera.transform;
			if (!fxModule.debugMode) DrawingUtils.DrawArrow(camTransform.position, camTransform.forward, camTransform.right, camTransform.up, Color.cyan);
		}

		void DrawLeftEditor()
		{
			GUILayout.BeginVertical();

			// config create
			if (GUILayout.Button("Create new config") && !createConfigPopup.show) createConfigPopup.Init(bodyConfigs);
			if (ui_removeConfigBtn.Draw(Time.time) && currentBody != "Default") RemoveSelectedConfig();

			// body selection
			GUILayout.Label("Select a config:");
			DrawConfigSelector();
			GUILayout.Space(20);

			// sim configuration
			DrawSimConfiguration();
			GUILayout.Space(20);

			// bottom controls
			if (GUILayout.Button("Align effects to camera")) ApplyCameraDirection();
			if (GUILayout.Button("Align effects to ship")) ApplyShipDirection();
			if (ui_saveConfigBtn.Draw(Time.time)) SaveConfig();

			// end
			GUILayout.EndVertical();
		}

		void DrawRightEditor()
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Body configuration:");

			// body configuration
			DrawBodyConfiguration();

			GUILayout.Space(20);

			// color configuration
			DrawColorConfiguration();

			// end
			GUILayout.EndVertical();
		}

		void DrawConfigSelector()
		{
			// draw the scrollview and selection grid with the configs
			ui_bodyListPosition = GUILayout.BeginScrollView(ui_bodyListPosition, GUILayout.Width(300f), GUILayout.Height(125f));
			int newChoice = GUILayout.SelectionGrid(ui_bodyChoice, bodyConfigs, Mathf.Min(bodyConfigs.Length, 3));

			if (newChoice != ui_bodyChoice)
			{
				// update ConfigManager
				if (currentBody != "Default") ConfigManager.Instance.bodyConfigs[currentBody] = new BodyConfig(config);
				else ConfigManager.Instance.DefaultConfig = new BodyConfig(config);

				// reset the config stuff
				ui_bodyChoice = newChoice;
				currentBody = bodyConfigs[newChoice];

				config = new BodyConfig(ConfigManager.Instance.bodyConfigs[currentBody]);
				fxModule.OverrideBodyConfig = config;
				ResetFieldText();

				fxModule.ReloadVessel();
			}

			GUILayout.EndScrollView();
		}

		void DrawSimConfiguration()
		{
			GUILayout.Label("These sliders are for previewing the effects while not reentering. Do not use these during normal gameplay!");
			fxModule.OverrideEffectStrength = GuiUtils.LabelSlider("Simulated effect strength", fxModule.OverrideEffectStrength, 0f, (float)ModSettings.I["strength_base"]);
			fxModule.OverrideEffectState = GuiUtils.LabelSlider("Simulated effect state", fxModule.OverrideEffectState, 0f, 1f);
		}

		void DrawBodyConfiguration()
		{
			foreach (string key in config.fields.Keys)
			{
				if (key == "streakProbability" || key == "streakThreshold") continue; // skip streak fields, they are drawn separately as sliders

				GuiUtils.DrawConfigFieldFloat(key, config.fields, GUILayout.Width(300f));
			}

			config["streak_probability"] = GuiUtils.LabelSlider("streak_probability", (float)config["streak_probability"], 0f, 0.09f);
			config["streak_threshold"] = GuiUtils.LabelSlider("streak_threshold", (float)config["streak_threshold"], 0f, -0.5f);
		}

		void DrawColorConfiguration()
		{
			foreach (string key in config.colors.fields.Keys)
			{
				DrawColorButton(key, key);
			}
		}

		// draws a button for a config color
		void DrawColorButton(string label, string colorKey)
		{
			HDRColor c = config.colors[colorKey];

			if (GuiUtils.DrawColorButton(label, Texture2D.whiteTexture, c.baseColor))
			{
				currentlyPicking = colorKey;
				colorPicker.Open(c.baseColor);
			}
		}

		// saves config to cfg file
		void SaveConfig()
		{
			// save config to ConfigManager
			if (currentBody != "Default") ConfigManager.Instance.bodyConfigs[currentBody] = new BodyConfig(config);
			else ConfigManager.Instance.DefaultConfig = new BodyConfig(config);

			Logging.Log($"Saving body config {currentBody}");

			// decide saving path
			string path = config.cfgPath;
			if (!ConfigManager.Instance.loadedBodyConfigs.Contains(currentBody))
			{
				path = KSPUtil.ApplicationRootPath + ConfigManager.NewConfigPath + config.bodyName + ".cfg";
			}

			// create a parent node
			ConfigNode parent = new ConfigNode("ATMOFX_BODY");

			// create the node
			ConfigNode node = new ConfigNode("ATMOFX_BODY");

			config.SaveToNode(ref node);

			// add to parent and save
			parent.AddNode(node);
			parent.Save(path);

			ScreenMessages.PostScreenMessage($"Saved config to file at path\n{path}", 5f);
			Logging.Log("Saved body config " + path);
		}

		void RemoveSelectedConfig()
		{
			ConfigManager.Instance.bodyConfigs.Remove(currentBody);
			config = new BodyConfig(ConfigManager.Instance.bodyConfigs["Default"]);
			fxModule.OverrideBodyConfig = config;
			currentBody = "Default";
			ui_bodyChoice = 0;

			bodyConfigs = ConfigManager.Instance.bodyConfigs.Keys.ToArray();
			ResetFieldText();

			fxModule.ReloadVessel();
		}

		// resets the ui input texts
		void ResetFieldText()
		{
			foreach (string key in config.fields.Keys)
			{
				config.fields[key].uiText = config[key].ToString();
			}
		}

		// sets the direction to the current camera facing
		void ApplyCameraDirection()
		{
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			if (FlightCamera.fetch.mainCamera == null) return;

			effectDirection = vessel.transform.InverseTransformDirection(FlightCamera.fetch.mainCamera.transform.forward);
		}

		// sets the direction to the ship's axis
		void ApplyShipDirection()
		{
			effectDirection = -Vector3.up;
		}

		Vector3 GetWorldDirection()
		{
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return Vector3.zero;

			return vessel.transform.TransformDirection(effectDirection);
		}

		// gets called when the color picker applies a color
		void OnApplyColor()
		{
			config.colors[currentlyPicking] = new HDRColor(colorPicker.color);

			// reset the commandbuffer, to update colors
			fxModule.ReloadCommandBuffer();
		}

		// gets called when the config creation popup confirms creation and closes
		void OnPopupSave()
		{
			// update ConfigManager
			if (currentBody != "Default") ConfigManager.Instance.bodyConfigs[currentBody] = new BodyConfig(config);
			else ConfigManager.Instance.DefaultConfig = new BodyConfig(config);

			// get the new config from the selected template
			config = new BodyConfig(ConfigManager.Instance.bodyConfigs[createConfigPopup.selectedTemplate]);
			currentBody = createConfigPopup.selectedName;
			config.bodyName = currentBody;

			// create a new config array and update ConfigManager's one
			string[] newBodyArray = new string[bodyConfigs.Length + 1];
			bodyConfigs.CopyTo(newBodyArray, 0);
			newBodyArray[bodyConfigs.Length] = currentBody;
			ConfigManager.Instance.bodyConfigs.Add(currentBody, new BodyConfig(config));

			// reset the current body stuff
			ui_bodyChoice = bodyConfigs.Length;

			bodyConfigs = newBodyArray;
			ResetFieldText();

			fxModule.OverrideBodyConfig = config;
			fxModule.ReloadVessel();
		}
	}
}
