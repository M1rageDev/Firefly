using System;
using System.Collections.Generic;
using UnityEngine;

namespace Firefly.GUI
{
	internal class GuiUtils
	{
		public class UiObjectInput<T>
		{
			public string[] text;
			public object value;

			public UiObjectInput(object value, int textCount = 1)
			{
				this.text = new string[textCount];
				this.value = value;
			}

			public T GetValue()
			{
				return (T)value;
			}

			public void Overwrite(object value)
			{
				this.value = value;

				// if the value is a floatpair, we need to set both values
				if (value is FloatPair pair)
				{
					this.text[0] = pair.x.ToString();
					this.text[1] = pair.y.ToString();
				}
				else
				{
					this.text[0] = value.ToString();
				}
			}
		}

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
		public static void DrawFloatInput(string label, ref UiObjectInput<float> uiInput, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			uiInput.text[0] = GUILayout.TextField(uiInput.text[0]);
			bool hasValue = float.TryParse(uiInput.text[0], out float v);
			if (hasValue) uiInput.value = v;

			GUILayout.EndHorizontal();
		}
		public static void DrawFloatInput(string label, ref string text, ref float value, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			text = GUILayout.TextField(text);
			bool hasValue = float.TryParse(text, out float v);
			if (hasValue) value = v;

			GUILayout.EndHorizontal();
		}

		// draws a string input field
		public static void DrawStringInput(string label, ref string text, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			text = GUILayout.TextField(text);

			GUILayout.EndHorizontal();
		}

		// draws a boolean input field
		public static void DrawBoolInput(string label, ref bool val, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			val = GUILayout.Toggle(val, "");

			GUILayout.EndHorizontal();
		}

		// draws a float pair input field (2 float values)
		public static void DrawFloatPairInput(string label, ref UiObjectInput<FloatPair> uiInput, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			FloatPair newValue = (FloatPair)uiInput.value;

			uiInput.text[0] = GUILayout.TextField(uiInput.text[0]);
			bool hasValue = float.TryParse(uiInput.text[0], out float v);
			if (hasValue) newValue.x = v;

			uiInput.text[1] = GUILayout.TextField(uiInput.text[1]);
			hasValue = float.TryParse(uiInput.text[1], out v);
			if (hasValue) newValue.y = v;

			uiInput.value = newValue;

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
