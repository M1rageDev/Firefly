using System.Collections.Generic;
using System.Linq;

namespace FireflyAPI
{
	public class BodyColors
	{
		public Dictionary<string, HDRColor?> fields = new Dictionary<string, HDRColor?>();

		// custom indexer
		public HDRColor? this[string i]
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
				KeyValuePair<string, HDRColor?> elem = fields.ElementAt(i);
				node.AddValue(elem.Key, elem.Value.Value.SDRIString());
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
			fields.Add("strength_multiplier", new ConfigField(1f, FireflyAPI.ValueType.Float));
			fields.Add("length_multiplier", new ConfigField(1f, FireflyAPI.ValueType.Float));
			fields.Add("opacity_multiplier", new ConfigField(1f, FireflyAPI.ValueType.Float));
			fields.Add("glow_multiplier", new ConfigField(1f, FireflyAPI.ValueType.Float));
			fields.Add("wrap_opacity_multiplier", new ConfigField(1f, FireflyAPI.ValueType.Float));
			fields.Add("wrap_fresnel_modifier", new ConfigField(1f, FireflyAPI.ValueType.Float));
			fields.Add("particle_threshold", new ConfigField(1800f, FireflyAPI.ValueType.Float));
			fields.Add("streak_probability", new ConfigField(0f, FireflyAPI.ValueType.Float));
			fields.Add("streak_threshold", new ConfigField(0f, FireflyAPI.ValueType.Float));  // range is 0-1, where 1 is 4000 m/s, default is 0.5
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
}
