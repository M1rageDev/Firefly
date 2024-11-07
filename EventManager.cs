using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class EventManager : MonoBehaviour
	{
		public static Dictionary<Guid, AtmoFxModule> fxInstances = new Dictionary<Guid, AtmoFxModule>();

		/// <summary>
		/// Registers one instance of an effect vesselmodule, which will get sent events
		/// </summary>
		public static void RegisterInstance(Guid id, AtmoFxModule instance)
		{
			if (fxInstances.ContainsKey(id)) fxInstances[id] = instance;
			else fxInstances.Add(id, instance);
		}

		/// <summary>
		/// Unregisters one vessel ID
		/// </summary>
		public static void UnregisterInstance(Guid id)
		{
			if (fxInstances.ContainsKey(id)) fxInstances.Remove(id);
		}

		public void Start()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			GameEvents.onDockingComplete.Add(DockingEvent);
			GameEvents.onVesselPartCountChanged.Add(ModifiedEventFunction);
			GameEvents.onVesselSOIChanged.Add(SoiChangeFunction);
		}

		public void OnDestroy()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			GameEvents.onDockingComplete.Remove(DockingEvent);
			GameEvents.onVesselPartCountChanged.Remove(ModifiedEventFunction);
			GameEvents.onVesselSOIChanged.Remove(SoiChangeFunction);

			// clear dict
			EventManager.fxInstances.Clear();
		}

		/// <summary>
		/// Starts the coroutine for a docking event
		/// </summary>
		void DockingEvent(GameEvents.FromToAction<Part, Part> action)
		{
			Logging.Log("Docked vessels");

			StartCoroutine(DockingEventCoroutine(action));
		}

		/// <summary>
		/// Docking event coroutine, runs the function after 5 frames
		/// </summary>
		IEnumerator DockingEventCoroutine(GameEvents.FromToAction<Part, Part> action)
		{
			for (int i = 0; i < 5; i++)
			{
				yield return null;
			}

			DockingEventFunction(action);
		}

		/// <summary>
		/// Actual behaviour of the docking event, sends a reload event to both vessels
		/// </summary>
		void DockingEventFunction(GameEvents.FromToAction<Part, Part> action)
		{
			Logging.Log($"Docked vessels {action.from.vessel.name}, {action.to.vessel.name}");

			if (fxInstances.TryGetValue(action.from.vessel.id, out AtmoFxModule module)) module.DoFullReload();
			if (fxInstances.TryGetValue(action.to.vessel.id, out module)) module.DoFullReload();
		}

		/// <summary>
		/// Fires everytime a vessel is modified, sends a reload event
		/// </summary>
		void ModifiedEventFunction(Vessel vessel)
		{
			Logging.Log($"Modified vessel {vessel.name}");

			if (fxInstances.TryGetValue(vessel.id, out AtmoFxModule module)) module.OnVesselModified();
			else Logging.Log("FX instance not registered");
		}

		/// <summary>
		/// Fires everytime a vessel changes it's SOI
		/// </summary>
		void SoiChangeFunction(GameEvents.HostedFromToAction<Vessel, CelestialBody> action)
		{
			if (fxInstances.TryGetValue(action.host.id, out AtmoFxModule module))
			{
				module.UpdateCurrentBody(ConfigManager.Instance.GetVesselBody(action.host), action.to);
			}
		}
	}
}
