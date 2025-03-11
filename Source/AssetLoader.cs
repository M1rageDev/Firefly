using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class AssetLoader : MonoBehaviour
	{
		// singleton
		public static AssetLoader Instance { get; private set; }

		// path to the assets
		public const string iconTexturePath = "Firefly/Assets/Textures/Icon";
		public const string bundlePath = "GameData/Firefly/Assets/Shaders/fxshaders.ksp";

		// loaded assets
		public Dictionary<string, Shader> loadedShaders = new Dictionary<string, Shader>();
		public Dictionary<string, Material> loadedMaterials = new Dictionary<string, Material>();
		public Dictionary<string, GameObject> loadedPrefabs = new Dictionary<string, GameObject>();
		public Dictionary<string, Texture2D> loadedTextures = new Dictionary<string, Texture2D>();

		// the actual stuff
		public Material globalMaterial;
		public bool hasMaterial = false;

		public Shader globalShader;
		public bool hasShader = false;

		public Texture2D iconTexture;

		// is everything loaded?
		public bool allAssetsLoaded = false;

		// the bundle
		AssetBundle bundle;

		public void Awake()
		{
			Instance = this;

			Logging.Log("AssetLoader Awake");

			LoadAssets();
			InitAssets();

			if (!allAssetsLoaded)
			{
				ConfigManager.Instance.errorList.Add(new ModLoadError(
					cause: ModLoadError.ProbableCause.IncorrectInstall,
					isSerious: true,
					sourcePath: "Firefly asset loader",
					description: "The asset loader did not load every required asset."
				));

				return;
			}

			// disable the stock effects
			Logging.Log("Disabling stock effects");
			Logging.Log("Turning the quality down to minimal");
			GameSettings.AERO_FX_QUALITY = 0;
		}

		/// <summary>
		/// Initializes all assets
		/// </summary>
		internal void InitAssets()
		{
			Logging.Log("Versioning:");
			Logging.Log(Versioning.VersionAuthor(this));
			Logging.Log(Versioning.Version(this));

			// load shader
			bool hasShader = TryGetShader("MirageDev/AtmosphericEntry", out Shader sh);
			if (!hasShader)
			{
				Logging.Log("Failed to load shader, halting startup");
				return;
			}
			globalShader = sh;

			// load material
			bool hasMaterial = TryGetMaterial("Reentry", out Material mt);
			if (!hasMaterial)
			{
				Logging.Log("Failed to load reentry material, halting startup");
				return;
			}
			globalMaterial = mt;

			// initialize material
			globalMaterial.shader = globalShader;

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
			// load icon and other textures
			iconTexture = GameDatabase.Instance.GetTexture(iconTexturePath, false);
			for (int i = 0; i < ConfigManager.Instance.texturesToLoad.Count; i++)
			{
				loadedTextures[ConfigManager.Instance.texturesToLoad[i]] = GameDatabase.Instance.GetTexture(ConfigManager.Instance.texturesToLoad[i], false);
			}

			// load the asset bundle
			string loadPath = Path.Combine(KSPUtil.ApplicationRootPath, bundlePath);
			bundle = AssetBundle.LoadFromFile(loadPath);

			if (!bundle)
			{
				Logging.Log($"Bundle couldn't be loaded: {loadPath}");
				ConfigManager.Instance.errorList.Add(new ModLoadError(
					cause: ModLoadError.ProbableCause.IncorrectInstall,
					isSerious: true,
					sourcePath: "Firefly asset loader",
					description: "The asset loader could not load the asset bundle."
				));
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

		public void ReloadAssets()
		{
			bundle.Unload(true);
			ClearAssets();

			LoadAssets();
			InitAssets();
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
