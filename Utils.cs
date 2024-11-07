using UnityEngine;

namespace Firefly
{
	public static class AtmoFxLayers
	{
		public const int Spacecraft = 0;
		public const int Fx = 15;
	}

	public class Logging
	{
		public const string Prefix = "[Firefly] ";

		public static void Log(object message)
		{
			Debug.Log(Prefix + message);
		}
	}

	public static class Utils
	{
		public static bool EvaluateFloat(string text, out float val)
		{
			return float.TryParse(text, out val);
		}

		public static bool EvaluateBool(string text, out bool val)
		{
			return bool.TryParse(text.ToLower(), out val);
		}

		public static bool EvaluateColorHDR(string text, out Color val)
		{
			bool isFormatted = true;
			val = Color.magenta;

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

			float factor = Mathf.Pow(2f, i);
			val = new Color(r * factor, g * factor, b * factor);

			return isFormatted;
		}
	}
}
