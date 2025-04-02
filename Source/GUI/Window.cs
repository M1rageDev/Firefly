using System.Collections.Generic;
using UnityEngine;

namespace Firefly.GUI
{
    class Window
    {
		public Rect windowRect = new Rect(900f, 100f, 300f, 300f);
		public bool show = false;
        public string title = "FireflyWindow";
        int id;

        public Window(string title)
        {
            this.title = title;
            this.id = GetHashCode();
		}

        public virtual void Show()
        {
            show = true;
        }

        public void RunGui()
        {
            if (!show) return;

			windowRect = GUILayout.Window(this.id, windowRect, Draw, title);
		}

        public virtual void Draw(int id)
        {
			UnityEngine.GUI.DragWindow();
		}

        public virtual void Hide()
        {
			show = false;
		}
    }
}
