using System.Linq;
using UnityEngine;

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

			GUILayout.BeginHorizontal();
			GUILayout.Label("Config name");
			ui_cfgName = GUILayout.TextField(ui_cfgName);
			GUILayout.EndHorizontal();

			GUILayout.Label("Select a template config");
			DrawConfigSelector();

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
		public float effectStrength;
		public float effectState;

		public BodyConfig config;

		string[] bodyConfigs;
		string currentBody;

		AtmoFxModule fxModule = null;

		// gui
		string removeConfigText = "Remove selected config";
		int removeConfigState = 0;
		float removeConfigStateTimer = 0f;

		string saveConfigText = "Save selected to cfg file";
		int saveConfigState = 0;
		float saveConfigStateTimer = 0f;

		Vector2 ui_bodyListPosition;
		int ui_bodyChoice;

		string ui_strengthMultiplier;
		string ui_lengthMultiplier;
		string ui_opacityMultiplier;
		string ui_wrapFresnelModifier;
		string ui_particleThreshold;

		// dialog windows
		public ColorPickerWindow colorPicker;
		string currentlyPicking;

		public CreateConfigPopup createConfigPopup;	

		// for drawing color buttons
		Texture2D whitePixel;

		public EffectEditor() : base("Effect editor")
		{
			windowRect = new Rect(300, 100, 300, 100);
			Instance = this;

			bodyConfigs = ConfigManager.Instance.bodyConfigs.Keys.ToArray();

			colorPicker = new ColorPickerWindow(900, 100, Color.red);
			colorPicker.onApplyColor = OnApplyColor;

			createConfigPopup = new CreateConfigPopup();
			createConfigPopup.onPopupSave = OnPopupSave;

			whitePixel = TextureUtils.GenerateColorTexture(1, 1, Color.white);
		}

		// saves config to cfg file
		void SaveConfig()
		{
			// check state and ask for confirmation
			if (saveConfigState == 0)
			{
				saveConfigText = "Are you sure?";
				saveConfigState = 1;
				saveConfigStateTimer = Time.time;
				return;
			}

			// save config to ConfigManager
			if (currentBody != "Default") ConfigManager.Instance.bodyConfigs[currentBody] = new BodyConfig(config);
			else ConfigManager.Instance.defaultConfig = new BodyConfig(config);

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

			// reset state
			ResetSaveConfigState();
		}

		// resets the ui input texts
		void ResetFieldText()
		{
			ui_strengthMultiplier = config.strengthMultiplier.ToString();
			ui_lengthMultiplier = config.lengthMultiplier.ToString();
			ui_opacityMultiplier = config.opacityMultiplier.ToString();
			ui_wrapFresnelModifier = config.wrapFresnelModifier.ToString();
			ui_particleThreshold = config.particleThreshold.ToString();
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

		public Vector3 GetWorldDirection()
		{
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return Vector3.zero;

			return vessel.transform.TransformDirection(effectDirection);
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
				config = new BodyConfig(ConfigManager.Instance.defaultConfig);
			}

			ResetFieldText();

			// load effects
			fxModule.doEffectEditor = true;
			if (!fxModule.isLoaded) fxModule.CreateVesselFx();
		}

		public override void Hide()
		{
			base.Hide();

			fxModule.doEffectEditor = false;

			fxModule.currentBody = ConfigManager.Instance.bodyConfigs[currentBody];
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
			fxModule.currentBody = config;

			// 3d
			if (fxModule == null || fxModule.fxVessel == null) return;
			Transform camTransform = fxModule.fxVessel.airstreamCamera.transform;
			if (!fxModule.debugMode) DrawingUtils.DrawArrow(camTransform.position, camTransform.forward, camTransform.right, camTransform.up, Color.cyan);

			// timed gui states
			if ((Time.time - saveConfigStateTimer) >= 4f && saveConfigState == 1)
			{
				ResetSaveConfigState();
			}
			if ((Time.time - removeConfigStateTimer) >= 4f && removeConfigState == 1)
			{
				ResetRemoveConfigState();
			}
		}

		void DrawLeftEditor()
		{
			GUILayout.BeginVertical();

			// config create
			if (GUILayout.Button("Create new config") && !createConfigPopup.show) createConfigPopup.Init(bodyConfigs);
			if (GUILayout.Button(removeConfigText) && currentBody != "Default") RemoveSelectedConfig();

			// body selection
			GUILayout.Label("Select a config:");
			DrawConfigSelector();
			GUILayout.Space(20);

			// sim configuration
			DrawSimConfiguration();
			GUILayout.Space(20);

			// saving
			if (GUILayout.Button("Align effects to camera")) ApplyCameraDirection();
			if (GUILayout.Button("Align effects to ship")) ApplyShipDirection();
			if (GUILayout.Button(saveConfigText)) SaveConfig();

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
				else ConfigManager.Instance.defaultConfig = new BodyConfig(config);

				// reset the config stuff
				ui_bodyChoice = newChoice;
				currentBody = bodyConfigs[newChoice];

				config = new BodyConfig(ConfigManager.Instance.bodyConfigs[currentBody]);
				ResetFieldText();

				fxModule.ReloadVessel();
			}

			GUILayout.EndScrollView();
		}

		void DrawSimConfiguration()
		{
			GUILayout.Label("These sliders are for previewing the effects while not reentering. Do not use these during normal gameplay!");
			effectStrength = GuiUtils.LabelSlider("Simulated effect strength", effectStrength, 0f, (float)ModSettings.I["strength_base"]);
			effectState = GuiUtils.LabelSlider("Simulated effect state", effectState, 0f, 1f);
		}

		void DrawBodyConfiguration()
		{
			GuiUtils.DrawFloatInput("Strength multiplier", ref ui_strengthMultiplier, ref config.strengthMultiplier, GUILayout.Width(300f));
			GuiUtils.DrawFloatInput("Length multiplier", ref ui_lengthMultiplier, ref config.lengthMultiplier);
			GuiUtils.DrawFloatInput("Opacity multiplier", ref ui_opacityMultiplier, ref config.opacityMultiplier);
			GuiUtils.DrawFloatInput("Wrap fresnel modifier", ref ui_wrapFresnelModifier, ref config.wrapFresnelModifier);
			GuiUtils.DrawFloatInput("Particle threshold", ref ui_particleThreshold, ref config.particleThreshold);

			config.streakProbability = GuiUtils.LabelSlider("Streak probability", config.streakProbability, 0f, 0.09f);
			config.streakThreshold = GuiUtils.LabelSlider("Streak threshold", config.streakThreshold, 0f, -0.5f);
		}

		void DrawColorConfiguration()
		{
			DrawColorButton("Glow", "glow");
			DrawColorButton("Hot Glow", "glow_hot");

			DrawColorButton("Trail Primary", "trail_primary");
			DrawColorButton("Trail Secondary", "trail_secondary");
			DrawColorButton("Trail Tertiary", "trail_tertiary");
			DrawColorButton("Trail Streak", "trail_streak");

			DrawColorButton("Wrap Layer", "wrap_layer");
			DrawColorButton("Wrap Streak", "wrap_streak");

			DrawColorButton("Bowshock", "shockwave");
		}

		// draws a button for a config color
		void DrawColorButton(string label, string colorKey)
		{
			HDRColor c = config.colors[colorKey];

			if (GuiUtils.DrawColorButton(label, whitePixel, c.baseColor))
			{
				currentlyPicking = colorKey;
				colorPicker.Open(c.baseColor);
			}
		}

		// gets called when the color picker applies a color
		void OnApplyColor()
		{
			config.colors[currentlyPicking] = new HDRColor(colorPicker.color);

			// reset the commandbuffer, to update colors
			fxModule.DestroyCommandBuffer();
			fxModule.InitializeCommandBuffer();
			fxModule.PopulateCommandBuffer();
		}

		// gets called when the config creation popup confirms creation and closes
		void OnPopupSave()
		{
			// update ConfigManager
			if (currentBody != "Default") ConfigManager.Instance.bodyConfigs[currentBody] = new BodyConfig(config);
			else ConfigManager.Instance.defaultConfig = new BodyConfig(config);

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

			fxModule.currentBody = config;
			fxModule.ReloadVessel();
		}

		void RemoveSelectedConfig()
		{
			// ask for confirmation
			if (removeConfigState == 0)
			{
				removeConfigText = "Are you sure?";
				removeConfigState = 1;
				removeConfigStateTimer = Time.time;

				return;
			}

			ConfigManager.Instance.bodyConfigs.Remove(currentBody);
			config = new BodyConfig(ConfigManager.Instance.bodyConfigs["Default"]);
			currentBody = "Default";
			ui_bodyChoice = 0;

			bodyConfigs = ConfigManager.Instance.bodyConfigs.Keys.ToArray();
			ResetFieldText();

			fxModule.ReloadVessel();

			// reset state
			ResetRemoveConfigState();
		}

		void ResetSaveConfigState()
		{
			saveConfigState = 0;
			saveConfigText = "Save selected to cfg file";
		}

		void ResetRemoveConfigState()
		{
			removeConfigText = "Remove selected config";
			removeConfigState = 0;
		}
	}
}
