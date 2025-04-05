using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Firefly
{
	public class HDRColor
	{
		public Color baseColor;
		public Color hdr;
		public float intensity;

		public bool hasValue;

		public HDRColor(Color sdri)
		{
			baseColor = sdri;
			hdr = Utils.SDRI_To_HDR(sdri);
			intensity = sdri.a;
		}

		public string SDRIString()
		{
			return $"{Mathf.RoundToInt(baseColor.r * 255f)} {Mathf.RoundToInt(baseColor.g * 255f)} {Mathf.RoundToInt(baseColor.b * 255f)} {baseColor.a}";
		}

		public static HDRColor CreateNull()
		{
			HDRColor c = new HDRColor(Color.black);
			c.hasValue = false;
			return c;
		}

		public static implicit operator Color(HDRColor x)
		{
			return x.hdr;
		}
	}

	public class BodyColors
	{
		public Dictionary<string, HDRColor> fields = new Dictionary<string, HDRColor>();

		public HDRColor shockwave;

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
			fields.Add("glow", org["glow"]);
			fields.Add("glow_hot", org["glow_hot"]);

			fields.Add("trail_primary", org["trail_primary"]);
			fields.Add("trail_secondary", org["trail_secondary"]);
			fields.Add("trail_tertiary", org["trail_tertiary"]);
			fields.Add("trail_streak", org["trail_streak"]);

			fields.Add("wrap_layer", org["wrap_layer"]);
			fields.Add("wrap_streak", org["wrap_streak"]);

			fields.Add("shockwave", org["shockwave"]);
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
		public string cfgPath = "";
		public string bodyName = "Unknown";
		public PlanetPackConfig planetPack = new PlanetPackConfig();

		// The entry strength gets multiplied by this before getting sent to the shader
		public float strengthMultiplier = 1f;

		// The trail length gets multiplied by this
		public float lengthMultiplier = 1f;

		// The trail opacity gets multiplied by this
		public float opacityMultiplier = 1f;

		// The wrap layer's fresnel effect is modified by this
		public float wrapFresnelModifier = 1f;

		// The threshold in m/s for particles to appear
		public float particleThreshold = 1800f;

		// This gets added to the streak probability
		public float streakProbability = 0f;

		// This gets added to the streak threshold, which is 0.5 by default (range is 0-1, where 1 is 4000 m/s, default is 0.5)
		public float streakThreshold = 0f;

		// Colors
		public BodyColors colors = new BodyColors();

		public BodyConfig() { }

		public BodyConfig(BodyConfig template)
		{
			this.bodyName = template.bodyName;
			this.cfgPath = template.cfgPath;
			this.strengthMultiplier = template.strengthMultiplier;
			this.lengthMultiplier = template.lengthMultiplier;
			this.opacityMultiplier = template.opacityMultiplier;
			this.wrapFresnelModifier = template.wrapFresnelModifier;
			this.particleThreshold = template.particleThreshold;
			this.streakProbability = template.streakProbability;
			this.streakThreshold = template.streakThreshold;

			this.colors = new BodyColors(template.colors);
		}

		public void SaveToNode(ref ConfigNode node)
		{
			node.AddValue("name", bodyName);
			node.AddValue("strength_multiplier", strengthMultiplier);

			node.AddValue("length_multiplier", lengthMultiplier);
			node.AddValue("opacity_multiplier", opacityMultiplier);
			node.AddValue("wrap_fresnel_modifier", wrapFresnelModifier);

			node.AddValue("particle_threshold", particleThreshold);

			node.AddValue("streak_probability", streakProbability);
			node.AddValue("streak_threshold", streakThreshold);

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
		public string name = "";

		public string prefab = "";

		public string mainTexture = "";
		public string emissionTexture = "";

		public float offset = 0f;
		public bool useHalfOffset = false;

		public FloatPair rate;
		public FloatPair lifetime;
		public FloatPair velocity;

		public ParticleConfig() { }

		public ParticleConfig(ParticleConfig x)
		{
			name = x.name;

			prefab = x.prefab;

			mainTexture = x.mainTexture;
			emissionTexture = x.emissionTexture;

			offset = x.offset;
			useHalfOffset = x.useHalfOffset;

			lifetime = new FloatPair(x.lifetime.x, x.lifetime.y);
			velocity = new FloatPair(x.velocity.x, x.velocity.y);
		}

		public void SaveToNode(ConfigNode node)
		{
			node.AddValue("prefab", prefab);
			node.AddValue("mainTexture", mainTexture);
			node.AddValue("emissionTexture", emissionTexture);
			node.AddValue("offset", offset.ToString());
			node.AddValue("useHalfOffset", useHalfOffset.ToString());
			node.AddValue("rate", rate.ToString());
			node.AddValue("lifetime", lifetime.ToString());
			node.AddValue("velocity", velocity.ToString());
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

		public BodyConfig defaultConfig;

		// error list
		public List<ModLoadError> errorList = new List<ModLoadError>();

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

		void LoadPlanetConfigs()
		{
			// clear the dict and list
			bodyConfigs.Clear();
			planetPackConfigs.Clear();

			// get the planet packs
			// here we're using the UrlConfig stuff, to be able to get the path of the config
			UrlDir.UrlConfig[] urlConfigs = GameDatabase.Instance.GetConfigs("ATMOFX_PLANET_PACK");

			// check if there's actually anything to load
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
							errorList.Add(new ModLoadError(
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

						errorList.Add(new ModLoadError(
							cause: ModLoadError.ProbableCause.BadConfig,
							isSerious: false,
							sourcePath: urlConfigs[i].url,
							description: "The config loader ran into an exception while loading this planet pack. " + ModLoadError.BadConfigAdvice
						));
					}
				}
			}

			// get the nodes
			urlConfigs = GameDatabase.Instance.GetConfigs("ATMOFX_BODY");

			// check if there's actually anything to load
			if (urlConfigs.Length > 0)
			{
				// iterate over every node and store the data
				for (int i = 0; i < urlConfigs.Length; i++)
				{
					try
					{
						bool success = ProcessSingleBodyNode(urlConfigs[i], out BodyConfig body);

						// couldn't load the config
						if (!success)
						{
							Logging.Log("Body couldn't be loaded");
							errorList.Add(new ModLoadError(
								cause: ModLoadError.ProbableCause.BadConfig,
								isSerious: false,
								sourcePath: urlConfigs[i].url,
								description: "This config could not be registered. " + ModLoadError.BadConfigAdvice
							));
							continue;
						}

						bodyConfigs.Add(body.bodyName, body);
					}
					catch (Exception e)  // catching plain exception, to then log it
					{
						Logging.Log($"Exception while loading config for {urlConfigs[i].config.GetValue("name")}.");
						Logging.Log(e.ToString());
						errorList.Add(new ModLoadError(
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

				errorList.Add(new ModLoadError(
					cause: ModLoadError.ProbableCause.IncorrectInstall,
					isSerious: true,
					sourcePath: "Default body config",
					description: "The config loader did not load the default config. This probably means the mod is installed incorrectly."
				));

				return;
			}

			defaultConfig = bodyConfigs["Default"];

			loadedBodyConfigs = bodyConfigs.Keys.ToArray();
		}

		void LoadPartConfigs()
		{
			// clear the dict
			partConfigs.Clear();

			// get the nodes
			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_PART");

			// check if there's actually anything to load
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
						errorList.Add(new ModLoadError(
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

			// get the nodes
			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_PARTICLES");

			// check if there's actually anything to load
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

		bool ProcessSingleBodyNode(UrlDir.UrlConfig cfg, out BodyConfig body)
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

				strengthMultiplier = ReadConfigValue(node, "strength_multiplier", ref isFormatted),
				lengthMultiplier = ReadConfigValue(node, "length_multiplier", ref isFormatted),
				opacityMultiplier = ReadConfigValue(node, "opacity_multiplier", ref isFormatted),
				wrapFresnelModifier = ReadConfigValue(node, "wrap_fresnel_modifier", ref isFormatted),
				particleThreshold = ReadConfigValue(node, "particle_threshold", ref isFormatted),
				streakProbability = ReadConfigValue(node, "streak_probability", ref isFormatted),
				streakThreshold = ReadConfigValue(node, "streak_threshold", ref isFormatted)
			};

			// read the colors
			isFormatted = isFormatted && ProcessBodyColors(node, false, out body.colors);

			// is the config formatted correctly?
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
					body.strengthMultiplier *= planetPackConfigs[i].strengthMultiplier;
				}
			}

			return true;
		}

		bool ProcessPlanetPackNode(ConfigNode node, out PlanetPackConfig cfg)
		{
			Logging.Log($"Loading planet pack config");

			// create the config
			bool isFormatted = true;
			cfg = new PlanetPackConfig
			{
				strengthMultiplier = ReadConfigValue(node, "strength_multiplier", ref isFormatted),
				transitionOffset = ReadConfigValue(node, "transition_offset", ref isFormatted),
			};

			// read the affected body array
			string array = node.GetValue("affected_bodies");

			if (!string.IsNullOrEmpty(array))
			{
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
			} else
			{
				isFormatted = false;
			}

			// is the config formatted correctly?
			if (!isFormatted)
			{
				Logging.Log($"Planet pack config '{node.name}' is not formatted correctly");
				return false;
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

				bool success = ProcessSingleParticleNode(singleNode, out ParticleConfig singleCfg);

				// log error if the config is not formatted correctly
				if (!success)
				{
					Logging.Log($"Couldn't process single particle config {name} in {cfgName}");
					errorList.Add(new ModLoadError(
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

		bool ProcessSingleParticleNode(ConfigNode node, out ParticleConfig cfg)
		{
			bool isFormatted = true;
			cfg = new ParticleConfig()
			{
				name = node.name,

				prefab = node.GetValue("prefab"),

				mainTexture = node.GetValue("mainTexture"),
				emissionTexture = node.GetValue("emissionTexture")
			};

			if (cfg.emissionTexture == "unused" || cfg.emissionTexture == "") cfg.emissionTexture = "";

			isFormatted = isFormatted && Utils.EvaluateFloat(node.GetValue("offset"), out cfg.offset);
			isFormatted = isFormatted && Utils.EvaluateBool(node.GetValue("useHalfOffset"), out cfg.useHalfOffset);

			isFormatted = isFormatted && Utils.EvaluateFloatPair(node.GetValue("rate"), out cfg.rate);
			isFormatted = isFormatted && Utils.EvaluateFloatPair(node.GetValue("lifetime"), out cfg.lifetime);
			isFormatted = isFormatted && Utils.EvaluateFloatPair(node.GetValue("velocity"), out cfg.velocity);

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

		/// <summary>
		/// Processes the colors node of a body
		/// </summary>
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

		/// <summary>
		/// Reads one float value from a node
		/// </summary>
		float ReadConfigValue(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateFloat(node.GetValue(key), out float result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Reads one boolean value from a node
		/// </summary>
		bool ReadConfigBoolean(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateBool(node.GetValue(key), out bool result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Reads one HDR color value from a node
		/// </summary>
		HDRColor ReadConfigColorHDR(ConfigNode node, string key, bool partConfig, ref bool isFormatted)
		{
			// check if exists
			if (!node.HasValue(key))
			{
				isFormatted = isFormatted && partConfig;

				return null;
			}

			// get the value
			string value = node.GetValue(key);

			// check if null
			if (value.ToLower() == "null" || value.ToLower() == "default")
			{
				isFormatted = isFormatted && partConfig;

				return null;
			}

			bool success = Utils.EvaluateColorHDR(value, out _, out Color sdr);
			isFormatted = isFormatted && success;

			return new HDRColor(sdr);
		}

		/// <summary>
		/// Tries getting the body config for a specified body name, and fallbacks if desired
		/// </summary>
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
				if (fallback) cfg = defaultConfig;
			}

			return hasConfig;
		}

		/// <summary>
		/// Gets the body config for a specified vessel
		/// </summary>
		public BodyConfig GetVesselBody(Vessel vessel)
		{
			TryGetBodyConfig(vessel.mainBody.bodyName, true, out BodyConfig cfg);
			return cfg;
		}
	}
}
