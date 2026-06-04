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
		public List<FxParticleSystem> particles = new List<FxParticleSystem>();
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

		// cached config values - updated on body change and load
		float cachedParticleThreshold;
		float cachedOpacityMultiplier;
		float cachedGlowMultiplier;
		float cachedWrapOpacityMultiplier;
		float cachedWrapFresnelModifier;
		float cachedStreakProbability;
		float cachedStreakThreshold;
		float cachedStrengthBase;
		float cachedStrengthMultiplier;
		float cachedLengthMultiplier;
		bool cachedDisableBowshock;

		// shader property IDs - resolved once at class load, never hashed again
		static readonly int ID_Velocity             = Shader.PropertyToID("_Velocity");
		static readonly int ID_EntryStrength        = Shader.PropertyToID("_EntryStrength");
		static readonly int ID_AirstreamVP          = Shader.PropertyToID("_AirstreamVP");
		static readonly int ID_AirstreamTex         = Shader.PropertyToID("_AirstreamTex");
		static readonly int ID_Hdr                  = Shader.PropertyToID("_Hdr");
		static readonly int ID_FxState              = Shader.PropertyToID("_FxState");
		static readonly int ID_AngleOfAttack        = Shader.PropertyToID("_AngleOfAttack");
		static readonly int ID_DisableBowshock      = Shader.PropertyToID("_DisableBowshock");
		static readonly int ID_LengthMultiplier     = Shader.PropertyToID("_LengthMultiplier");
		static readonly int ID_OpacityMultiplier    = Shader.PropertyToID("_OpacityMultiplier");
		static readonly int ID_GlowMultiplier       = Shader.PropertyToID("_GlowMultiplier");
		static readonly int ID_WrapOpacityMultiplier = Shader.PropertyToID("_WrapOpacityMultiplier");
		static readonly int ID_WrapFresnelModifier  = Shader.PropertyToID("_WrapFresnelModifier");
		static readonly int ID_StreakProbability    = Shader.PropertyToID("_StreakProbability");
		static readonly int ID_StreakThreshold      = Shader.PropertyToID("_StreakThreshold");
		static readonly int ID_ModelScale           = Shader.PropertyToID("_ModelScale");
		static readonly int ID_EnvelopeScaleFactor  = Shader.PropertyToID("_EnvelopeScaleFactor");
		static readonly int ID_RandomnessFactor     = Shader.PropertyToID("_RandomnessFactor");
		static readonly int ID_GlowColor            = Shader.PropertyToID("_GlowColor");
		static readonly int ID_HotGlowColor         = Shader.PropertyToID("_HotGlowColor");
		static readonly int ID_PrimaryColor         = Shader.PropertyToID("_PrimaryColor");
		static readonly int ID_SecondaryColor       = Shader.PropertyToID("_SecondaryColor");
		static readonly int ID_TertiaryColor        = Shader.PropertyToID("_TertiaryColor");
		static readonly int ID_StreakColor          = Shader.PropertyToID("_StreakColor");
		static readonly int ID_LayerColor           = Shader.PropertyToID("_LayerColor");
		static readonly int ID_LayerStreakColor     = Shader.PropertyToID("_LayerStreakColor");
		static readonly int ID_ShockwaveColor       = Shader.PropertyToID("_ShockwaveColor");
		static readonly int ID_MainTex              = Shader.PropertyToID("_MainTex");
		static readonly int ID_EmissionMap          = Shader.PropertyToID("_EmissionMap");

		private const float InvParticleRateScale = 1f / 600f;

		// override stuff, for use with the API/editors
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
		BodyConfig _overrideBodyConfig;

		// finds the stock handler of the aero FX
		AerodynamicsFX _aeroFX;
		public AerodynamicsFX AeroFX
		{
			get
			{
				if (_aeroFX == null)
				{
					GameObject fxLogicObject = GameObject.Find("FXLogic");
					if (fxLogicObject != null)
						_aeroFX = fxLogicObject.GetComponent<AerodynamicsFX>();
				}
				return _aeroFX;
			}
		}

		int reloadDelayFrames = 0;

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
		/// Caches typed values from config and settings, eliminating per-frame boxed dict lookups
		/// Call on load and on body change
		/// </summary>
		void CacheConfigValues()
		{
			BodyConfig config = GetCurrentConfig();
			cachedParticleThreshold   = (float)config["particle_threshold"];
			cachedOpacityMultiplier   = (float)config["opacity_multiplier"];
			cachedGlowMultiplier      = (float)config["glow_multiplier"];
			cachedWrapOpacityMultiplier = (float)config["wrap_opacity_multiplier"];
			cachedWrapFresnelModifier = (float)config["wrap_fresnel_modifier"];
			cachedStreakProbability   = (float)config["streak_probability"];
			cachedStreakThreshold     = (float)config["streak_threshold"];
			cachedStrengthMultiplier  = (float)config["strength_multiplier"];
			cachedLengthMultiplier    = (float)config["length_multiplier"];
			cachedStrengthBase        = (float)ModSettings.I["strength_base"];
			cachedDisableBowshock     = (bool)ModSettings.I["disable_bowshock"];
		}

		/// <summary>
		/// Loads a vessel, instantiates stuff like the camera and rendertexture, also creates the entry envelopes and particle systems
		/// </summary>
		public void CreateVesselFx()
		{
			if (!GUI.WindowManager.Instance.fireflyWindow.tgl_EffectToggle) return;

			if (vessel == null || (!vessel.loaded) || vessel.parts.Count < 1)
			{
				Logging.Log("Invalid vessel");
				Logging.Log($"loaded: {vessel.loaded}");
				Logging.Log($"partcount: {vessel.parts.Count}");
				Logging.Log($"atmo: {vessel.mainBody.atmosphere}");
				return;
			}

			if (isLoaded) return;

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

				material = Instantiate(AssetLoader.Instance.globalMaterial);
				fxVessel.material = material;

				GameObject cameraGO = new GameObject("AtmoFxCamera - " + vessel.name);
				fxVessel.airstreamCamera = cameraGO.AddComponent<Camera>();

				fxVessel.airstreamCamera.orthographic = true;
				fxVessel.airstreamCamera.clearFlags = CameraClearFlags.SolidColor;
				fxVessel.airstreamCamera.cullingMask = (1 << 0);

				fxVessel.airstreamTexture = new RenderTexture(512, 512, 1, RenderTextureFormat.Depth);
				fxVessel.airstreamTexture.Create();
				fxVessel.airstreamCamera.targetTexture = fxVessel.airstreamTexture;
			}

			if (fxVessel == null || material == null)
			{
				Logging.Log("fxVessel/material is null");
				RemoveVesselFx(false);
				return;
			}

			bool correctBounds = CalculateVesselBounds(fxVessel, vessel, true);
			if (!correctBounds)
			{
				Logging.Log("Recalculating invalid vessel bounds");
				CalculateVesselBounds(fxVessel, vessel, false);
			}
			fxVessel.airstreamCamera.orthographicSize = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude, 0.3f, 2000f);
			fxVessel.airstreamCamera.farClipPlane = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude * 2f, 1f, 1000f);

			UpdateCurrentBody(vessel.mainBody, true);

			InitializeCommandBuffer();

			ResetPartModelCache();

			UpdateFxEnvelopes();
			fxVessel.material.SetTexture(ID_AirstreamTex, fxVessel.airstreamTexture);

			PopulateCommandBuffer();

			if (!(bool)ModSettings.I["disable_particles"]) CreateParticleSystems(onModify);

			CacheConfigValues();

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

			Logging.Log("Cleared commandbuffer");
			Logging.Log($"Envelope model count: {fxVessel.fxEnvelope.Count}. Can start populating the commandbuffer.");

			BodyColors baseColors = new BodyColors(GetCurrentConfig().colors);  // hoisted - same config every iteration

			for (int i = 0; i < fxVessel.fxEnvelope.Count; i++)
			{
				FxEnvelopeModel envelope = fxVessel.fxEnvelope[i];

				fxVessel.commandBuffer.SetGlobalVector(ID_ModelScale, envelope.modelScale);
				fxVessel.commandBuffer.SetGlobalVector(ID_EnvelopeScaleFactor, envelope.envelopeScaleFactor);

				BodyColors colors = new BodyColors(baseColors);  // copy from cached base
				if (ConfigManager.Instance.partConfigs.ContainsKey(envelope.partName))
				{
					Logging.Log("Envelope has a part override config");
					BodyColors overrideColor = ConfigManager.Instance.partConfigs[envelope.partName];

					foreach (string key in overrideColor.fields.Keys)
					{
						if (overrideColor[key] != null) colors[key] = overrideColor[key];
					}
				}

				float randomnessFactor = 0f;
				if (envelope.partName == "PotatoRoid" || envelope.partName == "PotatoComet")
				{
					Logging.Log("Potatoroid - setting the randomness factor to 1");
					randomnessFactor = 1f;
				}
				fxVessel.commandBuffer.SetGlobalVector(ID_RandomnessFactor, Vector2.one * randomnessFactor);

				fxVessel.commandBuffer.SetGlobalColor(ID_GlowColor,       colors["glow"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_HotGlowColor,    colors["glow_hot"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_PrimaryColor,    colors["trail_primary"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_SecondaryColor,  colors["trail_secondary"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_TertiaryColor,   colors["trail_tertiary"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_StreakColor,     colors["trail_streak"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_LayerColor,      colors["wrap_layer"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_LayerStreakColor, colors["wrap_streak"]);
				fxVessel.commandBuffer.SetGlobalColor(ID_ShockwaveColor,  colors["shockwave"]);

				fxVessel.commandBuffer.DrawRenderer(envelope.renderer, fxVessel.material);
			}

			Logging.Log("Commandbuffer populated.");
		}

		public void DestroyCommandBuffer()
		{
			if (fxVessel.commandBuffer == null) return;

			CameraManager.Instance.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, fxVessel.commandBuffer);
			fxVessel.commandBuffer.Dispose();

			fxVessel.commandBuffer = null;
		}

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

		void ResetPartModelCache()
		{
			for (int i = 0; i < vessel.parts.Count; i++)
			{
				vessel.parts[i].ResetModelRenderersCache();
			}
		}

		/// <summary>
		/// Loops over every part and destroys the MeshRenderer of custom defined envelopes.
		/// MeshFilters are left intact since they store the mesh used for effect creation.
		/// Called on vessel module load, not effect init, to ensure envelopes are gone even when effects are unloaded.
		/// </summary>
		void CleanupEnvelopeRenderers()
		{
			for (int i = 0; i < vessel.parts.Count; i++)
			{
				Part part = vessel.parts[i];

				Transform[] fxEnvelopes = part.FindModelTransforms("atmofx_envelope");
				if (fxEnvelopes.Length < 1) fxEnvelopes = Utils.FindTaggedTransforms(part);
				if (fxEnvelopes.Length < 1) return;

				for (int j = 0; j < fxEnvelopes.Length; j++)
				{
					if (!fxEnvelopes[j].gameObject.activeInHierarchy) continue;
					if (!fxEnvelopes[j].TryGetComponent(out MeshFilter _)) continue;
					if (!fxEnvelopes[j].TryGetComponent(out MeshRenderer parentRenderer)) continue;

					parentRenderer.enabled = false;
				}
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
					if (!fxEnvelopes[j].gameObject.activeInHierarchy) continue;

					if (!fxEnvelopes[j].TryGetComponent(out MeshFilter _)) continue;
					if (!fxEnvelopes[j].TryGetComponent(out MeshRenderer parentRenderer)) continue;

					FxEnvelopeModel envelope = new FxEnvelopeModel(
						Utils.GetPartCfgName(part.partInfo.name),
						parentRenderer,
						Vector3.one,
						Vector3.one
						);
					fxVessel.fxEnvelope.Add(envelope);
				}

				return;
			}

			List<Renderer> models = part.FindModelRenderersCached();
			for (int j = 0; j < models.Count; j++)
			{
				Renderer model = models[j];

				if (!model.gameObject.activeInHierarchy) continue;

				if (Utils.CheckWheelFlareModel(part, model.gameObject.name)) continue;

				if (Utils.CheckLayerModel(model.transform)) continue;

				bool isSkinnedRenderer = model.TryGetComponent(out SkinnedMeshRenderer _);

				if (!isSkinnedRenderer)
				{
					bool hasMeshFilter = model.TryGetComponent(out MeshFilter filter);
					if (!hasMeshFilter) continue;

					Mesh mesh = filter.sharedMesh;
					if (mesh == null) continue;
				}

				if (!Utils.IsPartBoundCompatible(part)) continue;

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

			if (!onModify)
			{
				for (int i = 0; i < vessel.transform.childCount; i++)
				{
					Transform t = vessel.transform.GetChild(i);

					// avoid conflict with ShVAK's VaporCones mod - check name before destroying
					if (!t.name.Contains("FireflyPS")) continue;

					if (t.TryGetComponent(out ParticleSystem _)) Destroy(t.gameObject);
				}

				fxVessel.particleMaterials.Clear();
				fxVessel.particles.Clear();

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

			ParticleSystem ps = Instantiate(AssetLoader.Instance.loadedPrefabs[cfg.prefab], vessel.transform).GetComponent<ParticleSystem>();

			ps.gameObject.name = "_FireflyPS_" + cfg.name;

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

			ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
			renderer.material = new Material(renderer.sharedMaterial);
			renderer.material.SetTexture(ID_AirstreamTex, fxVessel.airstreamTexture);
			renderer.material.SetTexture(ID_MainTex, AssetLoader.Instance.loadedTextures[cfg.mainTexture]);

			if (!string.IsNullOrEmpty(cfg.emissionTexture))
				renderer.material.SetTexture(ID_EmissionMap, AssetLoader.Instance.loadedTextures[cfg.emissionTexture]);

			fxVessel.particleMaterials.Add(renderer.material);
			fxVessel.particles.Add(new FxParticleSystem()
			{
				name        = cfg.name,
				system      = ps,
				offset      = (float)cfg["offset"],
				useHalfOffset = (bool)cfg["use_half_offset"],
				rate        = (FloatPair)cfg["rate"],
				velocity    = (FloatPair)cfg["velocity"]
			});
		}

		void KillAllParticles()
		{
			if (fxVessel.areParticlesKilled) return;

			for (int i = 0; i < fxVessel.particles.Count; i++)
			{
				UpdateParticleRate(fxVessel.particles[i].system, 0f, 0f);
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

			// relativeVel corrects particle direction for non-active vessels with large velocity deltas
			velocityModule.x = new ParticleSystem.MinMaxCurve(dir.x * velocity.x + relativeVel.x, dir.x * velocity.y + relativeVel.x);
			velocityModule.y = new ParticleSystem.MinMaxCurve(dir.y * velocity.x + relativeVel.y, dir.y * velocity.y + relativeVel.y);
			velocityModule.z = new ParticleSystem.MinMaxCurve(dir.z * velocity.x + relativeVel.z, dir.z * velocity.y + relativeVel.z);
		}

		/// <summary>
		/// entryStrength is computed once per fixed frame in LateUpdate and passed in
		/// </summary>
		void UpdateParticleSystems(float entryStrength)
		{
			if (entryStrength < cachedParticleThreshold)
			{
				KillAllParticles();
				return;
			}

			fxVessel.areParticlesKilled = false;

			Vector3 relativeVel = GetRelativeVelocity();
			Vector3 worldVel = OverridePhysics ? -OverrideEntryDirection : -GetEntryVelocity();
			Vector3 direction = vessel.transform.InverseTransformDirection(worldVel);
			float lengthMultiplier = GetLengthMultiplier();
			float halfLengthMultiplier = Mathf.Max(lengthMultiplier * 0.5f, 1f);

			// multiply by reciprocal - div is ~3-8x more cycles on FPU
			desiredRate = Mathf.Clamp01((entryStrength - cachedParticleThreshold) * InvParticleRateScale);

			for (int i = 0; i < fxVessel.particles.Count; i++)
			{
				FxParticleSystem particle = fxVessel.particles[i];
				ParticleSystem ps = particle.system;

				ps.transform.localPosition = fxVessel.vesselBoundCenter + (direction * particle.offset * (particle.useHalfOffset ? halfLengthMultiplier : lengthMultiplier));

				UpdateParticleRate(ps, particle.rate.x * desiredRate, particle.rate.y * desiredRate);

				UpdateParticleVel(ps, worldVel, relativeVel, particle.velocity);
			}
		}

		/// <summary>
		/// Unloads the vessel, removing instances and other things
		/// </summary>
		public void RemoveVesselFx(bool onlyEnvelopes = false, bool force = false)
		{
			if (!isLoaded && !force) return;
			if (fxVessel == null) return;

			isLoaded = false;

			DestroyCommandBuffer();

			fxVessel.fxEnvelope.Clear();

			if (!onlyEnvelopes)
			{
				if (fxVessel.material != null) Destroy(fxVessel.material);
				if (fxVessel.airstreamCamera != null) Destroy(fxVessel.airstreamCamera.gameObject);
				if (fxVessel.airstreamTexture != null)
				{
					fxVessel.airstreamTexture.Release();
					Destroy(fxVessel.airstreamTexture);
				}

				for (int i = 0; i < fxVessel.particles.Count; i++)
				{
					if (fxVessel.particles[i].system != null)
						Destroy(fxVessel.particles[i].system.gameObject);
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
		/// Similar to ReloadVessel(), but much lighter since it does not re-instantiate the camera and particles
		/// </summary>
		public void OnVesselPartCountChanged()
		{
			RemoveVesselFx(true);
			reloadDelayFrames = Math.Max(reloadDelayFrames, 1);
		}

		public override void OnLoadVessel()
		{
			base.OnLoadVessel();

			CleanupEnvelopeRenderers();

			reloadDelayFrames = 20;
		}

		public override void OnUnloadVessel()
		{
			base.OnUnloadVessel();

			RemoveVesselFx(false);
		}

		public void OnDestroy()
		{
			RemoveVesselFx(false, true);
		}

		public void Update()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

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
			if (Time.fixedTime > lastFixedTime && isLoaded)  // gt instead of eq - float equality is fragile
			{
				lastFixedTime = Time.fixedTime;

				// compute once per fixed frame, pass down - was being recomputed in both UpdateParticleSystems and UpdateMaterialProperties
				float entryStrength = GetEntryStrength();

				if (fxVessel.hasParticles) UpdateParticleSystems(entryStrength);

				fxVessel.airstreamCamera.transform.position = GetOrthoCameraPosition();
				fxVessel.airstreamCamera.transform.LookAt(vessel.transform.TransformPoint(fxVessel.vesselBoundCenter));

				UpdateMaterialProperties(entryStrength);
			}

			// unloaded vessel fast path - skip loaded-vessel checks entirely
			if (!isLoaded)
			{
				if (!OverridePhysics)
				{
					double descentRate = vessel.altitude - vslLastAlt;
					vslLastAlt = vessel.altitude;
					if (reloadDelayFrames < 1 && descentRate < 0 && vessel.altitude <= vessel.mainBody.atmosphereDepth)
						CreateVesselFx();
				}
				return;
			}

			// loaded vessel - check if we've left the atmosphere
			if (!OverridePhysics && vessel.altitude > vessel.mainBody.atmosphereDepth)
			{
				RemoveVesselFx(false);
			}
		}

		/// <summary>
		/// Debug drawings
		/// </summary>
		public void OnGUI()
		{
			if (!debugMode || !isLoaded) return;

			Vector3[] vesselPoints = new Vector3[8];
			for (int i = 0; i < 8; i++)
			{
				vesselPoints[i] = vessel.transform.TransformPoint(fxVessel.vesselBounds[i]);
			}
			DrawingUtils.DrawBox(vesselPoints, Color.green);

			Vector3 fwd = vessel.GetFwdVector();
			Vector3 up = vessel.transform.up;
			Vector3 rt = Vector3.Cross(fwd, up);
			DrawingUtils.DrawAxes(vessel.transform.position, fwd, rt, up);

			Transform camTransform = fxVessel.airstreamCamera.transform;
			DrawingUtils.DrawArrow(camTransform.position, camTransform.forward, camTransform.right, camTransform.up, Color.magenta);
		}

		/// <summary>
		/// Handles SOI change - enables/disables effects, changes color configs.
		/// Disables on bodies without atmosphere.
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
		/// Updates the current body and re-caches config values
		/// </summary>
		private void UpdateCurrentBody(CelestialBody body, bool atLoad)
		{
			if (fxVessel != null)
			{
				Logging.Log($"Updating current body for {vessel.name}");

				ConfigManager.Instance.TryGetBodyConfig(body.name, true, out BodyConfig cfg);
				currentBody = cfg;

				CacheConfigValues();  // body changed, typed cache must be refreshed

				if (!atLoad)
				{
					DestroyCommandBuffer();
					InitializeCommandBuffer();
					PopulateCommandBuffer();
				}
			}
		}

		/// <summary>
		/// Updates the material properties each fixed frame
		/// entryStrength is computed once in LateUpdate and passed in
		/// </summary>
		void UpdateMaterialProperties(float entryStrength)
		{
			Matrix4x4 V = fxVessel.airstreamCamera.worldToCameraMatrix;
			Matrix4x4 P = GL.GetGPUProjectionMatrix(fxVessel.airstreamCamera.projectionMatrix, true);
			Matrix4x4 VP = P * V;

			for (int i = 0; i < fxVessel.particleMaterials.Count; i++)
			{
				fxVessel.particleMaterials[i].SetMatrix(ID_AirstreamVP, VP);
			}

			fxVessel.material.SetVector(ID_Velocity,            OverridePhysics ? OverrideEntryDirection : GetEntryVelocity());
			fxVessel.material.SetFloat(ID_EntryStrength,        entryStrength);
			fxVessel.material.SetMatrix(ID_AirstreamVP,         VP);
			fxVessel.material.SetInt(ID_Hdr,                    CameraManager.Instance.ActualHdrState ? 1 : 0);
			fxVessel.material.SetFloat(ID_FxState,              OverridePhysics ? OverrideEffectState : AeroFX.state);
			fxVessel.material.SetFloat(ID_AngleOfAttack,        OverridePhysics ? OverrideAngleOfAttack : Utils.GetAngleOfAttack(vessel));
			fxVessel.material.SetInt(ID_DisableBowshock,        cachedDisableBowshock ? 1 : 0);
			fxVessel.material.SetFloat(ID_LengthMultiplier,     GetLengthMultiplier());
			fxVessel.material.SetFloat(ID_OpacityMultiplier,    cachedOpacityMultiplier);
			fxVessel.material.SetFloat(ID_GlowMultiplier,       cachedGlowMultiplier);
			fxVessel.material.SetFloat(ID_WrapOpacityMultiplier, cachedWrapOpacityMultiplier);
			fxVessel.material.SetFloat(ID_WrapFresnelModifier,  cachedWrapFresnelModifier);
			fxVessel.material.SetFloat(ID_StreakProbability,    cachedStreakProbability);
			fxVessel.material.SetFloat(ID_StreakThreshold,      cachedStreakThreshold);
		}

		/// <summary>
		/// Calculates the total bounds of the entire vessel.
		/// Returns false if the bounding box is invalid.
		/// </summary>
		bool CalculateVesselBounds(AtmoFxVessel fxVessel, Vessel vsl, bool doChecks)
		{
			fxVessel.vesselMaxCorner = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			fxVessel.vesselMinCorner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

			for (int i = 0; i < vsl.parts.Count; i++)
			{
				if ((!Utils.IsPartBoundCompatible(vsl.parts[i])) && doChecks) continue;

				List<Renderer> renderers = vsl.parts[i].FindModelRenderersCached();
				for (int r = 0; r < renderers.Count; r++)
				{
					if (!renderers[r].gameObject.activeInHierarchy) continue;

					bool hasFilter = renderers[r].TryGetComponent(out MeshFilter meshFilter);
					bool isSkinnedRenderer = renderers[r].TryGetComponent(out SkinnedMeshRenderer skinnedModel);

					if (!isSkinnedRenderer)
					{
						if (!hasFilter) continue;
						if (meshFilter.mesh == null) continue;
					}

					if (Utils.CheckLayerModel(renderers[r].transform)) continue;

					Bounds modelBounds = isSkinnedRenderer ? skinnedModel.localBounds : meshFilter.mesh.bounds;
					Vector3[] corners = Utils.GetBoundCorners(modelBounds);

					Matrix4x4 matrix = vsl.transform.worldToLocalMatrix * renderers[r].transform.localToWorldMatrix;

					Vector3[] vesselCorners = new Vector3[8];

					for (int c = 0; c < 8; c++)
					{
						Vector3 v = matrix.MultiplyPoint3x4(corners[c]);

						vesselCorners[c] = v;

						fxVessel.vesselMinCorner = Vector3.Min(fxVessel.vesselMinCorner, v);
						fxVessel.vesselMaxCorner = Vector3.Max(fxVessel.vesselMaxCorner, v);
					}
				}
			}

			if (fxVessel.vesselMaxCorner.x == float.MinValue) return false;

			Vector3 vesselSize = new Vector3(
				Mathf.Abs(fxVessel.vesselMaxCorner.x - fxVessel.vesselMinCorner.x),
				Mathf.Abs(fxVessel.vesselMaxCorner.y - fxVessel.vesselMinCorner.y),
				Mathf.Abs(fxVessel.vesselMaxCorner.z - fxVessel.vesselMinCorner.z)
			);

			Bounds bounds = new Bounds(fxVessel.vesselMinCorner + vesselSize / 2f, vesselSize);

			fxVessel.vesselBounds       = Utils.GetBoundCorners(bounds);
			fxVessel.vesselMaxSize      = Mathf.Max(vesselSize.x, vesselSize.y, vesselSize.z);
			fxVessel.vesselBoundCenter  = bounds.center;
			fxVessel.vesselBoundExtents = vesselSize / 2f;
			fxVessel.vesselBoundRadius  = fxVessel.vesselBoundExtents.magnitude;

			CalculateBaseLengthMultiplier();

			return true;
		}

		/// <summary>
		/// Returns the correct bodyconfig to use, depending on whether override is active
		/// </summary>
		BodyConfig GetCurrentConfig()
		{
			if (OverridePhysics)
			{
				if (_overrideBodyConfig != null) return _overrideBodyConfig;
				else return ConfigManager.Instance.DefaultConfig;
			}
			else
			{
				return currentBody;
			}
		}

		/// <summary>
		/// Returns the normalized surface velocity direction
		/// </summary>
		Vector3 GetEntryVelocity()
		{
			return vessel.srf_velocity.normalized;
		}

		/// <summary>
		/// Returns the velocity of this vessel relative to the active vessel
		/// </summary>
		public Vector3 GetRelativeVelocity()
		{
			if (vessel.isActiveVessel)
				return Vector3.zero;

			return vessel.srf_velocity - FlightGlobals.ActiveVessel.srf_velocity;
		}

		/// <summary>
		/// Returns the effect entry strength for this frame.
		/// Uses cached config values - no dict lookups.
		/// </summary>
		public float GetEntryStrength()
		{
			BodyConfig config = GetCurrentConfig();

			float transitionOffset = config.planetPack.transitionOffset * AeroFX.state;
			float fxScalar = AeroFX.FxScalar + transitionOffset;
			fxScalar *= Mathf.Lerp(0.13f, 1f, AeroFX.state);
			fxScalar += Mathf.Min((float)vessel.dynamicPressurekPa * 10f, 0.2f) * AeroFX.state;
			fxScalar = Mathf.Min(fxScalar, 1f);

			float strength = fxScalar * cachedStrengthBase;

			// div -> mul - reciprocal of strength base is known at cache time
			float invStrengthBase = 1f / cachedStrengthBase;
			float delta = Mathf.Abs(strength - lastStrength) * invStrengthBase;
			strength = Mathf.Lerp(lastStrength, strength, TimeWarp.deltaTime * (1f + delta * 2f));

			lastStrength = strength;

			if (OverridePhysics)
				return OverrideEffectStrength * cachedStrengthMultiplier;
			else
				return strength * cachedStrengthMultiplier;
		}

		/// <summary>
		/// Calculates the base length multiplier from the vessel's bounding radius.
		/// Apollo capsule radius ~2 is used as reference.
		/// </summary>
		void CalculateBaseLengthMultiplier()
		{
			float baseRadius = fxVessel.vesselBoundRadius / 2f;
			fxVessel.baseLengthMultiplier = 1f + (baseRadius - 1f) * 0.3f;
		}

		/// <summary>
		/// Returns the final length multiplier from cached base and config values
		/// </summary>
		float GetLengthMultiplier()
		{
			return fxVessel.baseLengthMultiplier * cachedLengthMultiplier * (float)ModSettings.I["length_mult"];
		}

		/// <summary>
		/// Returns the orthographic camera position aligned with entry velocity
		/// </summary>
		Vector3 GetOrthoCameraPosition()
		{
			float distance = fxVessel.vesselBoundRadius * 1.1f;

			Vector3 dir = OverridePhysics ? OverrideEntryDirection : GetEntryVelocity();
			Vector3 localPos = fxVessel.vesselBoundCenter + distance * vessel.transform.InverseTransformDirection(dir);

			return vessel.transform.TransformPoint(localPos);
		}
	}
}
