using UnityEngine;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class CameraManager : MonoBehaviour
	{
		public static CameraManager Instance { get; private set; }

		public bool isHdr = false;

		public bool ActualHdrState { get 
			{
				if (Camera.main != null) return Camera.main.allowHDR;
				else return isHdr;
			} 
		}

		public void Awake()
		{
			Instance = this;
			
			OverrideHDR(ConfigManager.Instance.modSettings.hdrOverride);
		}

		/// <summary>
		/// Sets the HDR option for the main and IVA cameras
		/// </summary>
		public void OverrideHDR(bool hdr)
		{
			isHdr = hdr;

			ConfigManager.Instance.modSettings.hdrOverride = hdr;

			if (Camera.main != null)
			{
				Camera.main.allowHDR = hdr;
			}

			if (InternalCamera.Instance != null)
			{
				InternalCamera.Instance.GetComponent<Camera>().allowHDR = hdr;
			}
		}
	}
}
