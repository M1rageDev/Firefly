using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
	/// Stores the data and instances of the effects
	/// </summary>
	public class AtmoFxVessel
	{
		public List<FxEnvelopeModel> fxEnvelope = new List<FxEnvelopeModel>();

		public CommandBuffer commandBuffer;

		public bool hasParticles = false;

		public List<Material> particleMaterials = new List<Material>();
		public List<ParticleSystem> allParticles = new List<ParticleSystem>();
		public List<FloatPair> orgParticleRates = new List<FloatPair>();
		public ParticleSystem sparkParticles;
		public ParticleSystem chunkParticles;
		public ParticleSystem alternateChunkParticles;
		public ParticleSystem smokeParticles;
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
	public class AtmoFxModule : VesselModule
	{
		public AtmoFxVessel fxVessel;
		public bool isLoaded = false;

		public bool debugMode = false;

		float lastFixedTime;
		float desiredRate;
		float lastSpeed;

		double vslLastAlt;

		public BodyConfig currentBody;

		public bool doEffectEditor = false;

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

		/// <summary>
		/// Loads a vessel, instantiates stuff like the camera and rendertexture, also creates the entry velopes and particle system
		/// </summary>
		public void CreateVesselFx()
		{
			if (!WindowManager.Instance.tgl_EffectToggle) return;

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
			if (!(bool)ModSettings.I["disable_particles"]) CreateParticleSystems();  // run the function only if they're enabled in settings

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
				BodyColors colors = new BodyColors(currentBody.colors);  // create the original colors
				if (ConfigManager.Instance.partConfigs.ContainsKey(envelope.partName))
				{
					Logging.Log("Envelope has a part override config");
					BodyColors overrideColor = ConfigManager.Instance.partConfigs[envelope.partName];

					// override the colors with the override
					foreach (string key in overrideColor.fields.Keys)
					{
						if (overrideColor[key] != null) colors[key] = overrideColor[key];
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
				fxVessel.commandBuffer.SetGlobalColor("_GlowColor", colors["glow"]);
				fxVessel.commandBuffer.SetGlobalColor("_HotGlowColor", colors["glow_hot"]);

				fxVessel.commandBuffer.SetGlobalColor("_PrimaryColor", colors["trail_primary"]);
				fxVessel.commandBuffer.SetGlobalColor("_SecondaryColor", colors["trail_secondary"]);
				fxVessel.commandBuffer.SetGlobalColor("_TertiaryColor", colors["trail_tertiary"]);
				fxVessel.commandBuffer.SetGlobalColor("_StreakColor", colors["trail_streak"]);

				fxVessel.commandBuffer.SetGlobalColor("_LayerColor", colors["wrap_layer"]);
				fxVessel.commandBuffer.SetGlobalColor("_LayerStreakColor", colors["wrap_streak"]);

				fxVessel.commandBuffer.SetGlobalColor("_ShockwaveColor", colors["shockwave"]);

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

		void CreateParticleSystems()
		{
			Logging.Log("Creating particle systems");

			fxVessel.hasParticles = true;

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
			fxVessel.orgParticleRates.Clear();

			// spawn particle systems
			fxVessel.sparkParticles = CreateParticleSystem(AssetLoader.Instance.sparkParticles, "Sparks", "ChunkSprite", "ChunkSprite");
			fxVessel.chunkParticles = CreateParticleSystem(AssetLoader.Instance.chunkParticles, "Chunks", "ChunkSprite", "");
			fxVessel.alternateChunkParticles = CreateParticleSystem(AssetLoader.Instance.alternateChunkParticles, "ChunksAlternate", "ChunkSprite1", "");
			fxVessel.smokeParticles = CreateParticleSystem(AssetLoader.Instance.smokeParticles, "Smoke", "SmokeSprite", "");

			// disable if needed
			if ((bool)ModSettings.I["disable_sparks"]) fxVessel.sparkParticles.gameObject.SetActive(false);
			if ((bool)ModSettings.I["disable_debris"])
			{
				fxVessel.chunkParticles.gameObject.SetActive(false);
				fxVessel.alternateChunkParticles.gameObject.SetActive(false);
			}
			if ((bool)ModSettings.I["disable_smoke"]) fxVessel.smokeParticles.gameObject.SetActive(false);

			// update the particle system properties for every one of them
			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				ParticleSystem ps = fxVessel.allParticles[i];

				ParticleSystem.ShapeModule shapeModule = ps.shape;
				shapeModule.scale = fxVessel.vesselBoundExtents * 2f;

				ParticleSystem.VelocityOverLifetimeModule velocityModule = ps.velocityOverLifetime;
				velocityModule.radialMultiplier = 1f;

				UpdateParticleRate(ps, 0f, 0f);
			}
		}

		ParticleSystem CreateParticleSystem(GameObject prefab, string name, string texture, string emissionTexture)
		{
			// instantiate prefab
			ParticleSystem ps = Instantiate(prefab, vessel.transform).GetComponent<ParticleSystem>();
			fxVessel.allParticles.Add(ps);

			// change transform name
			ps.gameObject.name = vessel.name + "_FireflyPS_" + name;

			// store original emission rate
			ParticleSystem.MinMaxCurve curve = ps.emission.rateOverTime;
			fxVessel.orgParticleRates.Add(new FloatPair(curve.constantMin, curve.constantMax));

			// initialize transform pos and rot
			ps.transform.localRotation = Quaternion.identity;
			ps.transform.localPosition = fxVessel.vesselBoundCenter;

			// set material texture
			ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
			renderer.material = new Material(renderer.sharedMaterial);
			renderer.material.SetTexture("_AirstreamTex", fxVessel.airstreamTexture);

			// pick appropriate texture for the particle
			renderer.material.SetTexture("_MainTex", AssetLoader.Instance.loadedTextures[texture]);

			// set an emission texture, if required
			if (!string.IsNullOrEmpty(emissionTexture)) renderer.material.SetTexture("_EmissionMap", AssetLoader.Instance.loadedTextures[emissionTexture]);

			fxVessel.particleMaterials.Add(renderer.material);

			return ps;
		}

		/// <summary>
		/// Kills all particle systems
		/// </summary>
		void KillAllParticles()
		{
			if (fxVessel.areParticlesKilled) return;  // no need to constantly kill the particles

			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				UpdateParticleRate(fxVessel.allParticles[i], 0f, 0f);
			}

			fxVessel.areParticlesKilled = true;
		}

		/// <summary>
		/// Updates the rate for a given particle system
		/// </summary>
		void UpdateParticleRate(ParticleSystem system, float min, float max)
		{
			ParticleSystem.EmissionModule emissionModule = system.emission;
			ParticleSystem.MinMaxCurve rateCurve = emissionModule.rateOverTime;

			rateCurve.constantMin = min;
			rateCurve.constantMax = max;

			emissionModule.rateOverTime = rateCurve;
		}

		/// <summary>
		/// Updates the velocity vector for a given particle system
		/// </summary>
		void UpdateParticleVel(ParticleSystem system, Vector3 dir)
		{
			system.transform.forward = dir;
		}

		/// <summary>
		/// Updates the particle systems, should be called everytime the velocity changes
		/// </summary>
		void UpdateParticleSystems()
		{
			float entrySpeed = GetAdjustedEntrySpeed();

			// check if we should actually do the particles
			if (entrySpeed < currentBody.particleThreshold)
			{
				KillAllParticles();
				return;
			}

			fxVessel.areParticlesKilled = false;

			// rate
			desiredRate = Mathf.Clamp01((entrySpeed - currentBody.particleThreshold) / 600f);
			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				ParticleSystem ps = fxVessel.allParticles[i];

				float min = fxVessel.orgParticleRates[i].x * desiredRate;
				float max = fxVessel.orgParticleRates[i].y * desiredRate;

				UpdateParticleRate(ps, min, max);
			}

			// world velocity
			Vector3 direction = doEffectEditor ? EffectEditor.Instance.effectDirection : vessel.transform.InverseTransformDirection(GetEntryVelocity());
			Vector3 worldVel = doEffectEditor ? -EffectEditor.Instance.GetWorldDirection() : -GetEntryVelocity();

			float lengthMultiplier = GetLengthMultiplier();

			fxVessel.sparkParticles.transform.localPosition = fxVessel.vesselBoundCenter + direction * -0.5f * lengthMultiplier;

			fxVessel.chunkParticles.transform.localPosition = fxVessel.vesselBoundCenter + direction * -1.24f * lengthMultiplier;

			fxVessel.alternateChunkParticles.transform.localPosition = fxVessel.vesselBoundCenter + direction * -1.62f * lengthMultiplier;

			fxVessel.smokeParticles.transform.localPosition = fxVessel.vesselBoundCenter + direction * -2f * Mathf.Max(lengthMultiplier * 0.5f, 1f);

			// directions
			UpdateParticleVel(fxVessel.sparkParticles, worldVel);
			UpdateParticleVel(fxVessel.chunkParticles, worldVel);
			UpdateParticleVel(fxVessel.alternateChunkParticles, worldVel);
			UpdateParticleVel(fxVessel.smokeParticles, worldVel);
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
				if (fxVessel.sparkParticles != null) Destroy(fxVessel.sparkParticles.gameObject);
				if (fxVessel.chunkParticles != null) Destroy(fxVessel.chunkParticles.gameObject);
				if (fxVessel.alternateChunkParticles != null) Destroy(fxVessel.alternateChunkParticles.gameObject);
				if (fxVessel.smokeParticles != null) Destroy(fxVessel.smokeParticles.gameObject);

				lastSpeed = 0f;

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

				EffectEditor editor = EffectEditor.Instance;

				float entrySpeed = GetAdjustedEntrySpeed();

				// update particle stuff like strength and direction
				if (fxVessel.hasParticles) UpdateParticleSystems();

				// position the camera where it can see the entire vessel
				fxVessel.airstreamCamera.transform.position = GetOrthoCameraPosition();
				fxVessel.airstreamCamera.transform.LookAt(vessel.transform.TransformPoint(fxVessel.vesselBoundCenter));

				// view projection matrix for the airstream camera
				Matrix4x4 V = fxVessel.airstreamCamera.worldToCameraMatrix;
				Matrix4x4 P = GL.GetGPUProjectionMatrix(fxVessel.airstreamCamera.projectionMatrix, true);
				Matrix4x4 VP = P * V;

				// update the material with dynamic properties
				fxVessel.material.SetVector("_Velocity", doEffectEditor ? editor.GetWorldDirection() : GetEntryVelocity());
				fxVessel.material.SetFloat("_EntrySpeed", entrySpeed);
				fxVessel.material.SetMatrix("_AirstreamVP", VP);

				// particle properties, setting the VP matrix separately
				for (int i = 0; i < fxVessel.particleMaterials.Count; i++)
				{
					fxVessel.particleMaterials[i].SetMatrix("_AirstreamVP", VP);
				}

				UpdateMaterialProperties();
			}

			// Check if the ship goes outside of the atmosphere (and the speed is low enough), unload the effects if so
			if (vessel.altitude > vessel.mainBody.atmosphereDepth && isLoaded && !doEffectEditor)
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

				if (!doEffectEditor)
				{
					ConfigManager.Instance.TryGetBodyConfig(body.name, true, out BodyConfig cfg);
					currentBody = cfg;
				} else
				{
					currentBody = EffectEditor.Instance.config;
				}
				
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
			fxVessel.material.SetInt("_Hdr", CameraManager.Instance.ActualHdrState ? 1 : 0);
			fxVessel.material.SetFloat("_FxState", doEffectEditor ? EffectEditor.Instance.effectState : AeroFX.state);
			fxVessel.material.SetFloat("_AngleOfAttack", doEffectEditor ? 0f : Utils.GetAngleOfAttack(vessel));
			fxVessel.material.SetFloat("_ShadowPower", 0f);
			fxVessel.material.SetFloat("_VelDotPower", 0f);
			fxVessel.material.SetFloat("_EntrySpeedMultiplier", 1f);

			fxVessel.material.SetInt("_DisableBowshock", (bool)ModSettings.I["disable_bowshock"] ? 1 : 0);

			fxVessel.material.SetFloat("_LengthMultiplier", GetLengthMultiplier());
			fxVessel.material.SetFloat("_OpacityMultiplier", currentBody.opacityMultiplier);
			fxVessel.material.SetFloat("_WrapFresnelModifier", currentBody.wrapFresnelModifier);

			fxVessel.material.SetFloat("_StreakProbability", currentBody.streakProbability);
			fxVessel.material.SetFloat("_StreakThreshold", currentBody.streakThreshold);
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
		/// Returns the velocity direction
		/// </summary>
		Vector3 GetEntryVelocity()
		{
			return vessel.srf_velocity.normalized;
		}

		/// <summary>
		/// Returns the speed of the airflow, based on the stock stuff
		/// </summary>
		float GetEntrySpeed()
		{
			// Pretty much just the FxScalar, but scaled with the strength base value, with an added modifier for the mach effects, and offset by the planet pack cfg
			float transitionOffset = currentBody.planetPack.transitionOffset * AeroFX.state;
			float fxScalar = AeroFX.FxScalar + transitionOffset;
			fxScalar *= Mathf.Lerp(0.13f, 1f, AeroFX.state);

			// add additional value (only for reentry state), for faster transition
			fxScalar += Mathf.Min((float)vessel.dynamicPressurekPa * 10f, 0.2f) * AeroFX.state;

			// make sure to clamp the value to 1
			fxScalar = Mathf.Min(fxScalar, 1f);

			// scale with base
			float spd = fxScalar * (float)ModSettings.I["strength_base"];

			// Smoothly interpolate the last frame's and this frame's results
			// automatically adjusts the t value based on how much the results differ
			float delta = Mathf.Abs(spd - lastSpeed) / (float)ModSettings.I["strength_base"];
			spd = Mathf.Lerp(lastSpeed, spd, TimeWarp.deltaTime * (1f + delta * 2f));

			lastSpeed = spd;

			return spd;
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
			return fxVessel.baseLengthMultiplier * currentBody.lengthMultiplier * (float)ModSettings.I["length_mult"];
		}

		/// <summary>
		/// Returns the entry speed adjusted to the atmosphere parameters, and takes the effect editor into account
		/// </summary>
		public float GetAdjustedEntrySpeed()
		{
			return doEffectEditor ? (EffectEditor.Instance.effectSpeed * currentBody.strengthMultiplier) : (GetEntrySpeed() * currentBody.strengthMultiplier);
		}

		/// <summary>
		/// Returns the camera position adjusted for an orhtographic projection
		/// </summary>
		Vector3 GetOrthoCameraPosition()
		{
			float maxExtent = fxVessel.vesselBoundRadius;
			float distance = maxExtent * 1.1f;

			Vector3 localDir = doEffectEditor ? EffectEditor.Instance.effectDirection : vessel.transform.InverseTransformDirection(GetEntryVelocity());
			Vector3 localPos = fxVessel.vesselBoundCenter + distance * localDir;

			return vessel.transform.TransformPoint(localPos);
		}
	}
}
