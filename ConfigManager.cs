using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AtmosphericFx
{
	public class ModSettings
	{
		public static ModSettings Instance { get; private set; }

		public bool hdrOverride = true;
		public bool disableParticles = false;

		public int renderTextureResolution = 512;
		public float hexgridRadius = 0.05f;

		public ModSettings()
		{
			Instance = this;
		}
	}

	public enum ModifierOperation
	{
		ADD = 0,
		SUBTRACT = 1,
		MULTIPLY = 2,
		DIVIDE = 3
	}

	public struct TransitionModifier
	{
		// Smaller order values get ran first
		public float order;

		// The operation to use
		public ModifierOperation operation;

		// The value
		public float value;

		// Is the modifier dependent on the stock fx - if true then the value gets multiplied by the inverted stock fx scalar
		public bool stockfxDependent;
	}

	public struct BodyColors
	{
		public Color glow;
		public Color glowHot;

		public Color trailPrimary;
		public Color trailSecondary;
		public Color trailTertiary;

		public Color wrapLayer;
		public Color wrapStreak;

		public Color shockwave;
	}

	public class BodyConfig
	{
		public string bodyName = "Unknown";

		// The entry speed gets multiplied by this before getting sent to the shader
		public float intensity = 1f;

		// This is used to modify the AeroFX scalar before getting sent off
		public TransitionModifier[] transitionModifiers;

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
	}

	public class PlanetPackConfig
	{
		// The speed gets multiplied by this after applying body configs
		public float speedMultiplier = 1f;

		// Affected bodies
		public string[] affectedBodies;
	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class ConfigManager : MonoBehaviour
	{
		public static ConfigManager Instance { get; private set; }
		public const string SettingsPath = "GameData/AtmosphericFx/Configs/ModSettings.cfg";

		public ModSettings modSettings = new ModSettings();

		public Dictionary<string, BodyConfig> bodyConfigs = new Dictionary<string, BodyConfig>();
		public List<PlanetPackConfig> planetPackConfigs = new List<PlanetPackConfig>();

		public BodyConfig defaultConfig;

		public string homeWorld;

		public void Awake()
		{
			Instance = this;

			Logging.Log("ConfigManager Awake");
		}

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
			LoadModSettings();
			LoadPlanetConfigs();
		}
		
		/// <summary>
		/// Saves the mod setting overrides
		/// </summary>
		public void SaveModSettings()
		{
			// create a parent node
			ConfigNode parent = new ConfigNode("ATMOFX_SETTINGS");

			// create the node
			ConfigNode node = new ConfigNode("ATMOFX_SETTINGS");

			node.AddValue("hdr_override", modSettings.hdrOverride);
			node.AddValue("disable_particles", modSettings.disableParticles);
			node.AddValue("render_texture_resolution", modSettings.renderTextureResolution);
			node.AddValue("hexgrid_radius", modSettings.hexgridRadius);

			// add to parent and save
			parent.AddNode(node);
			parent.Save(KSPUtil.ApplicationRootPath + SettingsPath);
		}

		/// <summary>
		/// Loads the mod settings
		/// </summary>
		void LoadModSettings()
		{
			// load settings
			ConfigNode[] settingsNodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_SETTINGS");
			modSettings = new ModSettings();

			if (settingsNodes.Length < 1)
			{
				// we don't have any saved settings or the user deleted the cfg file
				Logging.Log("Using default mod settings");
				return;
			}

			ConfigNode settingsNode = settingsNodes[0];

			bool isFormatted = true;
			modSettings.hdrOverride = ReadConfigBoolean(settingsNode, "hdr_override", ref isFormatted);
			modSettings.disableParticles = ReadConfigBoolean(settingsNode, "disable_particles", ref isFormatted);
			modSettings.renderTextureResolution = (int)ReadConfigValue(settingsNode, "render_texture_resolution", ref isFormatted);
			modSettings.hexgridRadius = ReadConfigValue(settingsNode, "hexgrid_radius", ref isFormatted);

			if (!isFormatted)
			{
				Logging.Log("Settings cfg formatted incorrectly");
				modSettings = new ModSettings();
			}
		}

		/// <summary>
		/// Loads the planetpack and body configs
		/// </summary>
		void LoadPlanetConfigs()
		{
			// clear the dict and list
			bodyConfigs.Clear();
			planetPackConfigs.Clear();

			// get the planet packs
			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_PLANET_PACK");

			// check if there's actually anything to load
			if (nodes.Length > 0)
			{
				for (int i = 0; i < nodes.Length; i++)
				{
					ProcessPlanetPackNode(nodes[i], out PlanetPackConfig cfg);

					planetPackConfigs.Add(cfg);
				}
			}

			// get the nodes
			nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_BODY");

			// check if there's actually anything to load
			if (nodes.Length > 0)
			{
				// iterate over every node and store the data
				for (int i = 0; i < nodes.Length; i++)
				{
					bool success = ProcessSingleNode(nodes[i], out BodyConfig body);

					// couldn't load the config
					if (!success)
					{
						Logging.Log("Body couldn't be loaded");
						continue;
					}

					bodyConfigs.Add(body.bodyName, body);
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

				return;
			}

			defaultConfig = bodyConfigs["Default"];
		}

		/// <summary>
		/// Processes single node
		/// </summary>
		bool ProcessSingleNode(ConfigNode node, out BodyConfig body)
		{
			body = null;

			string bodyName = node.GetValue("name");

			Logging.Log($"Loading body {bodyName}");

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
				bodyName = bodyName,

				intensity = ReadConfigValue(node, "intensity", ref isFormatted),
				lengthMultiplier = ReadConfigValue(node, "length_multiplier", ref isFormatted),
				opacityMultiplier = ReadConfigValue(node, "opacity_multiplier", ref isFormatted),
				wrapFresnelModifier = ReadConfigValue(node, "wrap_fresnel_modifier", ref isFormatted),
				particleThreshold = ReadConfigValue(node, "particle_threshold", ref isFormatted),
				streakProbability = ReadConfigValue(node, "streak_probability", ref isFormatted),
				streakThreshold = ReadConfigValue(node, "streak_threshold", ref isFormatted)
			};

			// read the transition modifiers
			isFormatted = isFormatted && ProcessTransitionModifiers(node, out body.transitionModifiers);

			// read the colors
			isFormatted = isFormatted && ProcessBodyColors(node, out body.colors);

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
					body.intensity *= planetPackConfigs[i].speedMultiplier;
				}
			}

			return true;
		}

		/// <summary>
		/// Processes planet pack nodes
		/// </summary>
		bool ProcessPlanetPackNode(ConfigNode node, out PlanetPackConfig cfg)
		{
			Logging.Log($"Loading planet pack config");

			// create the config
			bool isFormatted = true;
			cfg = new PlanetPackConfig
			{
				speedMultiplier = ReadConfigValue(node, "speed_multiplier", ref isFormatted),
			};

			// read the affected body array
			ConfigNode affectedBodies = node.GetNode("AffectedBodies");
			string[] array = affectedBodies.GetValues("item");

			if (array.Length < 1)
			{
				Logging.Log("Planet pack config has zero affected bodies, it will not have any effect");
				return false;
			}

			cfg.affectedBodies = array;

			// is the config formatted correctly?
			if (!isFormatted)
			{
				Logging.Log($"Planet pack config is not formatted correctly: {node.name}");
				return false;
			}

			return true;
		}

		/// <summary>
		/// Processes the colors node of a body
		/// </summary>
		bool ProcessBodyColors(ConfigNode rootNode, out BodyColors body)
		{
			body = new BodyColors();

			ConfigNode colorNode = new ConfigNode();
			bool isFormatted = rootNode.TryGetNode("Color", ref colorNode);
			if (!isFormatted) return false;

			body.glow = ReadConfigColorHDR(colorNode, "glow", ref isFormatted);
			body.glowHot = ReadConfigColorHDR(colorNode, "glow_hot", ref isFormatted);
			body.trailPrimary = ReadConfigColorHDR(colorNode, "trail_primary", ref isFormatted);
			body.trailSecondary = ReadConfigColorHDR(colorNode, "trail_secondary", ref isFormatted);
			body.trailTertiary = ReadConfigColorHDR(colorNode, "trail_tertiary", ref isFormatted);
			body.wrapLayer = ReadConfigColorHDR(colorNode, "wrap_layer", ref isFormatted);
			body.wrapStreak = ReadConfigColorHDR(colorNode, "wrap_streak", ref isFormatted);
			body.shockwave = ReadConfigColorHDR(colorNode, "shockwave", ref isFormatted);

			return isFormatted;
		}

		/// <summary>
		/// Processes the transition modifiers
		/// </summary>
		bool ProcessTransitionModifiers(ConfigNode rootNode, out TransitionModifier[] mods)
		{
			mods = null;

			ConfigNode[] nodes = rootNode.GetNodes("TransitionModifier");
			mods = new TransitionModifier[nodes.Length];

			bool isFormatted = true;

			for (int i = 0; i < nodes.Length; i++)
			{
				mods[i].order = ReadConfigValue(nodes[i], "order", ref isFormatted);
				mods[i].operation = ReadConfigModifierOperation(nodes[i], "operation", ref isFormatted);
				mods[i].value = ReadConfigValue(nodes[i], "value", ref isFormatted);
				mods[i].stockfxDependent = ReadConfigBoolean(nodes[i], "stockfx_dependent", ref isFormatted);
			}

			mods = mods.OrderBy(m => m.order).ToArray();

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
		Color ReadConfigColorHDR(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateColorHDR(node.GetValue(key), out Color result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Reads the transition modifier mode enum
		/// </summary>
		ModifierOperation ReadConfigModifierOperation(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Enum.TryParse(node.GetValue(key), out ModifierOperation result);
			isFormatted = isFormatted && success;

			return result;
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
