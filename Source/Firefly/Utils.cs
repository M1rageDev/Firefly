using UnityEngine;
using FireflyAPI;

namespace Firefly
{
	public static class AtmoFxLayers
	{
		public const int Spacecraft = 0;
		public const int Fx = 23;
	}

	public class Logging
	{
		public const string Prefix = "[Firefly] ";

		public static void Log(object message)
		{
			Debug.Log(Prefix + message);
		}
	}

	internal class TextureUtils
	{
		public static Texture2D GenerateHueTexture(int width, int height)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				c = Utils.ColorHSV((float)x / (float)width, 1f, 1f);
				for (int y = 0; y < height; y++)
				{
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateHueTexture(int width, int height, float s, float v)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				c = Utils.ColorHSV((float)x / (float)width, s, v);
				for (int y = 0; y < height; y++)
				{
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateGradientTexture(int width, int height, Color c1, Color c2)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				c = Color.Lerp(c1, c2, (float)x / (float)width);
				for (int y = 0; y < height; y++)
				{
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateGradientTexture(int width, int height, float hue)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					c = Utils.ColorHSV(hue, (float)x / (float)width, (float)y / (float)height);
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateColorTexture(int width, int height, Color c)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateSelectorTexture(int width, int height, int border, Color insideColor, Color color)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					if (x < border || x > width - 1 - border) c = color;
					else if (y < border || y > height - 1 - border) c = color;
					else c = insideColor;

					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}
	}
}
