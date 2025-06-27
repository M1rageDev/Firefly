using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using FireflyAPI;

namespace Firefly
{
	/// <summary>
	/// Stores the data of an fx envelope renderer
	/// </summary>
	public struct FxEnvelopeModel
	{
		public string partName;
		public Renderer renderer;

		public Vector3 modelScale;
		public Vector3 envelopeScaleFactor;

		public FxEnvelopeModel(string partName, Renderer renderer, Vector3 modelScale, Vector3 envelopeScaleFactor)
		{
			this.partName = partName;
			this.renderer = renderer;

			this.modelScale = modelScale;
			this.envelopeScaleFactor = envelopeScaleFactor;
		}
	}

	/// <summary>
	/// Stores the data of an fx particle system instance
	/// </summary>
	public struct FxParticleSystem
	{
		public string name;

		public ParticleSystem system;

		public float offset;
		public bool useHalfOffset;

		public FloatPair rate;
		public FloatPair velocity;

		public FxParticleSystem(string name, ParticleSystem system, float offset, bool useHalfOffset, FloatPair rate, FloatPair velocity)
		{
			this.name = name;

			this.system = system;

			this.offset = offset;
			this.useHalfOffset = useHalfOffset;

			this.rate = rate;
			this.velocity = velocity;
		}
	}

	/// <summary>
	/// Stores the data and instances of the effects
	/// </summary>
	public class AtmoFxVessel
	{
		public List<FxEnvelopeModel> fxEnvelope = new List<FxEnvelopeModel>();

		public CommandBuffer commandBuffer;

		public bool hasParticles = false;

		public List<Material> particleMaterials = new List<Material>();
		public Dictionary<string, FxParticleSystem> allParticles = new Dictionary<string, FxParticleSystem>();
		public List<string> particleKeys = new List<string>();
		public bool areParticlesKilled = false;

		public Camera airstreamCamera;
		public RenderTexture airstreamTexture;

		public Vector3[] vesselBounds = new Vector3[8];
		public Vector3 vesselBoundCenter;
		public Vector3 vesselBoundExtents;
		public Vector3 vesselMinCorner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		public Vector3 vesselMaxCorner = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		public float vesselBoundRadius;
		public float vesselMaxSize;

		public float baseLengthMultiplier = 1f;

		public Material material;
	}

	/// <summary>
	/// The module which manages the effects for each vessel
	/// </summary>
	public class AtmoFxModule : VesselModule, IFireflyModule
	{
		public AtmoFxVessel fxVessel;
		public bool isLoaded = false;

		public bool debugMode = false;

		float lastFixedTime;
		float desiredRate;
		float lastStrength;

		double vslLastAlt;

		public BodyConfig currentBody;

		// override stuff
		public bool OverridePhysics { get; set; }
		public string OverridenBy { get; set; } = "Firefly internals";
		public Vector3 OverrideEntryDirection { get; set; } = Vector3.zero;
		public float OverrideEffectStrength { get; set; } = 0f;
		public float OverrideEffectState { get; set; } = 0f;
		public float OverrideAngleOfAttack { get; set; } = 0f;
		public string OverrideBodyConfigName 
		{ 
			get 
			{
				return _overrideBodyConfig.bodyName;
			} 
			set
			{
				ConfigManager.Instance.TryGetBodyConfig(value, true, out _overrideBodyConfig);
			}
		}
		BodyConfig _overrideBodyConfig = ConfigManager.Instance.DefaultConfig;

		// finds the stock handler of the aero FX
		AerodynamicsFX _aeroFX;
		public AerodynamicsFX AeroFX
		{
			get
			{
				// if the private handle isn't assigned yet, then do it now
				if (_aeroFX == null)
				{
					// find the object
					GameObject fxLogicObject = GameObject.Find("FXLogic");
					if (fxLogicObject != null)
						_aeroFX = fxLogicObject.GetComponent<AerodynamicsFX>();  // get the actual FX handling component
				}
				return _aeroFX;
			}
		}

		int reloadDelayFrames= 0;

		public override Activation GetActivation()
		{
			return Activation.LoadedVessels | Activation.FlightScene;
		}

		public void SetOverrideBodyConfig(BodyConfig cfg)
		{
			OverrideBodyConfigName = cfg.bodyName;
			_overrideBodyConfig = cfg;
		}

		public void ResetOverride()
		{
			OverridenBy = "Firefly internals";
			OverrideEntryDirection = Vector3.zero;
			OverrideEffectStrength = 0f;
			OverrideEffectState = 0f;
			OverrideAngleOfAttack = 0f;
			OverrideBodyConfigName = "Default";
		}

		/// <summary>
		/// Loads a vessel, instantiates stuff like the camera and rendertexture, also creates the entry velopes and particle system
		/// </summary>
		public void CreateVesselFx()
		{
			if (!GUI.WindowManager.Instance.fireflyWindow.tgl_EffectToggle) return;

			// check if the vessel is actually loaded, and if it has any parts
			if (vessel == null || (!vessel.loaded) || vessel.parts.Count < 1 )
			{
				Logging.Log("Invalid vessel");
				Logging.Log($"loaded: {vessel.loaded}");
				Logging.Log($"partcount: {vessel.parts.Count}");
				Logging.Log($"atmo: {vessel.mainBody.atmosphere}");
				return;
			}

			if (isLoaded) return;

			// check for atmosphere
			if (!vessel.mainBody.atmosphere)
			{
				Logging.Log("MainBody does not have an atmosphere");
				return;
			}

			bool onModify = fxVessel != null;

			Logging.Log("Loading vessel " + vessel.name);
			Logging.Log(onModify ? "Using light method" : "Using heavy method");

			Material material;

			if (onModify)
			{
				material = fxVessel.material;
			}
			else
			{
				fxVessel = new AtmoFxVessel();

				// create material
				material = Instantiate(AssetLoader.Instance.globalMaterial);
				fxVessel.material = material;

				// create camera
				GameObject cameraGO = new GameObject("AtmoFxCamera - " + vessel.name);
				fxVessel.airstreamCamera = cameraGO.AddComponent<Camera>();

				fxVessel.airstreamCamera.orthographic = true;
				fxVessel.airstreamCamera.clearFlags = CameraClearFlags.SolidColor;
				fxVessel.airstreamCamera.cullingMask = (1 << 0);  // Only render layer 0, which is for the spacecraft

				// create rendertexture
				fxVessel.airstreamTexture = new RenderTexture(512, 512, 1, RenderTextureFormat.Depth);
				fxVessel.airstreamTexture.Create();
				fxVessel.airstreamCamera.targetTexture = fxVessel.airstreamTexture;
			}

			// Check if the fxVessel or material is null
			if (fxVessel == null || material == null)
			{
				Logging.Log("fxVessel/material is null");

				RemoveVesselFx(false);
				return;
			}

			// calculate the vessel bounds
			bool correctBounds = CalculateVesselBounds(fxVessel, vessel, true);
			if (!correctBounds)
			{
				Logging.Log("Recalculating invalid vessel bounds");
				CalculateVesselBounds(fxVessel, vessel, false);
			}
			fxVessel.airstreamCamera.orthographicSize = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude, 0.3f, 2000f);  // clamp the ortho camera size
			fxVessel.airstreamCamera.farClipPlane = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude * 2f, 1f, 1000f);  // set the far clip plane so the segment occlusion works

			// set the current body
			UpdateCurrentBody(vessel.mainBody, true);

			// create the command buffer
			InitializeCommandBuffer();

			// reset part cache
			ResetPartModelCache();

			// create the fx envelopes
			UpdateFxEnvelopes();
			fxVessel.material.SetTexture("_AirstreamTex", fxVessel.airstreamTexture);  // Set the airstream depth texture parameter

			// populate the command buffer
			PopulateCommandBuffer();

			// create the particles
			if (!(bool)ModSettings.I["disable_particles"]) CreateParticleSystems(onModify);  // run the function only if they're enabled in settings

			Logging.Log("Finished loading vessel");
			isLoaded = true;
		}

		public void InitializeCommandBuffer()
		{
			fxVessel.commandBuffer = new CommandBuffer();
			fxVessel.commandBuffer.name = $"Firefly atmospheric effects [{vessel.vesselName}]";
			fxVessel.commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
			CameraManager.Instance.AddCommandBuffer(CameraEvent.AfterForwardAlpha, fxVessel.commandBuffer);
		}
		
		/// <summary>
		/// Populates the command buffer with the envelope
		/// </summary>
		public void PopulateCommandBuffer()
		{
			fxVessel.commandBuffer.Clear();

			for (int i = 0; i < fxVessel.fxEnvelope.Count; i++)
			{
				FxEnvelopeModel envelope = fxVessel.fxEnvelope[i];

				// set model values
				fxVessel.commandBuffer.SetGlobalVector("_ModelScale", envelope.modelScale);
				fxVessel.commandBuffer.SetGlobalVector("_EnvelopeScaleFactor", envelope.envelopeScaleFactor);

				// part overrides
				BodyColors colors = new BodyColors(GetCurrentConfig().colors);  // create the original colors
				if (ConfigManager.Instance.partConfigs.ContainsKey(envelope.partName))
				{
					// if the part has an override config, use it
					Logging.Log("Envelope has a part override config");
					BodyColors overrideColor = ConfigManager.Instance.partConfigs[envelope.partName];

					// override the colors with the PART config
					foreach (string key in overrideColor.fields.Keys)
					{
						if (overrideColor[key].HasValue) colors[key] = overrideColor[key];
					}
				}

				// is asteroid? if yes set randomness factor to 1, so the shader draws colored streaks
				float randomnessFactor = 0f;
				if (envelope.partName == "PotatoRoid" || envelope.partName == "PotatoComet")
				{
					Logging.Log("Potatoroid - setting the randomness factor to 1");
					randomnessFactor = 1f;
				}
				fxVessel.commandBuffer.SetGlobalVector("_RandomnessFactor", Vector2.one * randomnessFactor);

				// add commands to set the color properties
				fxVessel.commandBuffer.SetGlobalColor("_GlowColor", colors["glow"].Value);
				fxVessel.commandBuffer.SetGlobalColor("_HotGlowColor", colors["glow_hot"].Value);

				fxVessel.commandBuffer.SetGlobalColor("_PrimaryColor", colors["trail_primary"].Value);
				fxVessel.commandBuffer.SetGlobalColor("_SecondaryColor", colors["trail_secondary"].Value);
				fxVessel.commandBuffer.SetGlobalColor("_TertiaryColor", colors["trail_tertiary"].Value);
				fxVessel.commandBuffer.SetGlobalColor("_StreakColor", colors["trail_streak"].Value);

				fxVessel.commandBuffer.SetGlobalColor("_LayerColor", colors["wrap_layer"].Value);
				fxVessel.commandBuffer.SetGlobalColor("_LayerStreakColor", colors["wrap_streak"].Value);

				fxVessel.commandBuffer.SetGlobalColor("_ShockwaveColor", colors["shockwave"].Value);

				// draw the mesh
				fxVessel.commandBuffer.DrawRenderer(envelope.renderer, fxVessel.material);
			}
		}

		/// <summary>
		/// Destroys and disposes the command buffer
		/// </summary>
		public void DestroyCommandBuffer()
		{
			CameraManager.Instance.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, fxVessel.commandBuffer);
			fxVessel.commandBuffer.Dispose();
		}

		/// <summary>
		/// Resets the commandbuffer (destroys, inits and populates it)
		/// </summary>
		public void ReloadCommandBuffer()
		{
			if (fxVessel.commandBuffer == null)
			{
				Logging.Log("Command buffer is null, cannot reload it");
				return;
			}

			DestroyCommandBuffer();
			InitializeCommandBuffer();
			PopulateCommandBuffer();
		}

		/// <summary>
		/// Resets the model renderer cache for each part
		/// </summary>
		void ResetPartModelCache()
		{
			for (int i = 0; i < vessel.parts.Count; i++)
			{
				vessel.parts[i].ResetModelRenderersCache();
			}
		}

		/// <summary>
		/// Processes one part and creates the envelope mesh for it
		/// </summary>
		void CreatePartEnvelope(Part part)
		{
			Transform[] fxEnvelopes = part.FindModelTransforms("atmofx_envelope");
			if (fxEnvelopes.Length < 1) fxEnvelopes = Utils.FindTaggedTransforms(part);

			if (fxEnvelopes.Length > 0)
			{
				Logging.Log($"Part {part.name} has a defined effect envelope. Skipping mesh search.");

				for (int j = 0; j < fxEnvelopes.Length; j++)
				{
					// check if active
					if (!fxEnvelopes[j].gameObject.activeInHierarchy) continue;

					if (!fxEnvelopes[j].TryGetComponent(out MeshFilter _)) continue;
					if (!fxEnvelopes[j].TryGetComponent(out MeshRenderer parentRenderer)) continue;

					parentRenderer.enabled = false;

					// create the envelope
					FxEnvelopeModel envelope = new FxEnvelopeModel(
						Utils.GetPartCfgName(part.partInfo.name),
						parentRenderer,
						Vector3.one,
						Vector3.one
						);
					fxVessel.fxEnvelope.Add(envelope);
				}

				// skip model search
				return;
			}

			// TODO: reminder that collider support is disabled for commandbuffer branch

			List<Renderer> models = part.FindModelRenderersCached();
			for (int j = 0; j < models.Count; j++)
			{
				Renderer model = models[j];

				// check if active
				if (!model.gameObject.activeInHierarchy) continue;

				// check for wheel flare
				if (Utils.CheckWheelFlareModel(part, model.gameObject.name)) continue;

				// check for layers
				if (Utils.CheckLayerModel(model.transform)) continue;

				// is skinned
				bool isSkinnedRenderer = model.TryGetComponent(out SkinnedMeshRenderer _);

				if (!isSkinnedRenderer)  // if it's a normal model, check if it has a filter and a mesh
				{
					// try getting the mesh filter
					bool hasMeshFilter = model.TryGetComponent(out MeshFilter filter);
					if (!hasMeshFilter) continue;

					// try getting the mesh
					Mesh mesh = filter.sharedMesh;
					if (mesh == null) continue;
				}

				if (!Utils.IsPartBoundCompatible(part)) continue;

				// create the envelope
				FxEnvelopeModel envelope = new FxEnvelopeModel(
					Utils.GetPartCfgName(part.partInfo.name),
					model,
					Utils.GetModelEnvelopeScale(part, model.transform),
					new Vector3(1.05f, 1.07f, 1.05f));
				fxVessel.fxEnvelope.Add(envelope);
			}
		}

		void UpdateFxEnvelopes()
		{
			Logging.Log($"Updating fx envelopes for vessel {vessel.name}");
			Logging.Log($"Found {vessel.parts.Count} parts on the vessel");

			fxVessel.fxEnvelope.Clear();

			for (int i = 0; i < vessel.parts.Count; i++)
			{
				Part part = vessel.parts[i];
				if (!Utils.IsPartCompatible(part)) continue;

				CreatePartEnvelope(part);
			}
		}

		void CreateParticleSystems(bool onModify)
		{
			Logging.Log("Creating particle systems");

			fxVessel.hasParticles = true;

			// only recreate the particle systems if this is not a ship modification
			if (!onModify)
			{
				for (int i = 0; i < vessel.transform.childCount; i++)
				{
					Transform t = vessel.transform.GetChild(i);

					// TODO: look into other methods of doing this
					// this is stupid, I don't know why this is neccessary but it is
					// to avoid conflict with ShVAK's VaporCones mod, check the name of the transform before destroying it
					if (!t.name.Contains("FireflyPS")) continue;

					if (t.TryGetComponent(out ParticleSystem _)) Destroy(t.gameObject);
				}

				fxVessel.particleMaterials.Clear();
				fxVessel.allParticles.Clear();
				fxVessel.particleKeys.Clear();

				// spawn particle systems
				foreach (string key in ConfigManager.Instance.particleConfigs.Keys)
				{
					ParticleConfig cfg = ConfigManager.Instance.particleConfigs[key];
					CreateParticleSystem(cfg);
				}
			}
		}

		void CreateParticleSystem(ParticleConfig cfg)
		{
			if (!AssetLoader.Instance.loadedTextures.ContainsKey(cfg.mainTexture)) return;
			if (!string.IsNullOrEmpty(cfg.emissionTexture))
				if (!AssetLoader.Instance.loadedTextures.ContainsKey(cfg.emissionTexture)) return;

			if (!(bool)cfg["is_active"])
			{
				Logging.Log($"Skipping particle system {cfg.name}, since it is marked as inactive");
				return;
			}

			// instantiate prefab
			ParticleSystem ps = Instantiate(AssetLoader.Instance.loadedPrefabs[cfg.prefab], vessel.transform).GetComponent<ParticleSystem>();

			// change transform name
			ps.gameObject.name = "_FireflyPS_" + cfg.name;

			// do some init stuff
			ps.transform.localRotation = Quaternion.identity;
			ps.transform.localPosition = fxVessel.vesselBoundCenter;

			ParticleSystem.MainModule mainModule = ps.main;
			FloatPair lifetime = (FloatPair)cfg["lifetime"];
			mainModule.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);

			ParticleSystem.ShapeModule shapeModule = ps.shape;
			shapeModule.scale = fxVessel.vesselBoundExtents * 2f;

			ParticleSystem.VelocityOverLifetimeModule velocityModule = ps.velocityOverLifetime;
			velocityModule.radialMultiplier = 1f;

			UpdateParticleRate(ps, 0f, 0f);

			// set material texture
			ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
			renderer.material = new Material(renderer.sharedMaterial);
			renderer.material.SetTexture("_AirstreamTex", fxVessel.airstreamTexture);

			// pick appropriate texture for the particle
			renderer.material.SetTexture("_MainTex", AssetLoader.Instance.loadedTextures[cfg.mainTexture]);

			// set an emission texture, if required
			if (!string.IsNullOrEmpty(cfg.emissionTexture)) 
				renderer.material.SetTexture("_EmissionMap", AssetLoader.Instance.loadedTextures[cfg.emissionTexture]);

			fxVessel.particleMaterials.Add(renderer.material);
			fxVessel.allParticles.Add(cfg.name, new FxParticleSystem()
			{
				name = cfg.name,

				system = ps,

				offset = (float)cfg["offset"],
				useHalfOffset = (bool)cfg["use_half_offset"],

				rate = (FloatPair)cfg["rate"],
				velocity = (FloatPair)cfg["velocity"]
			});
			fxVessel.particleKeys.Add(cfg.name);
		}

		void KillAllParticles()
		{
			if (fxVessel.areParticlesKilled) return;  // no need to constantly kill the particles

			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				UpdateParticleRate(fxVessel.allParticles[fxVessel.particleKeys[i]].system, 0f, 0f);
			}

			fxVessel.areParticlesKilled = true;
		}

		void UpdateParticleRate(ParticleSystem system, float min, float max)
		{
			ParticleSystem.EmissionModule emissionModule = system.emission;
			ParticleSystem.MinMaxCurve rateCurve = emissionModule.rateOverTime;

			rateCurve.constantMin = min;
			rateCurve.constantMax = max;

			emissionModule.rateOverTime = rateCurve;
		}

		void UpdateParticleVel(ParticleSystem system, Vector3 dir, Vector3 relativeVel, FloatPair velocity)
		{
			ParticleSystem.VelocityOverLifetimeModule velocityModule = system.velocityOverLifetime;

			// NOTE: the relative velocity is for vessels which have a large relative velocity to the active vessel
			//       which is used to make the particles move in the correct direction, even for not-active vessels
			velocityModule.x = new ParticleSystem.MinMaxCurve(dir.x * velocity.x + relativeVel.x, dir.x * velocity.y + relativeVel.x);
			velocityModule.y = new ParticleSystem.MinMaxCurve(dir.y * velocity.x + relativeVel.y, dir.y * velocity.y + relativeVel.y);
			velocityModule.z = new ParticleSystem.MinMaxCurve(dir.z * velocity.x + relativeVel.z, dir.z * velocity.y + relativeVel.z);
		}

		void UpdateParticleSystems()
		{
			float entryStrength = GetEntryStrength();
			BodyConfig config = GetCurrentConfig();

			// check if we should actually do the particles
			if (entryStrength < (float)config["particle_threshold"])
			{
				KillAllParticles();
				return;
			}

			fxVessel.areParticlesKilled = false;

			// world velocity
			Vector3 relativeVel = GetRelativeVelocity();  // relative to active vessel
			Vector3 worldVel = OverridePhysics ? -OverrideEntryDirection : -GetEntryVelocity();
			Vector3 direction = vessel.transform.InverseTransformDirection(worldVel);
			float lengthMultiplier = GetLengthMultiplier();
			float halfLengthMultiplier = Mathf.Max(lengthMultiplier * 0.5f, 1f);

			// update for each particle
			desiredRate = Mathf.Clamp01((entryStrength - (float)config["particle_threshold"]) / 600f);
			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				FxParticleSystem particle = fxVessel.allParticles[fxVessel.particleKeys[i]];
				ParticleSystem ps = particle.system;

				// offset
				ps.transform.localPosition = fxVessel.vesselBoundCenter + (direction * particle.offset * (particle.useHalfOffset ? halfLengthMultiplier : lengthMultiplier));

				// rate
				float min = particle.rate.x * desiredRate;
				float max = particle.rate.y * desiredRate;
				UpdateParticleRate(ps, min, max);

				// velocity
				UpdateParticleVel(ps, worldVel, relativeVel, particle.velocity);
			}
		}

		/// <summary>
		/// Unloads the vessel, removing instances and other things like that
		/// </summary>
		public void RemoveVesselFx(bool onlyEnvelopes = false)
		{
			if (!isLoaded) return;

			isLoaded = false;

			// destroy the commandbuffer
			DestroyCommandBuffer();

			fxVessel.fxEnvelope.Clear();

			if (!onlyEnvelopes)
			{
				// destroy the misc stuff
				if (fxVessel.material != null) Destroy(fxVessel.material);
				if (fxVessel.airstreamCamera != null) Destroy(fxVessel.airstreamCamera.gameObject);
				if (fxVessel.airstreamTexture != null) Destroy(fxVessel.airstreamTexture);

				// destroy the particles
				for (int i = 0; i < fxVessel.allParticles.Count; i++)
				{
					if (fxVessel.allParticles[fxVessel.particleKeys[i]].system != null) Destroy(fxVessel.allParticles[fxVessel.particleKeys[i]].system.gameObject);
				}

				lastStrength = 0f;

				fxVessel = null;
			}

			Logging.Log("Unloaded vessel " + vessel.vesselName);
		}

		/// <summary>
		/// Reloads the vessel (simulates unloading and loading again)
		/// </summary>
		public void ReloadVessel()
		{
			RemoveVesselFx(false);
			reloadDelayFrames = Math.Max(reloadDelayFrames, 1);
		}

		/// <summary>
		/// Similar to ReloadVessel(), but it's much lighter since it does not re-instantiate the camera and particles
		/// </summary>
		public void OnVesselPartCountChanged()
		{
			// Mark the vessel for reloading
			RemoveVesselFx(true);
			reloadDelayFrames = Math.Max(reloadDelayFrames, 1);
		}

		public override void OnLoadVessel()
		{
			base.OnLoadVessel();

			reloadDelayFrames = 20;
		}

		public override void OnUnloadVessel()
		{
			base.OnUnloadVessel();

			RemoveVesselFx(false);
		}

		public void OnDestroy()
		{
			RemoveVesselFx(false);
		}

		public void Update()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			// Reload if the vessel is marked for reloading
			if (reloadDelayFrames > 0 && vessel.loaded && !vessel.packed)
			{
				if (--reloadDelayFrames == 0)
				{
					CreateVesselFx();
				}
			}
		}

		public void LateUpdate()
		{
			// Certain things only need to happen if we had a fixed update
			if (Time.fixedTime != lastFixedTime && isLoaded)
			{
				lastFixedTime = Time.fixedTime;

				// update particle stuff like strength and direction
				if (fxVessel.hasParticles) UpdateParticleSystems();

				// position the camera where it can see the entire vessel
				fxVessel.airstreamCamera.transform.position = GetOrthoCameraPosition();
				fxVessel.airstreamCamera.transform.LookAt(vessel.transform.TransformPoint(fxVessel.vesselBoundCenter));

				UpdateMaterialProperties();
			}

			// Check if the ship goes outside of the atmosphere, unload the effects if so
			if (vessel.altitude > vessel.mainBody.atmosphereDepth && isLoaded && !OverridePhysics)
			{
				RemoveVesselFx(false);
			}

			// Check if the vessel is not marked for reloading and if it's entering the atmosphere
			double descentRate = vessel.altitude - vslLastAlt;
			vslLastAlt = vessel.altitude;
			if (reloadDelayFrames < 1 && descentRate < 0 && vessel.altitude <= vessel.mainBody.atmosphereDepth && !isLoaded)
			{
				CreateVesselFx();
			}
		}

		/// <summary>
		/// Debug drawings
		/// </summary>
		public void OnGUI()
		{
			if (!debugMode || !isLoaded) return;

			// vessel bounds
			Vector3[] vesselPoints = new Vector3[8];
			for (int i = 0; i < 8; i++)
			{
				vesselPoints[i] = vessel.transform.TransformPoint(fxVessel.vesselBounds[i]);
			}
			DrawingUtils.DrawBox(vesselPoints, Color.green);

			// vessel axes
			Vector3 fwd = vessel.GetFwdVector();
			Vector3 up = vessel.transform.up;
			Vector3 rt = Vector3.Cross(fwd, up);
			DrawingUtils.DrawAxes(vessel.transform.position, fwd, rt, up);

			// camera
			Transform camTransform = fxVessel.airstreamCamera.transform;
			DrawingUtils.DrawArrow(camTransform.position, camTransform.forward, camTransform.right, camTransform.up, Color.magenta);
		}

		/// <summary>
		/// Does the necessary stuff during an SOI change, like enabling/disabling the effects and changing the color configs
		/// Disables the effects on bodies without an atmosphere
		/// Enables the effects if necessary
		/// </summary>
		public void OnVesselSOIChanged(CelestialBody body)
		{
			if (!body.atmosphere)
			{
				RemoveVesselFx();
				return;
			}

			if (!isLoaded)
			{
				CreateVesselFx();
				return;
			}

			UpdateCurrentBody(body, false);
		}

		/// <summary>
		/// Updates the current body, and updates the properties
		/// </summary>
		private void UpdateCurrentBody(CelestialBody body, bool atLoad)
		{
			if (fxVessel != null)
			{
				Logging.Log($"Updating current body for {vessel.name}");

				ConfigManager.Instance.TryGetBodyConfig(body.name, true, out BodyConfig cfg);
				currentBody = cfg;
				
				if (!atLoad)
				{
					// reset the commandbuffer
					DestroyCommandBuffer();
					InitializeCommandBuffer();
					PopulateCommandBuffer();
				}
			}
		}

		/// <summary>
		/// Updates the material properties
		/// </summary>
		void UpdateMaterialProperties()
		{
			float entryStrength = GetEntryStrength();
			BodyConfig config = GetCurrentConfig();

			// calculate view-projection matrix for the airstream camera
			Matrix4x4 V = fxVessel.airstreamCamera.worldToCameraMatrix;
			Matrix4x4 P = GL.GetGPUProjectionMatrix(fxVessel.airstreamCamera.projectionMatrix, true);
			Matrix4x4 VP = P * V;

			// update the particle properties, setting the VP matrix separately
			for (int i = 0; i < fxVessel.particleMaterials.Count; i++)
			{
				fxVessel.particleMaterials[i].SetMatrix("_AirstreamVP", VP);
			}

			// update the material with dynamic properties
			fxVessel.material.SetVector("_Velocity", OverridePhysics ? OverrideEntryDirection : GetEntryVelocity());
			fxVessel.material.SetFloat("_EntryStrength", entryStrength);
			fxVessel.material.SetMatrix("_AirstreamVP", VP);

			fxVessel.material.SetInt("_Hdr", CameraManager.Instance.ActualHdrState ? 1 : 0);
			fxVessel.material.SetFloat("_FxState", OverridePhysics ? OverrideEffectState : AeroFX.state);
			fxVessel.material.SetFloat("_AngleOfAttack", OverridePhysics ? OverrideAngleOfAttack : Utils.GetAngleOfAttack(vessel));

			fxVessel.material.SetInt("_DisableBowshock", (bool)ModSettings.I["disable_bowshock"] ? 1 : 0);

			fxVessel.material.SetFloat("_LengthMultiplier", GetLengthMultiplier());
			fxVessel.material.SetFloat("_OpacityMultiplier", (float)config["opacity_multiplier"]);
			fxVessel.material.SetFloat("_GlowMultiplier", (float)config["glow_multiplier"]);
			fxVessel.material.SetFloat("_WrapOpacityMultiplier", (float)config["wrap_opacity_multiplier"]);
			fxVessel.material.SetFloat("_WrapFresnelModifier", (float)config["wrap_fresnel_modifier"]);

			fxVessel.material.SetFloat("_StreakProbability", (float)config["streak_probability"]);
			fxVessel.material.SetFloat("_StreakThreshold", (float)config["streak_threshold"]);
		}

		/// <summary>
		/// Calculates the total bounds of the entire vessel
		/// Returns if the calculation resulted in a correct bounding box
		/// </summary>
		bool CalculateVesselBounds(AtmoFxVessel fxVessel, Vessel vsl, bool doChecks)
		{
			// reset the corners
			fxVessel.vesselMaxCorner = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			fxVessel.vesselMinCorner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

			for (int i = 0; i < vsl.parts.Count; i++)
			{
				if ((!Utils.IsPartBoundCompatible(vsl.parts[i])) && doChecks) continue;

				List<Renderer> renderers = vsl.parts[i].FindModelRenderersCached();
				for (int r = 0; r < renderers.Count; r++)
				{
					if (!renderers[r].gameObject.activeInHierarchy) continue;

					// try getting the mesh filter
					bool hasFilter = renderers[r].TryGetComponent(out MeshFilter meshFilter);

					// is skinned
					bool isSkinnedRenderer = renderers[r].TryGetComponent(out SkinnedMeshRenderer skinnedModel);

					if (!isSkinnedRenderer)  // if it's a normal model, check if it has a filter and a mesh
					{
						if (!hasFilter) continue;
						if (meshFilter.mesh == null) continue;
					}

					// check if the mesh is legal
					if (Utils.CheckLayerModel(renderers[r].transform)) continue;

					// get the corners of the mesh
					Bounds modelBounds = isSkinnedRenderer ? skinnedModel.localBounds : meshFilter.mesh.bounds;
					Vector3[] corners = Utils.GetBoundCorners(modelBounds);

					// create the transformation matrix
					// part -> world -> vessel
					Matrix4x4 matrix = vsl.transform.worldToLocalMatrix * renderers[r].transform.localToWorldMatrix;

					Vector3[] vesselCorners = new Vector3[8];

					// iterate through each corner and multiply by the matrix
					for (int c = 0; c < 8; c++)
					{
						Vector3 v = matrix.MultiplyPoint3x4(corners[c]);

						vesselCorners[c] = v;

						// update the vessel bounds
						fxVessel.vesselMinCorner = Vector3.Min(fxVessel.vesselMinCorner, v);
						fxVessel.vesselMaxCorner = Vector3.Max(fxVessel.vesselMaxCorner, v);
					}
				}
			}

			if (fxVessel.vesselMaxCorner.x == float.MinValue) return false;  // return false if the corner hasn't changed

			Vector3 vesselSize = new Vector3(
				Mathf.Abs(fxVessel.vesselMaxCorner.x - fxVessel.vesselMinCorner.x),
				Mathf.Abs(fxVessel.vesselMaxCorner.y - fxVessel.vesselMinCorner.y),
				Mathf.Abs(fxVessel.vesselMaxCorner.z - fxVessel.vesselMinCorner.z)
			);

			Bounds bounds = new Bounds(fxVessel.vesselMinCorner + vesselSize / 2f, vesselSize);

			fxVessel.vesselBounds = Utils.GetBoundCorners(bounds);
			fxVessel.vesselMaxSize = Mathf.Max(vesselSize.x, vesselSize.y, vesselSize.z);
			fxVessel.vesselBoundCenter = bounds.center;
			fxVessel.vesselBoundExtents = vesselSize / 2f;
			fxVessel.vesselBoundRadius = fxVessel.vesselBoundExtents.magnitude;

			CalculateBaseLengthMultiplier();  // done after calculating bounds

			return true;
		}

		/// <summary>
		/// Returns the correct bodyconfig to use, depending on whether the override is on
		/// </summary>
		/// <returns></returns>
		BodyConfig GetCurrentConfig()
		{
			if (OverridePhysics)
			{
				return _overrideBodyConfig;
			} else
			{
				return currentBody;
			}
		}

		/// <summary>
		/// Returns the velocity direction
		/// </summary>
		Vector3 GetEntryVelocity()
		{
			return vessel.srf_velocity.normalized;
		}

		/// <summary>
		/// Returns the relative velocity of the vessel, relative to the active vessel (target vel)
		/// </summary>
		/// <returns></returns>
		public Vector3 GetRelativeVelocity()
		{
			if (vessel.isActiveVessel)
			{
				// if the vessel is the active vessel, then return zero
				return Vector3.zero;
			}

			Vector3 activeVslVelocity = FlightGlobals.ActiveVessel.srf_velocity;
			Vector3 thisVelocity = vessel.srf_velocity;
			
			return thisVelocity - activeVslVelocity;
		}

		/// <summary>
		/// Returns the strength of the effects
		/// </summary>
		public float GetEntryStrength()
		{
			BodyConfig config = GetCurrentConfig();

			// Pretty much just the FxScalar, but scaled with the strength base value, with an added modifier for the mach effects, and offset by the planet pack cfg
			float transitionOffset = config.planetPack.transitionOffset * AeroFX.state;
			float fxScalar = AeroFX.FxScalar + transitionOffset;
			fxScalar *= Mathf.Lerp(0.13f, 1f, AeroFX.state);

			// add additional value (only for reentry state), for faster transition
			fxScalar += Mathf.Min((float)vessel.dynamicPressurekPa * 10f, 0.2f) * AeroFX.state;

			// make sure to clamp the value to 1
			fxScalar = Mathf.Min(fxScalar, 1f);

			// scale with base
			float strength = fxScalar * (float)ModSettings.I["strength_base"];

			// Smoothly interpolate the last frame's and this frame's results
			// automatically adjusts the t value based on how much the results differ
			float delta = Mathf.Abs(strength - lastStrength) / (float)ModSettings.I["strength_base"];
			strength = Mathf.Lerp(lastStrength, strength, TimeWarp.deltaTime * (1f + delta * 2f));

			lastStrength = strength;

			if (OverridePhysics)
			{
				return OverrideEffectStrength * (float)config["strength_multiplier"];
			} else
			{
				return strength * (float)config["strength_multiplier"];
			}
		}

		/// <summary>
		/// Calculates the base length multiplier, with the vessel's radius
		/// </summary>
		void CalculateBaseLengthMultiplier()
		{
			// the Apollo capsule has a radius of around 2, which makes it a good reference
			float baseRadius = fxVessel.vesselBoundRadius / 2f;

			// gets the final result
			// for example, if the base radius is 2 then the result will be 1.4
			// or if the base radius is 3 then the result will be 1.8
			fxVessel.baseLengthMultiplier = 1f + (baseRadius - 1f) * 0.3f;
		}

		/// <summary>
		/// Calculates the length multiplier based on the base multiplier and current body config
		/// </summary>
		float GetLengthMultiplier()
		{
			return fxVessel.baseLengthMultiplier * (float)GetCurrentConfig()["length_multiplier"] * (float)ModSettings.I["length_mult"];
		}

		/// <summary>
		/// Returns the camera position adjusted for an orhtographic projection
		/// </summary>
		Vector3 GetOrthoCameraPosition()
		{
			float maxExtent = fxVessel.vesselBoundRadius;
			float distance = maxExtent * 1.1f;

			Vector3 dir = OverridePhysics ? OverrideEntryDirection : GetEntryVelocity();
			Vector3 localPos = fxVessel.vesselBoundCenter + distance * vessel.transform.InverseTransformDirection(dir);

			return vessel.transform.TransformPoint(localPos);
		}
	}
}
