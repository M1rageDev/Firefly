using System.Collections.Generic;
using UnityEngine;

namespace Firefly.GUI
{
    class Window
    {
        public static List<Window> AllWindows = new List<Window>();

		public Rect windowRect = new Rect(900f, 100f, 300f, 300f);
        public string title = "FireflyWindow";
		public bool isGlobal = false;  // if the window should be drawn even if the app is closed

		public bool show = false;
        int id;

        public Window(string title)
        {
            this.title = title;
            this.id = GetHashCode();

			AllWindows.Add(this);
		}

        public virtual void Show()
        {
            show = true;
        }

		public virtual void Draw(int id)
		{
			UnityEngine.GUI.DragWindow();
		}

		public virtual void Hide()
		{
			show = false;
		}

		public void RunGui()
        {
            if (!show) return;

			windowRect = GUILayout.Window(this.id, windowRect, Draw, title);
		}

        public void OnDestroy()
        {
			AllWindows.Remove(this);
		}
    }
}
