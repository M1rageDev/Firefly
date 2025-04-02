using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Firefly.GUI
{
	internal class ErrorListWindow : Window
	{
		List<ModLoadError> seriousErrors = new List<ModLoadError>();
		bool anyInstallErrors = false;

		GUIStyle seriousErrorStyle = new GUIStyle();
		Vector2 ui_errorListPosition;

		Texture2D whitePixel;

		public ErrorListWindow() : base("Firefly Error List")
		{
			windowRect = new Rect(300, 100, 600, 100);
			if (ConfigManager.Instance.errorList.Count > 0)
			{
				Show();
			}

			seriousErrors = ConfigManager.Instance.errorList.Where(x => x.isSerious).ToList();
			anyInstallErrors = ConfigManager.Instance.errorList.Where(x => x.cause == ModLoadError.ProbableCause.IncorrectInstall).Count() > 0;

			seriousErrorStyle.normal.textColor = Color.red;

			whitePixel = TextureUtils.GenerateColorTexture(1, 1, Color.white);
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();

			// notification about serious errors
			if (seriousErrors.Count > 0)
			{
				GUILayout.Label($"The loader detected {seriousErrors.Count} serious errors. These will make the mod NOT function properly or AT ALL.", seriousErrorStyle);
			} else
			{
				GUILayout.Label("These errors are not serious, but will likely make something not work (like custom configs not getting applied)");
			}

			// notification about incorrect install
			if (anyInstallErrors)
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
			DrawSeparator();
			for (int i = 0; i < ConfigManager.Instance.errorList.Count; i++)
			{
				DrawError(ConfigManager.Instance.errorList[i]);
				DrawSeparator();
			}
			GUILayout.EndVertical();

			GUILayout.EndScrollView();
		}

		void DrawSeparator()
		{
			Rect rect = GUILayoutUtility.GetRect(400f, 1f, GUILayout.Width(400f));
			UnityEngine.GUI.DrawTexture(rect, whitePixel);
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
