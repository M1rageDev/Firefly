using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class CameraManager : MonoBehaviour
	{
		public static CameraManager Instance { get; private set; }

		public bool isHdr = false;

		List<KeyValuePair<CameraEvent,CommandBuffer>> cameraBuffers = new List<KeyValuePair<CameraEvent,CommandBuffer>>();

		public bool ActualHdrState { get 
			{
				if (Camera.main != null) return Camera.main.allowHDR;
				else return isHdr;
			} 
		}

		public void Awake()
		{
			Instance = this;

			OverrideHDR((bool)ModSettings.I["hdr_override"]);
		}

		/// <summary>
		/// Adds a command buffer to the flight camera
		/// </summary>
		public void AddCommandBuffer(CameraEvent evt, CommandBuffer buf)
		{
			for (int i = 0; i < cameraBuffers.Count; i++)
			{
				if (cameraBuffers[i].Key == evt && cameraBuffers[i].Value == buf) return;
			}

			// add the CB
			Camera flightCam = FlightCamera.fetch.mainCamera;
			if (flightCam == null) return;

			CommandBuffer[] buffers = flightCam.GetCommandBuffers(evt);
			if (buffers.Contains(buf)) return;  // detect duplicates

			flightCam.AddCommandBuffer(evt, buf);

			// add the CB to the global list
			cameraBuffers.Add(new KeyValuePair<CameraEvent,CommandBuffer>(evt, buf));
		}

		/// <summary>
		/// Removes a specified command buffer from the flight camera
		/// </summary>
		public void RemoveCommandBuffer(CameraEvent evt, CommandBuffer buf)
		{
			FlightCamera.fetch.mainCamera?.RemoveCommandBuffer(evt, buf);

			for (int i = 0; i < cameraBuffers.Count; i++)
			{
				if (cameraBuffers[i].Key == evt && cameraBuffers[i].Value == buf)
				{
					cameraBuffers.RemoveAt(i);
					break;
				}
			}
		}

		/// <summary>
		/// Sets the HDR option for the main and IVA cameras
		/// </summary>
		public void OverrideHDR(bool hdr)
		{
			isHdr = hdr;

			ModSettings.I["hdr_override"] = hdr;

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
