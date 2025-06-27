using System.Linq;
using UnityEngine;

namespace FireflyAPI
{
	public struct FloatPair
	{
		public float x;
		public float y;

		public FloatPair(float x, float y)
		{
			this.x = x;
			this.y = y;
		}

		public override string ToString()
		{
			return x + " " + y;
		}
	}

	public enum ValueType
	{
		Boolean,
		Float,
		Float3,
		FloatPair
	}

	public class ConfigField
	{
		public object value;
		public ValueType valueType;

		public bool needsReload;

		public string uiText;

		// used for values with more than one value
		public string uiText1;
		public string uiText2;

		public ConfigField(object value, ValueType valueType, bool needsReload = false)
		{
			this.value = value;
			this.valueType = valueType;
			this.needsReload = needsReload;

			UpdateUiText();
		}

		public ConfigField(ConfigField template)
		{
			this.value = template.value;
			this.valueType = template.valueType;
			this.needsReload = template.needsReload;

			this.uiText = template.uiText;
			this.uiText1 = template.uiText1;
			this.uiText2 = template.uiText2;
		}

		public void UpdateUiText()
		{
			if (valueType == ValueType.Float3)
			{
				Vector3 vec = (Vector3)value;
				uiText = vec.x.ToString();
				uiText1 = vec.y.ToString();
				uiText2 = vec.z.ToString();
			}
			else if (valueType == ValueType.FloatPair)
			{
				FloatPair pair = (FloatPair)value;
				uiText = pair.x.ToString();
				uiText1 = pair.y.ToString();
			}
			else
			{
				uiText = value.ToString();
			}
		}

		public object GetValueForSave()
		{
			object ret = value;

			// special cases
			if (valueType == ValueType.Float3)
			{
				Vector3 vec = (Vector3)value;

				ret = string.Join(" ", vec.x, vec.y, vec.z);
			}
			else if (valueType == ValueType.FloatPair)
			{
				FloatPair pair = (FloatPair)value;

				ret = string.Join(" ", pair.x, pair.y);
			}

			return ret;
		}

		public void ParseString(string x, ref bool isFormatted, bool needsValue = false)
		{
			if (x == null)
			{
				if (needsValue) isFormatted = false;
				return;
			}

			bool success = false;
			switch (valueType)
			{
				case ValueType.Boolean:
					bool result_bool;
					success = Utils.EvaluateBool(x, out result_bool);
					this.value = result_bool;
					break;
				case ValueType.Float:
					float result_float;
					success = Utils.EvaluateFloat(x, out result_float);
					this.value = result_float;
					break;
				case ValueType.FloatPair:
					FloatPair result_pair;
					success = Utils.EvaluateFloatPair(x, out result_pair);
					this.value = result_pair;
					break;
				case ValueType.Float3:
					Vector3 result_float3;
					success = Utils.EvaluateFloat3(x, out result_float3);
					this.value = result_float3;
					break;
				default: break;
			}
			isFormatted = isFormatted && success;
		}
	}

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

		public static implicit operator Color(HDRColor x)
		{
			return x.hdr;
		}
	}

	public static class Utils
	{
		public static bool EvaluateFloat(string text, out float val)
		{
			return float.TryParse(text, out val);
		}

		public static bool EvaluateInt(string text, out int val)
		{
			return int.TryParse(text, out val);
		}

		public static bool EvaluateBool(string text, out bool val)
		{
			return bool.TryParse(text.ToLower(), out val);
		}

		public static bool EvaluateFloatPair(string text, out FloatPair val)
		{
			bool isFormatted = true;
			val = new FloatPair(0f, 0f);

			string[] channels = text.Split(' ');
			if (channels.Length < 2) return false;

			// evaluate the values
			isFormatted = isFormatted && EvaluateFloat(channels[0], out val.x);
			isFormatted = isFormatted && EvaluateFloat(channels[1], out val.y);

			return isFormatted;
		}

		public static bool EvaluateFloat3(string text, out Vector3 val)
		{
			bool isFormatted = true;
			val = Vector3.zero;

			string[] channels = text.Split(' ');
			if (channels.Length < 3) return false;

			// evaluate the values
			isFormatted = isFormatted && EvaluateFloat(channels[0], out val.x);
			isFormatted = isFormatted && EvaluateFloat(channels[1], out val.y);
			isFormatted = isFormatted && EvaluateFloat(channels[2], out val.z);

			return isFormatted;
		}

		// converts an SDRI color (I stored in alpha) to an HDR color
		public static Color SDRI_To_HDR(Color sdri)
		{
			float factor = Mathf.Pow(2f, sdri.a);
			return new Color(sdri.r * factor, sdri.g * factor, sdri.b * factor);
		}

		// converts an SDRI color to an HDR color
		public static Color SDRI_To_HDR(float r, float g, float b, float i)
		{
			float factor = Mathf.Pow(2f, i);
			return new Color(r * factor, g * factor, b * factor);
		}

		public static bool EvaluateColorHDR(string text, out Color val, out Color sdr)
		{
			bool isFormatted = true;
			val = Color.magenta;
			sdr = Color.magenta;

			string[] channels = text.Split(' ');
			if (channels.Length < 4) return false;

			// evaluate the values
			float r = 0f;
			float g = 0f;
			float b = 0f;
			float i = 0f;
			isFormatted = isFormatted && EvaluateFloat(channels[0], out r);
			isFormatted = isFormatted && EvaluateFloat(channels[1], out g);
			isFormatted = isFormatted && EvaluateFloat(channels[2], out b);
			isFormatted = isFormatted && EvaluateFloat(channels[3], out i);

			// divide by 255 to convert into 0-1 range
			r /= 255f;
			g /= 255f;
			b /= 255f;

			val = SDRI_To_HDR(r, g, b, i);
			sdr = new Color(r, g, b, i);

			return isFormatted;
		}

		/// <summary>
		/// Returns the cfg name from a part.partInfo.name field
		/// </summary>
		public static string GetPartCfgName(string name)
		{
			return name.Replace('.', '_');
		}

		/// <summary>
		/// Is part legible for bound calculations?
		/// </summary>
		public static bool IsPartBoundCompatible(Part part)
		{
			return IsPartCompatible(part) && !(
				part.Modules.Contains("ModuleParachute")
			);
		}

		/// <summary>
		/// Is part legible for fx envelope calculations?
		/// </summary>
		public static bool IsPartCompatible(Part part)
		{
			return !(
				part.Modules.Contains("ModuleConformalDecal") ||
				part.Modules.Contains("ModuleConformalFlag") ||
				part.Modules.Contains("ModuleConformalText") ||
				part.name.Contains("RadialDrill")  // TODO: Actually fix this, instead of making a workaround like this
			);
		}

		/// <summary>
		/// Landing gear have flare meshes for some reason, this function checks if a mesh is a flare or not
		/// </summary>
		public static bool CheckWheelFlareModel(Part part, string model)
		{
			bool isFlare = string.Equals(model, "flare", System.StringComparison.OrdinalIgnoreCase);
			bool isWheel = part.HasModuleImplementing<ModuleWheelBase>();

			return isFlare && isWheel;
		}

		/// <summary>
		/// Check if a model's layer is incorrect
		/// </summary>
		public static bool CheckLayerModel(Transform model)
		{
			return (
				model.gameObject.layer == 1
			);
		}

		public static Vector3 VectorDivide(Vector3 a, Vector3 b)
		{
			return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
		}

		// note - the output vector is actually a result of (1 / result), to make the shader code use multiplication instead of division
		public static Vector3 GetModelEnvelopeScale(Part part, Transform model)
		{
			if (part.name.Contains("GrapplingDevice") || part.name.Contains("smallClaw") || part.name.Contains("longAntenna"))
			{
				return VectorDivide(Vector3.one, model.localScale);
			}
			else if (part.name.Contains("largeSolarPanel"))
			{
				return Vector3.one;  // Gigantor XL needs 1, since the animation scales the panel
			}
			else
			{
				return VectorDivide(Vector3.one, model.lossyScale);
			}
		}

		public static Transform[] FindTaggedTransforms(Part part)
		{
			// finds transforms tagged with Icon_Hidden and only those with atmofx_envelope in their name
			return part.FindModelTransformsWithTag("Icon_Hidden")
				.Where(x => x.name.Contains("atmofx_envelope")).ToArray();
		}

		public static Vector3 ConvertNodeToModel(Vector3 node, Part part, Transform model)
		{
			// convert to world-space
			Vector3 worldSpace = part.transform.TransformPoint(node);

			// convert to model-space
			return model.transform.InverseTransformPoint(worldSpace);
		}

		/// <summary>
		/// Returns the angle of attack of a vessel
		/// Technically this is not the angle of attack, but it's good enough for this project
		/// </summary>
		public static float GetAngleOfAttack(Vessel vessel)
		{
			Transform transform = vessel.GetTransform();
			Vector3 velocity = vessel.srf_velocity.normalized;

			float angle = Vector3.Angle(transform.forward, velocity) * Mathf.Deg2Rad;

			return angle;
		}

		/// <summary>
		/// Returns the corners of a given Bounds object
		/// </summary>
		public static Vector3[] GetBoundCorners(Bounds bounds)
		{
			Vector3 center = bounds.center;
			float x = bounds.extents.x;
			float y = bounds.extents.y;
			float z = bounds.extents.z;

			Vector3[] corners = new Vector3[8];

			corners[0] = center + new Vector3(x, y, z);
			corners[1] = center + new Vector3(x, y, -z);
			corners[2] = center + new Vector3(-x, y, z);
			corners[3] = center + new Vector3(-x, y, -z);

			corners[4] = center + new Vector3(x, -y, z);
			corners[5] = center + new Vector3(x, -y, -z);
			corners[6] = center + new Vector3(-x, -y, z);
			corners[7] = center + new Vector3(-x, -y, -z);

			return corners;
		}

		/// <summary>
		/// Converts HSV values to a Color object
		/// </summary>
		public static Color ColorHSV(float h, float s, float v)
		{
			return Color.HSVToRGB(h, s, v);
		}

		/// <summary>
		/// Converts a Color object to HSV values
		/// </summary>
		public static void ColorHSV(Color c, out float h, out float s, out float v)
		{
			Color.RGBToHSV(c, out h, out s, out v);
		}
	}
}
