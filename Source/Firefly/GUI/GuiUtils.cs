using System;
using System.Collections.Generic;
using UnityEngine;

namespace Firefly.GUI
{
	internal class GuiUtils
	{
		/// <summary>
		/// Holds a value and a text input field for it
		/// Used for stuff like the effect/particle editor to simplify making the parameter controls
		/// </summary>
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

		/// <summary>
		/// Button which will ask for confirmation before doing something
		/// </summary>
		public class ConfirmingButton
		{
			public enum State
			{
				Normal,
				Confirming
			}

			public const string CONFIRM_LABEL = "Are you sure?";

			public string normalLabel;
			public float timeout;

			string currentLabel;
			State state;
			float t;

			public ConfirmingButton(string normalLabel, float timeout = 4f)
			{
				this.normalLabel = normalLabel;
				this.timeout = timeout;
				UpdateState(State.Normal);
			}

			// note that this method also ticks the logic
			public bool Draw(float time, params GUILayoutOption[] options)
			{
				bool btn = GUILayout.Button(currentLabel, options);

				// if state is confirming, then tick the time and return the button state
				if (state == State.Confirming)
				{
					if ((time - t) > timeout || btn)
					{
						UpdateState(State.Normal);  // reset state if too much time has passed or button has been pressed
					}

					return btn;
				}
				else
				{
					// if state is normal, then change label to confirmation and return false
					if (btn) UpdateState(State.Confirming, time);

					return false;
				}
			}

			void UpdateState(State newState, float time = 0f)
			{
				switch (newState)
				{
					case State.Normal:
						currentLabel = normalLabel;
						break;
					case State.Confirming:
						currentLabel = CONFIRM_LABEL;
						t = time;
						break;
					default: break;
				}

				state = newState;
			}
		}

		public static void DrawSettingsFieldBool(string label, Dictionary<string, ConfigField> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			tgl[label].value = GUILayout.Toggle((bool)tgl[label].value, label + needsReload);
		}

		public static void DrawSettingsFieldFloat(string label, Dictionary<string, ConfigField> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			GUILayout.BeginHorizontal();
			GUILayout.Label(label + needsReload);

			tgl[label].uiText = GUILayout.TextField(tgl[label].uiText);
			bool hasValue = float.TryParse(tgl[label].uiText, out float value);
			if (hasValue) tgl[label].value = value;

			GUILayout.EndHorizontal();
		}

		public static void DrawConfigField(string label, Dictionary<string, ConfigField> holder, params GUILayoutOption[] layoutOptions)
		{
			switch (holder[label].valueType)
			{
				case ValueType.Boolean:
					DrawConfigFieldBool(label, holder, layoutOptions);
					break;
				case ValueType.Float:
					DrawConfigFieldFloat(label, holder, layoutOptions);
					break;
				case ValueType.FloatPair:
					DrawConfigFieldFloatPair(label, holder, layoutOptions);
					break;
				default:
					Debug.LogError($"GuiUtils.DrawConfigField: Unknown type {holder[label].valueType} for field {label}");
					break;
			}
		}

		public static void DrawConfigFieldBool(string label, Dictionary<string, ConfigField> holder, params GUILayoutOption[] layoutOptions)
		{
			holder[label].value = GUILayout.Toggle((bool)holder[label].value, label, layoutOptions);
		}

		public static void DrawConfigFieldFloat(string label, Dictionary<string, ConfigField> holder, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			holder[label].uiText = GUILayout.TextField(holder[label].uiText);
			bool hasValue = float.TryParse(holder[label].uiText, out float value);
			if (hasValue) holder[label].value = value;

			GUILayout.EndHorizontal();
		}

		public static void DrawConfigFieldFloatPair(string label, Dictionary<string, ConfigField> holder, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			FloatPair newValue = (FloatPair)holder[label].value;

			holder[label].uiText = GUILayout.TextField(holder[label].uiText);
			bool hasValue = float.TryParse(holder[label].uiText, out float v);
			if (hasValue) newValue.x = v;

			holder[label].uiText1 = GUILayout.TextField(holder[label].uiText1);
			hasValue = float.TryParse(holder[label].uiText1, out v);
			if (hasValue) newValue.y = v;

			holder[label].value = newValue;

			GUILayout.EndHorizontal();
		}

		public static float LabelSlider(string label, float value, float startValue, float endValue)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);

			float v = GUILayout.HorizontalSlider(value, startValue, endValue);

			GUILayout.EndHorizontal();

			return v;
		}

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

		public static void DrawStringInput(string label, ref string text, float maxFieldWidth = -1f, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			if (maxFieldWidth > 0f)
			{
				text = GUILayout.TextField(text, GUILayout.MaxWidth(maxFieldWidth));
			} else
			{
				text = GUILayout.TextField(text);
			}

			GUILayout.EndHorizontal();
		}

		public static void DrawBoolInput(string label, ref bool val, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			val = GUILayout.Toggle(val, "");

			GUILayout.EndHorizontal();
		}

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

		// draws a 1px thick white line with specified width
		public static void DrawHorizontalSeparator(float width)
		{
			Rect rect = GUILayoutUtility.GetRect(width, 1f, GUILayout.Width(width));
			UnityEngine.GUI.DrawTexture(rect, Texture2D.whiteTexture);
		}

		// draws a 1px thick white line with specified height
		public static void DrawVerticalSeparator(float height)
		{
			Rect rect = GUILayoutUtility.GetRect(1f, height, GUILayout.Height(height));
			UnityEngine.GUI.DrawTexture(rect, Texture2D.whiteTexture);
		}
	}
}
