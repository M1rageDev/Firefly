using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AtmosphericFx
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class AssetLoader : MonoBehaviour
	{
		// singleton
		public static AssetLoader Instance { get; private set; }

		// path to the assets
		public const string iconPath = "AtmosphericFx/Assets/Icons/Icon";
		public const string bundlePath ="GameData/AtmosphericFx/Assets/Shaders/fxshaders.ksp";

		// loaded icon
		public Texture2D iconTexture;

		// our loaded assets
		public Dictionary<string, Shader> loadedShaders = new Dictionary<string, Shader>();
		public Dictionary<string, Material> loadedMaterials = new Dictionary<string, Material>();
		public Dictionary<string, GameObject> loadedPrefabs = new Dictionary<string, GameObject>();

		// the actual stuff
		public Material globalMaterial;
		public bool hasMaterial = false;

		public Shader globalShader;
		public bool hasShader = false;

		public GameObject sparkParticles;
		public bool hasSparkParticles = false;

		public GameObject chunkParticles;
		public bool hasChunkParticles = false;

		public GameObject alternateChunkParticles;
		public bool hasAlternateChunkParticles = false;

		public GameObject smokeParticles;
		public bool hasSmokeParticles = false;

		// is everything loaded?
		public bool allAssetsLoaded = false;

		public void Awake()
		{
			Instance = this;

			Logging.Log("AssetLoader Awake");

			LoadAssets();
			InitAssets();
		}

		/// <summary>
		/// Initializes all assets
		/// </summary>
		internal void InitAssets()
		{
			Logging.Log("Versioning:");
			Logging.Log(Versioning.VersionAuthor);
			Logging.Log(Versioning.Version);

			// load shader
			bool hasShader = TryGetShader("MirageDev/GridProjectionFX", out Shader sh);
			if (!hasShader)
			{
				Logging.Log("Failed to load shader, halting startup");
				return;
			}
			globalShader = sh;

			// load material
			bool hasMaterial = TryGetMaterial("GridProjectionMat", out Material mt);
			if (!hasMaterial)
			{
				Logging.Log("Failed to load reentry material, halting startup");
				return;
			}
			globalMaterial = mt;

			// initialize material
			globalMaterial.shader = globalShader;

			// load particle systems
			hasSparkParticles = TryGetPrefab("SparkParticles", out sparkParticles);
			hasChunkParticles = TryGetPrefab("ChunkParticles", out chunkParticles);
			hasAlternateChunkParticles = TryGetPrefab("AlternateChunkParticles", out alternateChunkParticles);
			hasSmokeParticles = TryGetPrefab("SmokeParticles", out smokeParticles);

			if (!hasSparkParticles || !hasChunkParticles || !hasAlternateChunkParticles || !hasSmokeParticles)
			{
				Logging.Log($"Spark particles loaded? {hasSparkParticles}");
				Logging.Log($"Chunk particles loaded? {hasChunkParticles}");
				Logging.Log($"Alternate chunk particles loaded? {hasAlternateChunkParticles}");
				Logging.Log($"Smoke particles loaded? {hasSmokeParticles}");

				Logging.Log("Failed to load particles, halting startup");
				return;
			}

			allAssetsLoaded = true;
		}

		/// <summary>
		/// Clears all loaded asset dictionaries
		/// </summary>
		internal void ClearAssets()
		{
			loadedShaders.Clear();
			loadedMaterials.Clear();
			loadedPrefabs.Clear();
		}

		/// <summary>
		/// Loads all available assets from the asset bundle into the dictionaries
		/// </summary>
		internal void LoadAssets()
		{
			// load the icon texture
			iconTexture = GameDatabase.Instance.GetTexture(iconPath, false);

			// load the asset bundle
			string loadPath = Path.Combine(KSPUtil.ApplicationRootPath, bundlePath);
			AssetBundle bundle = AssetBundle.LoadFromFile(loadPath);

			if (!bundle)
			{
				Logging.Log($"Bundle couldn't be loaded: {loadPath}");
			}
			else
			{
				loadedShaders.Clear();

				Shader[] shaders = bundle.LoadAllAssets<Shader>();
				foreach (Shader shader in shaders)
				{
					Logging.Log($"Found shader {shader.name}");

					loadedShaders.Add(shader.name, shader);
				}

				Material[] materials = bundle.LoadAllAssets<Material>();
				foreach (Material material in materials)
				{
					Logging.Log($"Found material {material.name}");

					loadedMaterials.Add(material.name, material);
				}

				GameObject[] prefabs = bundle.LoadAllAssets<GameObject>();
				foreach (GameObject prefab in prefabs)
				{
					Logging.Log($"Found prefab {prefab.name}");

					loadedPrefabs.Add(prefab.name, prefab);
				}
			}
		}

		public bool TryGetShader(string name, out Shader shader)
		{
			if (!loadedShaders.ContainsKey(name))
			{
				// shader was not loaded
				shader = null;
				return false;
			}
			
			// shader was loaded, pass it to the out parameter
			shader = loadedShaders[name];
			return true;
		}

		public bool TryGetMaterial(string name, out Material shader)
		{
			if (!loadedMaterials.ContainsKey(name))
			{
				// material was not loaded
				shader = null;
				return false;
			}

			// material was loaded, pass it to the out parameter
			shader = loadedMaterials[name];
			return true;
		}

		public bool TryGetPrefab(string name, out GameObject particle)
		{
			if (!loadedPrefabs.ContainsKey(name))
			{
				// prefab was not loaded
				particle = null;
				return false;
			}

			// prefab was loaded, pass it to the out parameter
			particle = loadedPrefabs[name];
			return true;
		}
	}
}
