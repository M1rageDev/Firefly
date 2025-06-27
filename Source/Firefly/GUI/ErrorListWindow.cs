using UnityEngine;

namespace Firefly.GUI
{
	internal class ErrorListWindow : Window
	{
		GUIStyle seriousErrorStyle = new GUIStyle();

		// ui stuff
		Vector2 ui_errorListPosition;

		public ErrorListWindow() : base("Firefly Error List")
		{
			windowRect = new Rect(300, 100, 600, 100);
			if (ErrorManager.Instance.errorList.Count > 0)
			{
				Show();
			}

			seriousErrorStyle.normal.textColor = Color.red;
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();

			// notification about serious errors
			if (ErrorManager.Instance.seriousErrors.Count > 0)
			{
				GUILayout.Label($"The loader detected {ErrorManager.Instance.seriousErrors.Count} serious errors. These will make the mod NOT function properly or AT ALL.", seriousErrorStyle);
			} else
			{
				GUILayout.Label("These errors are not serious, but will likely make something not work (like custom configs not getting applied)");
			}

			// notification about incorrect install
			if (ErrorManager.Instance.anyInstallErrors)
			{
				GUILayout.Space(20f);
				GUILayout.Label("One or more of the errors are probably caused by an incorrect install of the mod. Please make sure you installed it according to the install instructions.");
				GUILayout.Label("We highly recommend using CKAN to install mods.");
			}

			// draw the scrollview with the errors
			DrawErrorList();

			// draw close button
			if (GUILayout.Button("Close error list (ignore errors)")) Hide();

			GUILayout.EndVertical();
			UnityEngine.GUI.DragWindow();
		}

		void DrawErrorList()
		{
			GUILayout.Space(20f);
			ui_errorListPosition = GUILayout.BeginScrollView(ui_errorListPosition, GUILayout.Height(300f));

			GUILayout.BeginVertical();
			GuiUtils.DrawHorizontalSeparator(400f);
			for (int i = 0; i < ErrorManager.Instance.errorList.Count; i++)
			{
				DrawError(ErrorManager.Instance.errorList[i]);
				GuiUtils.DrawHorizontalSeparator(400f);
			}
			GUILayout.EndVertical();

			GUILayout.EndScrollView();
		}

		void DrawError(ModLoadError error)
		{
			if (error.isSerious)
			{
				GUILayout.Label("This error is serious. It will likely make the mod not work at all.", seriousErrorStyle);
			}
			GUILayout.Label("Error source: " + error.sourcePath);
			GUILayout.Label("Error description: " + error.description);
			GUILayout.Label("Probable cause: " + error.cause.ToString());
		}
	}
}
