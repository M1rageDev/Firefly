﻿using CommNet.Network;
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

		// The entry speed gets multiplied by this before getting sent to the shader
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
		// The speed gets multiplied by this after applying body configs
		public float speedMultiplier = 1f;

		// Affected bodies
		public string[] affectedBodies;
	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class ConfigManager : MonoBehaviour
	{
		public static ConfigManager Instance { get; private set; }

		public const string NewConfigPath = "GameData/Firefly/Configs/Saved/";

		public Dictionary<string, BodyConfig> bodyConfigs = new Dictionary<string, BodyConfig>();
		public List<PlanetPackConfig> planetPackConfigs = new List<PlanetPackConfig>();
		public string[] loadedBodyConfigs;

		public Dictionary<string, BodyColors> partConfigs = new Dictionary<string, BodyColors>();

		public BodyConfig defaultConfig;

		public string homeWorld;

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
					try
					{
						bool success = ProcessPlanetPackNode(nodes[i], out PlanetPackConfig cfg);
						Logging.Log($"Processing planet pack cfg '{nodes[i].name}'");

						if (!success)
						{
							Logging.Log("Planet pack cfg can't be registered");
							continue;
						}

						Logging.Log($"Successfully registered planet pack cfg '{nodes[i].name}'");
						planetPackConfigs.Add(cfg);
					} 
					catch (Exception e)  // catching plain exception, to then log it
					{
						Logging.Log($"Exception while loading planet pack {nodes[i].name}.");
						Logging.Log(e.ToString());
					}
				}
			}

			// get the nodes
			// here we're using the UrlConfig stuff, to be able to get the path of the config
			UrlDir.UrlConfig[] urlConfigs = GameDatabase.Instance.GetConfigs("ATMOFX_BODY");

			// check if there's actually anything to load
			if (urlConfigs.Length > 0)
			{
				// iterate over every node and store the data
				for (int i = 0; i < urlConfigs.Length; i++)
				{
					try
					{
						bool success = ProcessSingleNode(urlConfigs[i], out BodyConfig body);

						// couldn't load the config
						if (!success)
						{
							Logging.Log("Body couldn't be loaded");
							continue;
						}

						bodyConfigs.Add(body.bodyName, body);
					}
					catch (Exception e)  // catching plain exception, to then log it
					{
						Logging.Log($"Exception while loading config for {urlConfigs[i].config.GetValue("name")}.");
						Logging.Log(e.ToString());
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

				return;
			}

			defaultConfig = bodyConfigs["Default"];

			loadedBodyConfigs = bodyConfigs.Keys.ToArray();
		}

		/// <summary>
		/// Loads the configs for individual parts
		/// </summary>
		void LoadPartConfigs()
		{
			// clear the dict
			partConfigs.Clear();

			// get the planet packs
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
						continue;
					}

					partConfigs.Add(partId, cfg);
				}
			}
		}

		/// <summary>
		/// Processes single node
		/// </summary>
		bool ProcessSingleNode(UrlDir.UrlConfig cfg, out BodyConfig body)
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
					body.strengthMultiplier *= planetPackConfigs[i].speedMultiplier;
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

		/// <summary>
		/// Processes a single part config node
		/// </summary>
		bool ProcessPartConfigNode(ConfigNode node, out BodyColors cfg)
		{
			bool isFormatted = ProcessBodyColors(node, true, out cfg);

			return isFormatted;
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
