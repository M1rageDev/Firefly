using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Firefly
{
	public class ModSettings
	{
		public static ModSettings I { get; private set; }

		public enum ValueType
		{
			Boolean,
			Float,
			Float3
		}

		public class Field
		{
			public object value;
			public ValueType valueType;

			public bool needsReload;

			public string uiText;

			public Field(object value, ValueType valueType, bool needsReload)
			{
				this.value = value;
				this.valueType = valueType;
				this.needsReload = needsReload;

				this.uiText = value.ToString();
			}
		}

		public Dictionary<string, Field> fields;

		public ModSettings()
		{
			this.fields = new Dictionary<string, Field>();

			I = this;
		}

		public static ModSettings CreateDefault()
		{
			ModSettings ms = new ModSettings
			{
				fields = new Dictionary<string, Field>()
				{
					{ "hdr_override", new Field(true, ValueType.Boolean, false) },
					{ "disable_bowshock", new Field(false, ValueType.Boolean, false) },
					{ "disable_particles", new Field(false, ValueType.Boolean, true) },
					{ "strength_base", new Field(2800f, ValueType.Float, false) },
					{ "length_mult", new Field(1f, ValueType.Float, false) }
				}
			};

			return ms;
		}

		/// <summary>
		/// Saves every field to a ConfigNode
		/// </summary>
		public void SaveToNode(ref ConfigNode node)
		{
			for (int i = 0; i < fields.Count; i++)
			{
				KeyValuePair<string, Field> elem = fields.ElementAt(i);
				object val = elem.Value.value;

				// special case for float3
				if (elem.Value.valueType == ValueType.Float3)
				{
					Vector3 vec = (Vector3)val;

					val = string.Join(" ", vec.x, vec.y, vec.z);
				}

				// save
				node.AddValue(elem.Key, val);

				Logging.Log($"ModSettings -  Saved {elem.Key} to node as {val}");
			}
		}

		public override string ToString()
		{
			string result = "";

			for (int i = 0; i < fields.Count; i++)
			{
				KeyValuePair<string, Field> element = fields.ElementAt(i);
				result += $"<{element.Value.valueType}>{element.Key}";
				result += $": {element.Value.value}";
				result += "\n";
			}

			return result;
		}

		/// <summary>
		/// Gets the type of a field value
		/// </summary>
		public ValueType? GetFieldType(string key)
		{
			if (fields.ContainsKey(key))
			{
				return fields[key].valueType;
			}

			return null;
		}

		/// <summary>
		/// Gets a field from the dict specified by a key
		/// </summary>
		public Field GetField(string key)
		{
			if (fields.ContainsKey(key))
			{
				return fields[key];
			}

			return null;
		}

		// custom indexer
		public object this[string i]
		{
			get => fields[i].value;
			set => fields[i].value = value;
		}
	}

	/// <summary>
	/// Class which manages the entire settings system. It is initialized by the ConfigManager.
	/// </summary>
	internal class SettingsManager
	{
		public static SettingsManager Instance { get; private set; }
		public const string SettingsPath = "GameData/Firefly/ModSettings.cfg";

		public ModSettings modSettings = ModSettings.CreateDefault();

		public SettingsManager()
		{
			Instance = this;

			Logging.Log("Initialized SettingsManager");
		}

		/// <summary>
		/// Saves the mod setting overrides
		/// </summary>
		public void SaveModSettings()
		{
			Logging.Log("Saving mod settings");

			// create a parent node
			ConfigNode parent = new ConfigNode("ATMOFX_SETTINGS");

			// create the node
			ConfigNode node = new ConfigNode("ATMOFX_SETTINGS");

			modSettings.SaveToNode(ref node);

			// add to parent and save
			parent.AddNode(node);
			parent.Save(KSPUtil.ApplicationRootPath + SettingsPath);
		}

		/// <summary>
		/// Loads the mod settings
		/// </summary>
		public void LoadModSettings()
		{
			// load settings
			ConfigNode[] settingsNodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_SETTINGS");
			modSettings = ModSettings.CreateDefault();

			if (settingsNodes.Length < 1)
			{
				// we don't have any saved settings or the user deleted the cfg file
				Logging.Log("Using default mod settings");
				return;
			}

			ConfigNode settingsNode = settingsNodes[0];

			// load the actual stuff from the ConfigNode
			bool isFormatted = true;
			for (int i = 0; i < modSettings.fields.Count; i++)
			{
				KeyValuePair<string, ModSettings.Field> e = modSettings.fields.ElementAt(i);

				modSettings[e.Key] = ReadSettingsField(settingsNode, e.Key, ref isFormatted);
			}

			if (!isFormatted)
			{
				Logging.Log("Settings cfg formatted incorrectly");
				modSettings = ModSettings.CreateDefault();
			}

			Logging.Log("Loaded Mod Settings: \n" + modSettings.ToString());
		}

		/// <summary>
		/// Reads one boolean value from a node
		/// </summary>
		object ReadSettingsField(ConfigNode node, string field, ref bool isFormatted)
		{
			string value = node.GetValue(field);
			ModSettings.ValueType? type = modSettings.GetFieldType(field);

			if (value == null)
			{
				isFormatted = false;
				return null;
			}

			bool success = false;
			object result = default;
			switch (type)
			{
				case ModSettings.ValueType.Boolean:
					bool result_bool;
					success = Utils.EvaluateBool(value, out result_bool);
					result = result_bool;
					break;
				case ModSettings.ValueType.Float:
					float result_float;
					success = Utils.EvaluateFloat(value, out result_float);
					result = result_float;
					break;
				case ModSettings.ValueType.Float3:
					Vector3 result_float3;
					success = Utils.EvaluateFloat3(value, out result_float3);
					result = result_float3;
					break;
				default: break;
			}

			isFormatted = isFormatted && success;

			return result;
		}
	}
}
