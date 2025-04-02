using UnityEngine;

namespace Firefly.GUI
{
	internal class StockEffectsWindow : Window
	{
		public StockEffectsWindow() : base("Firefly Stock Effects Warning")
		{
			windowRect = new Rect(300, 100, 600, 50);
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();

			GUILayout.Label("The mod detected that the \"Aero effect quality\" setting is set higher than minimal.");
			GUILayout.Label("This will make the stock effects mix with the ones from Firefly.");

			// draw close button
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Fix problem")) FixProblem();
			if (GUILayout.Button("Ignore")) Hide();
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();
			UnityEngine.GUI.DragWindow();
		}

		void FixProblem()
		{
			GameSettings.AERO_FX_QUALITY = 0;
			Hide();
		}
	}
}
