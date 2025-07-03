using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FireflyAPI;

namespace Firefly
{
	public class BodyColors
	{
		public Dictionary<string, HDRColor> fields = new Dictionary<string, HDRColor>();

		// custom indexer
		public HDRColor this[string i]
		{
			get => fields[i];
			set => fields[i] = value;
		}

		public BodyColors()
		{
			fields.Add("glow", null);
			fields.Add("glow_hot", null);

			fields.Add("trail_primary", null);
			fields.Add("trail_secondary", null);
			fields.Add("trail_tertiary", null);
			fields.Add("trail_streak", null);

			fields.Add("wrap_layer", null);
			fields.Add("wrap_streak", null);

			fields.Add("shockwave", null);
		}

		/// <summary>
		/// Creates a copy of another BodyColors
		/// </summary>
		public BodyColors(BodyColors org)
		{
			foreach (string key in org.fields.Keys)
			{
				fields.Add(key, org[key]);
			}
		}

		public void SaveToNode(ref ConfigNode node)
		{
			for (int i = 0; i < fields.Count; i++)
			{
				KeyValuePair<string, HDRColor> elem = fields.ElementAt(i);
				node.AddValue(elem.Key, elem.Value.SDRIString());
			}
		}
	}

	public class BodyConfig
	{
		public Dictionary<string, ConfigField> fields = new Dictionary<string, ConfigField>();

		public string cfgPath = "";
		public string bodyName = "Unknown";
		public int configVersion = 5;
		public PlanetPackConfig planetPack = new PlanetPackConfig();

		public BodyColors colors = new BodyColors();

		public object this[string i]
		{
			get => fields[i].value;
			set => fields[i].value = value;
		}

		public BodyConfig()
		{
			fields.Add("strength_multiplier", new ConfigField(1f, ValueType.Float));
			fields.Add("length_multiplier", new ConfigField(1f, ValueType.Float));
			fields.Add("opacity_multiplier", new ConfigField(1f, ValueType.Float));
			fields.Add("glow_multiplier", new ConfigField(1f, ValueType.Float));
			fields.Add("wrap_opacity_multiplier", new ConfigField(1f, ValueType.Float));
			fields.Add("wrap_fresnel_modifier", new ConfigField(1f, ValueType.Float));
			fields.Add("particle_threshold", new ConfigField(1800f, ValueType.Float));
			fields.Add("streak_probability", new ConfigField(0f, ValueType.Float));
			fields.Add("streak_threshold", new ConfigField(0f, ValueType.Float));  // range is 0-1, where 1 is 4000 m/s, default is 0.5
		}

		public BodyConfig(BodyConfig template)
		{
			this.cfgPath = template.cfgPath;
			this.bodyName = template.bodyName;
			this.configVersion = template.configVersion;

			foreach (string key in template.fields.Keys)
			{
				fields.Add(key, new ConfigField(template.fields[key]));
			}

			this.colors = new BodyColors(template.colors);
		}

		public void SaveToNode(ref ConfigNode node)
		{
			node.AddValue("name", bodyName);
			node.AddValue("config_version", configVersion);

			for (int i = 0; i < fields.Count; i++)
			{
				KeyValuePair<string, ConfigField> elem = fields.ElementAt(i);
				node.AddValue(elem.Key, elem.Value.GetValueForSave());
			}

			ConfigNode colorsNode = new ConfigNode("Color");
			colors.SaveToNode(ref colorsNode);
			node.AddNode(colorsNode);
		}
	}

	public class PlanetPackConfig
	{
		// The strength gets multiplied by this after applying body configs
		public float strengthMultiplier = 1f;

		// The strength gets offset by this value (range 0-1)
		// NOTE: This value gets multiplied by the FxState
		public float transitionOffset = 0f;

		// Affected bodies
		public string[] affectedBodies;
	}

	public class ParticleConfig
	{
		public Dictionary<string, ConfigField> fields = new Dictionary<string, ConfigField>();

		public string name = "";

		public string prefab = "";
		public string mainTexture = "";
		public string emissionTexture = "";

		public object this[string i]
		{
			get => fields[i].value;
			set => fields[i].value = value;
		}

		public ParticleConfig() 
		{
			fields.Add("is_active", new ConfigField(true, ValueType.Boolean));
			fields.Add("offset", new ConfigField(0f, ValueType.Float));
			fields.Add("use_half_offset", new ConfigField(false, ValueType.Boolean));
			fields.Add("rate", new ConfigField(new FloatPair(0f, 0f), ValueType.FloatPair));
			fields.Add("lifetime", new ConfigField(new FloatPair(0f, 0f), ValueType.FloatPair));
			fields.Add("velocity", new ConfigField(new FloatPair(0f, 0f), ValueType.FloatPair));
		}

		public ParticleConfig(ParticleConfig x)
		{
			name = x.name;

			prefab = x.prefab;
			mainTexture = x.mainTexture;
			emissionTexture = x.emissionTexture;
			
			foreach (string key in x.fields.Keys)
			{
				fields.Add(key, new ConfigField(x.fields[key]));
			}
		}

		public void SaveToNode(ConfigNode node)
		{
			node.AddValue("prefab", prefab);
			node.AddValue("main_texture", mainTexture);
			node.AddValue("emission_texture", emissionTexture);

			foreach (string key in fields.Keys)
			{
				node.AddValue(key, fields[key].GetValueForSave());
			}
		}
	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class ConfigManager : MonoBehaviour
	{
		public static ConfigManager Instance { get; private set; }

		public const string NewConfigPath = "GameData/Firefly/Configs/Saved/";
		public const string ParticleConfigPath = "GameData/Firefly/Configs/FireflyParticles.cfg";

		public const string DefaultParticleNodeName = "FireflyParticles_Default";

		// loaded configs
		public Dictionary<string, ParticleConfig> particleConfigs = new Dictionary<string, ParticleConfig>();
		public Dictionary<string, BodyColors> partConfigs = new Dictionary<string, BodyColors>();
		public Dictionary<string, BodyConfig> bodyConfigs = new Dictionary<string, BodyConfig>();
		public List<PlanetPackConfig> planetPackConfigs = new List<PlanetPackConfig>();
		public string[] loadedBodyConfigs;

		public List<string> texturesToLoad = new List<string>();

		public BodyConfig DefaultConfig { get; set; }

		// internal SettingsManager handle, used to instantiate it
		SettingsManager settingsManager;

		public void Awake()
		{
			Instance = this;

			Logging.Log("ConfigManager Awake");

			Logging.Log("Creating SettingsManager");
			settingsManager = new SettingsManager();
		}

		/// <summary>
		/// Method which gets ran after MM finishes patching, to allow for config patches
		/// </summary>
		public static void ModuleManagerPostLoad()
		{
			Logging.Log("ConfigManager MMPostLoad");

			Instance.StartLoading();
		}

		/// <summary>
		/// Loads every planet pack and body config
		/// </summary>
		public void StartLoading()
		{
			settingsManager.LoadModSettings();
			LoadPlanetConfigs();
			LoadPartConfigs();
			LoadParticleConfigs();
		}

		void LoadPlanetPackConfigs()
		{
			planetPackConfigs.Clear();

			// here we're using the UrlConfig stuff, to be able to get the path of the config
			UrlDir.UrlConfig[] urlConfigs = GameDatabase.Instance.GetConfigs("ATMOFX_PLANET_PACK");

			if (urlConfigs.Length > 0)
			{
				for (int i = 0; i < urlConfigs.Length; i++)
				{
					ConfigNode node = urlConfigs[i].config;
					string nodeName = node.GetValue("name");

					try
					{
						bool success = ProcessPlanetPackNode(node, out PlanetPackConfig cfg);
						Logging.Log($"Processing planet pack cfg '{nodeName}'");

						if (!success)
						{
							Logging.Log("Planet pack cfg can't be registered");
							ErrorManager.Instance.RegisterError(new ModLoadError(
								cause: ModLoadError.ProbableCause.BadConfig,
								isSerious: false,
								sourcePath: urlConfigs[i].url,
								description: "This planet pack could not be registered. " + ModLoadError.BadConfigAdvice
							));
							continue;
						}

						Logging.Log($"Successfully registered planet pack cfg '{nodeName}'");
						planetPackConfigs.Add(cfg);
					}
					catch (Exception e)  // catching plain exception, to then log it
					{
						Logging.Log($"Exception while loading planet pack {nodeName}.");
						Logging.Log(e.ToString());

						ErrorManager.Instance.RegisterError(new ModLoadError(
							cause: ModLoadError.ProbableCause.BadConfig,
							isSerious: false,
							sourcePath: urlConfigs[i].url,
							description: "The config loader ran into an exception while loading this planet pack. " + ModLoadError.BadConfigAdvice
						));
					}
				}
			}
		}

		void LoadPlanetConfigs()
		{
			bodyConfigs.Clear();

			UrlDir.UrlConfig[] urlConfigs = GameDatabase.Instance.GetConfigs("ATMOFX_BODY");
			if (urlConfigs.Length > 0)
			{
				for (int i = 0; i < urlConfigs.Length; i++)
				{
					try
					{
						bool success = ProcessBodyConfigNode(urlConfigs[i], out BodyConfig body);
						bool isWrongVersion = body.configVersion != Versioning.ConfigVersion;

						// couldn't load the config
						if (!success)
						{
							Logging.Log("Body couldn't be loaded");
							ErrorManager.Instance.RegisterError(new ModLoadError(
								cause: ModLoadError.ProbableCause.BadConfig,
								isSerious: false,
								sourcePath: urlConfigs[i].url,
								description: "This config could not be registered. " + ModLoadError.BadConfigAdvice
							));

							// make sure to check, to be able to display another error
							if (!isWrongVersion) continue;
						}

						// wrong version
						if (isWrongVersion)
						{
							Logging.Log("Body couldn't be loaded (WRONG VERSION CONFIG)");
							ErrorManager.Instance.RegisterError(new ModLoadError(
								cause: ModLoadError.ProbableCause.ConfigVersionMismatch,
								isSerious: false,
								sourcePath: urlConfigs[i].url,
								description: "This config could not be registered. " + 
											(body.configVersion < Versioning.ConfigVersion ? ModLoadError.OutdatedConfigAdvice : ModLoadError.OutdatedFireflyAdvice)
							));
							continue;
						}

						bodyConfigs.Add(body.bodyName, body);
					}
					catch (Exception e)  // catching plain exception, to then log it
					{
						Logging.Log($"Exception while loading config for {urlConfigs[i].config.GetValue("name")}.");
						Logging.Log(e.ToString());
						ErrorManager.Instance.RegisterError(new ModLoadError(
							cause: ModLoadError.ProbableCause.BadConfig,
							isSerious: false,
							sourcePath: urlConfigs[i].url,
							description: "The config loader ran into an exception while loading this planet pack. " + ModLoadError.BadConfigAdvice
						));
					}
				}
			}

			// get the default config
			bool hasDefault = bodyConfigs.ContainsKey("Default");
			if (!hasDefault)
			{
				// some nice error message, since this is a pretty bad one
				Logging.Log("-------------------------------------------");
				Logging.Log("Default config not loaded, halting startup.");
				Logging.Log("This likely means a corrupted install.");
				Logging.Log("-------------------------------------------");

				ErrorManager.Instance.RegisterError(new ModLoadError(
					cause: ModLoadError.ProbableCause.IncorrectInstall,
					isSerious: true,
					sourcePath: "Default body config",
					description: "The config loader did not load the default config. This probably means the mod is installed incorrectly."
				));

				return;
			}

			DefaultConfig = bodyConfigs["Default"];
			loadedBodyConfigs = bodyConfigs.Keys.ToArray();
		}

		void LoadPartConfigs()
		{
			partConfigs.Clear();

			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_PART");
			if (nodes.Length > 0)
			{
				for (int i = 0; i < nodes.Length; i++)
				{
					string partId = nodes[i].GetValue("name");
					bool success = ProcessPartConfigNode(nodes[i], out BodyColors cfg);

					Logging.Log($"Processed part override config {partId}");

					if (!success)
					{
						Logging.Log($"Couldn't process override config for part {partId}");
						ErrorManager.Instance.RegisterError(new ModLoadError(
							cause: ModLoadError.ProbableCause.BadConfig,
							isSerious: false,
							sourcePath: "Part override config for " + partId,
							description: "This config could not be registered. " + ModLoadError.BadConfigAdvice
						));
						continue;
					}

					partConfigs.Add(partId, cfg);
				}
			}
		}

		void LoadParticleConfigs()
		{
			particleConfigs.Clear();
			texturesToLoad.Clear();

			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_PARTICLES");
			if (nodes.Length > 0)
			{
				for (int i = 0; i < nodes.Length; i++)
				{
					string name = nodes[i].GetValue("name");
					ProcessParticleConfigNode(nodes[i]);

					Logging.Log($"Processed particle config {name}");
				}
			}
		}

		bool ProcessPlanetPackNode(ConfigNode node, out PlanetPackConfig cfg)
		{
			Logging.Log($"Loading planet pack config");

			// create the config
			bool isFormatted = true;
			cfg = new PlanetPackConfig
			{
				strengthMultiplier = ReadConfigFloat(node, "strength_multiplier", ref isFormatted),
				transitionOffset = ReadConfigFloat(node, "transition_offset", ref isFormatted),
			};

			// read the affected body array
			string array = node.GetValue("affected_bodies");
			if (!string.IsNullOrEmpty(array))
			{
				// split into individual bodies
				string[] strings = array.Split(',');
				for (int i = 0; i < strings.Length; i++)
				{
					strings[i] = strings[i].Trim();
				}

				if (strings.Length < 1)
				{
					Logging.Log("WARNING: Planet pack config affects no bodies");
					return false;
				}

				cfg.affectedBodies = strings;
			}
			else
			{
				isFormatted = false;
			}

			if (!isFormatted)
			{
				Logging.Log($"Planet pack config '{node.name}' is not formatted correctly");
				return false;
			}

			return true;
		}

		bool ProcessBodyConfigNode(UrlDir.UrlConfig cfg, out BodyConfig body)
		{
			ConfigNode node = cfg.config;

			body = null;

			string bodyName = node.GetValue("name");

			Logging.Log($"Loading body '{bodyName}'");

			// make sure there aren't any duplicates
			if (bodyConfigs.ContainsKey(bodyName))
			{
				Logging.Log($"Duplicate body config found: {bodyName}");
				return false;
			}

			// create the config
			bool isFormatted = true;
			body = new BodyConfig
			{
				cfgPath = cfg.parent.fullPath,
				bodyName = bodyName,
				configVersion = ReadConfigInt(node, "config_version", ref isFormatted)
			};

			// check if the config version is defined
			if (body.configVersion == 0)  // not comparing to null, since uninitialized int is 0
			{
				// no config version, then version must be < 5 (cfg version which the versioning system was introduced in)
				body.configVersion = 4;
			}

			// read the fields
			foreach (string key in body.fields.Keys)
			{
				body.fields[key].ParseString(node.GetValue(key), ref isFormatted, false);
			}

			// read the colors
			isFormatted = isFormatted && ProcessBodyColors(node, false, out body.colors);

			// check if formatted
			if (!isFormatted)
			{
				Logging.Log($"Body config is not formatted correctly: {bodyName}");
				return false;
			}

			// apply planet pack configs
			for (int i = 0; i < planetPackConfigs.Count; i++)
			{
				// check if the body should be affected
				if (planetPackConfigs[i].affectedBodies.Contains(bodyName))
				{
					body.planetPack = planetPackConfigs[i];
					body["strength_multiplier"] = (float)body["strength_multiplier"] * planetPackConfigs[i].strengthMultiplier;
				}
			}

			return true;
		}

		bool ProcessPartConfigNode(ConfigNode node, out BodyColors cfg)
		{
			bool isFormatted = ProcessBodyColors(node, true, out cfg);

			return isFormatted;
		}

		void ProcessParticleConfigNode(ConfigNode node)
		{
			string cfgName = node.GetValue("name");

			// process each particle system definition
			for (int i = 0; i < node.CountNodes; i++)
			{
				ConfigNode singleNode = node.nodes[i];
				string name = singleNode.name;

				bool success = ProcessSingleParticleDef(singleNode, out ParticleConfig singleCfg);
				if (!success)
				{
					Logging.Log($"Couldn't process single particle config {name} in {cfgName}");
					ErrorManager.Instance.RegisterError(new ModLoadError(
						cause: ModLoadError.ProbableCause.BadConfig,
						isSerious: false,
						sourcePath: $"Single particle system {name} in {cfgName}",
						description: "This config could not be registered. " + ModLoadError.BadConfigAdvice
					));
					continue;
				}

				texturesToLoad.Add(singleCfg.mainTexture);
				texturesToLoad.Add(singleCfg.emissionTexture);

				particleConfigs.Add(name, singleCfg);
			}
		}

		bool ProcessSingleParticleDef(ConfigNode node, out ParticleConfig cfg)
		{
			bool isFormatted = true;
			cfg = new ParticleConfig()
			{
				name = node.name,

				prefab = node.GetValue("prefab"),

				mainTexture = node.GetValue("main_texture"),
				emissionTexture = node.GetValue("emission_texture")
			};

			// if no emission texture, set it to empty string
			if (cfg.emissionTexture == "unused" || cfg.emissionTexture == "") cfg.emissionTexture = "";

			// read the fields
			foreach (string key in cfg.fields.Keys)
			{
				cfg.fields[key].ParseString(node.GetValue(key), ref isFormatted, false);
			}

			return isFormatted;
		}

		/// <summary>
		/// Adds all required textures to a list, to be loaded later by the AssetLoader
		/// </summary>
		public void RefreshTextureList()
		{
			texturesToLoad.Clear();

			foreach (string key in particleConfigs.Keys)
			{
				ParticleConfig cfg = particleConfigs[key];
				if (!texturesToLoad.Contains(cfg.mainTexture)) texturesToLoad.Add(cfg.mainTexture);
				if (!texturesToLoad.Contains(cfg.emissionTexture)) texturesToLoad.Add(cfg.emissionTexture);
			}
		}

		bool ProcessBodyColors(ConfigNode rootNode, bool partConfig, out BodyColors body)
		{
			body = new BodyColors();

			ConfigNode colorNode = new ConfigNode();
			bool isFormatted = rootNode.TryGetNode("Color", ref colorNode);
			if (!isFormatted) return false;

			BodyColors overrideCol = new BodyColors();
			var keys = overrideCol.fields.Keys;
			foreach (string key in keys)
			{
				body[key] = ReadConfigColorHDR(colorNode, key, partConfig, ref isFormatted);
			}

			return isFormatted;
		}

		float ReadConfigFloat(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateFloat(node.GetValue(key), out float result);
			isFormatted = isFormatted && success;

			return result;
		}

		int ReadConfigInt(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateInt(node.GetValue(key), out int result);
			isFormatted = isFormatted && success;

			return result;
		}

		bool ReadConfigBoolean(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateBool(node.GetValue(key), out bool result);
			isFormatted = isFormatted && success;

			return result;
		}

		HDRColor ReadConfigColorHDR(ConfigNode node, string key, bool isPartConfig, ref bool isFormatted)
		{
			if (!node.HasValue(key))
			{
				// if this is not a partconfig (it's a body config) then make sure to set isFormatted to false
				// partconfigs can have missing values, but body configs should not
				if (!isPartConfig) isFormatted = false;

				return null;
			}

			string value = node.GetValue(key);
			if (value.ToLower() == "null" || value.ToLower() == "default")
			{
				// same logic as above
				if (!isPartConfig) isFormatted = false;
				return null;
			}

			bool success = Utils.EvaluateColorHDR(value, out _, out Color sdr);
			isFormatted = isFormatted && success;

			return new HDRColor(sdr);
		}

		/// <summary>
		/// Tries to get a body config for a given body name. Returns true if the config was found, false otherwise.
		/// </summary>
		/// <param name="bodyName">The config name to get</param>
		/// <param name="fallback">Whether to fallback to the default config if specified config was not found</param>
		/// <param name="cfg">The output config</param>
		/// <returns>Whether the specified config was found</returns>
		public bool TryGetBodyConfig(string bodyName, bool fallback, out BodyConfig cfg)
		{
			bool hasConfig = bodyConfigs.ContainsKey(bodyName);

			if (hasConfig)
			{
				cfg = bodyConfigs[bodyName];
			} else
			{
				// null the cfg, or fallback to the default one
				cfg = null;
				if (fallback) cfg = DefaultConfig;
			}

			return hasConfig;
		}

		/// <summary>
		/// Gets the body config for a given vessel. Returns the default config if not found.
		/// </summary>
		public BodyConfig GetVesselBody(Vessel vessel)
		{
			TryGetBodyConfig(vessel.mainBody.bodyName, true, out BodyConfig cfg);
			return cfg;
		}
	}
}
