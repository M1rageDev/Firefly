using System.Collections.Generic;
using UnityEngine;

namespace Firefly.GUI
{
    class Popup : Window
    {
		public enum CallbackType 
		{
			Ok,
			Cancel
		}

		public delegate void PopupCallback(CallbackType type);
		public delegate void PopupDraw();

		public Dictionary<string, object> customData = new Dictionary<string, object>();
		PopupCallback callback;
		PopupDraw draw;

		public Popup(string title, Vector2 org, PopupCallback callback, PopupDraw draw) : base("")
		{
			windowRect = new Rect(org.x, org.y, 300, 300);
			this.title = title;

			this.callback = callback;
			this.draw = draw;
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();

			// draw additional stuff
			draw();

			// draw controls
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Cancel")) Cancel();
			if (GUILayout.Button("Ok")) Ok();
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();
			UnityEngine.GUI.DragWindow();
		}

		void Cancel()
		{
			Hide();
			callback(CallbackType.Cancel);
		}

		void Ok()
		{
			Hide();
			callback(CallbackType.Ok);
		}
	}
}
