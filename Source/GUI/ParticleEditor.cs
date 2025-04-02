using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Firefly.GUI
{
    class ParticleEditor : Window
	{
		public static ParticleEditor Instance { get; private set; }

		public ParticleEditor() : base("Particle Editor")
		{
			windowRect = new Rect(900f, 100f, 300f, 300f);

			Instance = this;
		}

		public override void Draw(int id)
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Use the Effect Editor's sim sliders to make the particles show");
			GUILayout.EndVertical();

			UnityEngine.GUI.DragWindow();
		}

		public override void Show()
		{
			base.Show();
		}

		public override void Hide()
		{
			base.Hide();
		}
	}
}
