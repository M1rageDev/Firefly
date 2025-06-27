using KSP.UI.Screens;
using UnityEngine;

namespace Firefly.GUI
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class WindowManager : MonoBehaviour
	{
		public static WindowManager Instance { get; private set; }

		ApplicationLauncherButton appButton = null;

		public bool uiHidden = false;
		public bool appToggle = false;

		// windows
		public FireflyWindow fireflyWindow;
		public EffectEditor effectEditor;
		public ParticleEditor particleEditor;
		public ErrorListWindow errorListWindow;
		public StockEffectsWindow stockEffectsWindow;
		
		public void Awake()
		{
			Instance = this;

			fireflyWindow = new FireflyWindow();
			effectEditor = new EffectEditor();
			particleEditor = new ParticleEditor();
			errorListWindow = new ErrorListWindow();
			stockEffectsWindow = new StockEffectsWindow();
		}

		public void Start()
		{
			// only create the app if there are no serious errors
			if (ModLoadError.SeriousErrorCount < 1)
			{
				appButton = ApplicationLauncher.Instance.AddModApplication(
					(() => appToggle = true),
					(() => appToggle = false),
					null, null, null, null,
					ApplicationLauncher.AppScenes.FLIGHT,
					AssetLoader.Instance.iconTexture
				);
			}

			GameEvents.onHideUI.Add(OnHideUi);
			GameEvents.onShowUI.Add(OnShowUi);

			if (GameSettings.AERO_FX_QUALITY > 0)
			{
				// inform users about conflicting effects when instantiating this class
				stockEffectsWindow.Show();
			}
		}

		public void OnDestroy()
		{
			// remove everything associated with the thing
			if (appButton != null) ApplicationLauncher.Instance.RemoveModApplication(appButton);
			GameEvents.onHideUI.Remove(OnHideUi);
			GameEvents.onShowUI.Remove(OnShowUi);
		}

		void OnHideUi()
		{
			uiHidden = true;
		}

		void OnShowUi()
		{
			uiHidden = false;
		}

		public void OnGUI()
		{
			// if F2 or there's no active vessel dont draw anything
			if (uiHidden || FlightGlobals.ActiveVessel == null) return;

			// draw all windows
			for (int i = 0; i < Window.AllWindows.Count; i++)
			{
				Window window = Window.AllWindows[i];

				// always draw global windows, otherwise check if the app is open
				if (window.isGlobal || appToggle) window.RunGui();
			}
		}
	}
}
