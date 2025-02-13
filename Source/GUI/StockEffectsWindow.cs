using UnityEngine;
using System.Collections.Generic;

namespace Firefly
{
	internal class StockEffectsWindow
	{
		public Rect windowPosition = new Rect(300, 100, 600, 50);
		public bool windowActive = false;

		public void Gui(int id)
		{
			GUILayout.BeginVertical();

			GUILayout.Label("The mod detected that the \"Aero effect quality\" setting is set higher than minimal.");
			GUILayout.Label("This will make the stock effects mix with the ones from Firefly.");

			// draw close button
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Fix problem")) FixProblem();
			if (GUILayout.Button("Ignore")) windowActive = false;
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		void FixProblem()
		{
			GameSettings.AERO_FX_QUALITY = 0;
			windowActive = false;
		}
	}
}
