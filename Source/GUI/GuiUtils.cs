using System;
using System.Collections.Generic;
using UnityEngine;

namespace Firefly.GUI
{
	internal class GuiUtils
	{
		// draws a setting bool override field
		public static void DrawConfigFieldBool(string label, Dictionary<string, ModSettings.Field> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			tgl[label].value = GUILayout.Toggle((bool)tgl[label].value, label + needsReload);
		}

		// draws a setting float override field
		public static void DrawConfigFieldFloat(string label, Dictionary<string, ModSettings.Field> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			GUILayout.BeginHorizontal();
			GUILayout.Label(label + needsReload);

			tgl[label].uiText = GUILayout.TextField(tgl[label].uiText);
			bool hasValue = float.TryParse(tgl[label].uiText, out float value);
			if (hasValue) tgl[label].value = value;

			GUILayout.EndHorizontal();
		}

		// draws a labeled slider
		public static float LabelSlider(string label, float value, float startValue, float endValue)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);

			float v = GUILayout.HorizontalSlider(value, startValue, endValue);

			GUILayout.EndHorizontal();

			return v;
		}

		// draws a float input field
		public static void DrawFloatInput(string label, ref string text, ref float value, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			text = GUILayout.TextField(text);
			bool hasValue = float.TryParse(text, out float v);
			if (hasValue) value = v;

			GUILayout.EndHorizontal();
		}

		// gets rect point from mouse point
		public static bool GetRectPoint(Vector2 point, Rect rect, out Vector2 result)
		{
			if (rect.Contains(point))
			{
				result = new Vector2(
					Mathf.Clamp(point.x - rect.xMin, 0f, rect.width),
					rect.width - Mathf.Clamp(point.y - rect.yMin, 0f, rect.height)
				);

				return true;
			}

			result = Vector2.zero;
			return false;
		}

		// draws a button with a color
		// pix texture should be a 1x1 white texture
		public static bool DrawColorButton(string label, Texture2D pix, Color color)
		{
			GUILayout.BeginHorizontal();

			GUILayout.Label(label);

			bool b = GUILayout.Button("", GUILayout.Width(60), GUILayout.Height(20));
			Rect rect = GUILayoutUtility.GetLastRect();
			rect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
			UnityEngine.GUI.DrawTexture(rect, pix, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);

			GUILayout.EndHorizontal();

			return b;
		}
	}
}
