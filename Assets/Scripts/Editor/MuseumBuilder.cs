using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds the Phase 3 museum section into its own scene (Museum.unity, first in Build Settings):
//
//   Entrance Corridor (start) -> Grand Gallery (hub: exhibits, chandeliers, a staircase up to a dais)
//   -> locked door -> Vampire History Wing (the Morvayne tomb, the curator's journal) -> locked Archive door
//
// Walking into the gallery slams the corridor door and spawns two thralls; the last one drops the History
// Wing Key. The wing has a second, smaller ambush. Readable placards and letters carry the story.
//
// Geometry is boxes with world-space UVs (so the Environment materials tile evenly), props are copies of
// the third-party prefabs measured and scaled to set sizes. Safe to re-run: the Museum root, the player
// and the camera rig are rebuilt; data assets and the volume profile are reused.
// Uses the enemy and pickup prefabs from Build Prototype Arena, so run that once first.
public static class MuseumBuilder
{
	private const string ScenePath = "Assets/Scenes/Museum.unity";
	private const string RootName = "Museum";

	private const string EnvironmentMaterialFolder = "Assets/Materials/Environment";
	private const string KeyFolder = "Assets/Data/Keys";
	private const string WingKeyPath = KeyFolder + "/HistoryWingKey.asset";
	private const string ArchiveKeyPath = KeyFolder + "/ArchiveKey.asset";
	private const string VolumeProfilePath = "Assets/Settings/MuseumVolumeProfile.asset";
	private const string SkyboxPath = "Assets/ThirdParty/Environment/SkySeries Freebie/DarkStorm.mat";

	private const string Pegasus = "Assets/ThirdParty/Props/Pegasus Studios/Elegant Interior Decor 3D Models Pack/Resources/Prefabs/";
	private const string Dungeon = "Assets/ThirdParty/Props/Dungeon/URP/Prefabs/";
	private const string Coffins = "Assets/ThirdParty/Props/LoafbrrAssets/CoffinAndFlowers/Prefab/";
	private const string Cobwebs = "Assets/ThirdParty/Props/PolyOne/Cobwebs Pack/Prefabs/";
	private const string Frames = "Assets/ThirdParty/Props/Picture_Frames_FREE/Prefabs/";
	private const string KeyModelPath = "Assets/ThirdParty/Pickups/Rust Key/Prefabs/rust_key.prefab";
	// the prefab still uses the pack's Built-in "Autodesk Interactive" material (pink in URP); this is the
	// hand-converted URP/Lit copy
	private const string KeyMaterialPath = "Assets/ThirdParty/Pickups/Rust Key/Materials/rust_key_standard.mat";

	private const string DoorOpenClip = "freesound_community-door-creak-38423.mp3";
	private const string DoorSlamClip = "freesound_community-door-slam-79889.mp3";
	private const string DoorLockedClip = "freesound_community-rattlinglockeddoor-100095.mp3";
	private const string KeyClip = "litupsubway-key-collect-sfx-522219.mp3";
	private const string PageClip = "creatorshome-turn-a-page-336933.mp3";

	private const int IgnoreRaycastLayer = PrototypeArenaBuilder.IgnoreRaycastLayer;

	// room extents (metres). The gallery's dais and the wing floor are 1.8 m up
	private const float CorridorHeight = 4.5f;
	private const float GalleryHeight = 9f;
	private const float DaisHeight = 1.8f;
	private const float WingHeight = 5f;
	private const float Wall = 0.5f;

	private static readonly Color CandleColor = new Color(1f, 0.62f, 0.32f);
	private static readonly Color ChandelierColor = new Color(1f, 0.75f, 0.5f);

	// per-run state
	private static Material _marble, _stone, _wood, _fabric;
	private static AudioClip _pageClip;

	[MenuItem("Tools/Crimson Nights/Build Museum")]
	public static void Build()
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode)
		{
			Debug.LogError("[MuseumBuilder] Exit Play Mode first.");
			return;
		}

		var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeArenaBuilder.EnemyPrefabPath);
		var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeArenaBuilder.PlayerPrefabPath);
		if (enemyPrefab == null || playerPrefab == null)
		{
			Debug.LogError("[MuseumBuilder] Enemy or player prefab missing. Run Tools > Crimson Nights > Build Prototype Arena first.");
			return;
		}

		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
		var scene = OpenOrCreateScene();

		_marble = LoadMaterial("Marble", new Color(0.75f, 0.73f, 0.7f));
		_stone = LoadMaterial("Stone Wall", new Color(0.35f, 0.33f, 0.32f));
		_wood = LoadMaterial("Dark Wood", new Color(0.22f, 0.13f, 0.08f));
		_fabric = LoadMaterial("Fabric", new Color(0.4f, 0.05f, 0.07f));
		_pageClip = PrototypeArenaBuilder.LoadClip(PageClip);

		KeyData wingKey = GetOrCreateKey(WingKeyPath, "History Wing Key");
		KeyData archiveKey = GetOrCreateKey(ArchiveKeyPath, "Archive Key");
		VolumeProfile profile = GetOrCreateVolumeProfile();
		AddPlayerInteraction();
		AssetDatabase.SaveAssets();

		ClearPreviousBuild();
		var root = new GameObject(RootName).transform;
		SetupLighting(root, profile);

		BuildCorridor(root, out Door entranceDoor);
		BuildGallery(root, enemyPrefab, wingKey, entranceDoor, out Door wingDoor);
		BuildWing(root, enemyPrefab, archiveKey, wingDoor);

		// navmesh, baked at runtime from the colliders under the root (doors are left out of the bake)
		var surface = root.gameObject.AddComponent<NavMeshSurface>();
		surface.collectObjects = CollectObjects.Children;
		surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
		surface.layerMask = 1 << 0;
		root.gameObject.AddComponent<RuntimeNavMeshBaker>();

		// player at the corridor's far end, facing the gallery
		var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
		player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -30f), Quaternion.identity);
		PrototypeArenaBuilder.SetupCameras(player.transform.Find("PlayerCameraRoot"));
		Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;

		AddSceneToBuildSettings();
		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		Debug.Log("[MuseumBuilder] Museum built into " + ScenePath);
	}

	// ---------------------------------------------------------------- setup

	private static UnityEngine.SceneManagement.Scene OpenOrCreateScene()
	{
		var active = EditorSceneManager.GetActiveScene();
		if (active.path == ScenePath) return active;

		if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
		{
			return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
		}
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		EditorSceneManager.SaveScene(scene, ScenePath);
		return scene;
	}

	// first in the build, so a build starts in the museum; the arena stays in as a combat test
	private static void AddSceneToBuildSettings()
	{
		var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
		scenes.RemoveAll(s => s.path == ScenePath);
		scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
		EditorBuildSettings.scenes = scenes.ToArray();
	}

	private static void ClearPreviousBuild()
	{
		GameObject old = GameObject.Find(RootName);
		if (old != null) Object.DestroyImmediate(old);
		foreach (var fpc in Object.FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			Object.DestroyImmediate(fpc.gameObject);
		}
	}

	private static void AddPlayerInteraction()
	{
		GameObject root = PrefabUtility.LoadPrefabContents(PrototypeArenaBuilder.PlayerPrefabPath);
		try
		{
			PrototypeArenaBuilder.AddInteraction(root);
			PrototypeArenaBuilder.AddPlayerLight(root);
			PrefabUtility.SaveAsPrefabAsset(root, PrototypeArenaBuilder.PlayerPrefabPath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// the hand-made Environment materials, or a flat stand-in if one is missing
	private static Material LoadMaterial(string name, Color fallback)
	{
		var material = AssetDatabase.LoadAssetAtPath<Material>($"{EnvironmentMaterialFolder}/{name}.mat");
		if (material != null) return material;

		Debug.LogWarning($"[MuseumBuilder] {EnvironmentMaterialFolder}/{name}.mat not found, using a flat colour.");
		material = new Material(Shader.Find(PrototypeArenaBuilder.LitShader));
		material.SetColor("_BaseColor", fallback);
		return material;
	}

	private static KeyData GetOrCreateKey(string path, string displayName)
	{
		KeyData key = PrototypeArenaBuilder.GetOrCreateAsset<KeyData>(path, out bool created);
		if (created)
		{
			key.DisplayName = displayName;
			EditorUtility.SetDirty(key);
		}
		return key;
	}

	// dark, slightly desaturated grade with a heavy vignette and a little grain
	private static VolumeProfile GetOrCreateVolumeProfile()
	{
		var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
		if (profile != null) return profile;

		profile = ScriptableObject.CreateInstance<VolumeProfile>();
		AssetDatabase.CreateAsset(profile, VolumeProfilePath);

		var tonemapping = profile.Add<Tonemapping>(true);
		tonemapping.mode.Override(TonemappingMode.ACES);

		var bloom = profile.Add<Bloom>(true);
		bloom.threshold.Override(0.9f);
		bloom.intensity.Override(0.6f);
		bloom.scatter.Override(0.65f);

		var vignette = profile.Add<Vignette>(true);
		vignette.intensity.Override(0.38f);
		vignette.smoothness.Override(0.45f);

		var colorAdjustments = profile.Add<ColorAdjustments>(true);
		colorAdjustments.postExposure.Override(0.3f);
		colorAdjustments.contrast.Override(12f);
		colorAdjustments.saturation.Override(-15f);

		var grain = profile.Add<FilmGrain>(true);
		grain.type.Override(FilmGrainLookup.Thin1);
		grain.intensity.Override(0.15f);

		foreach (VolumeComponent component in profile.components)
		{
			component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
			AssetDatabase.AddObjectToAsset(component, profile);
		}
		EditorUtility.SetDirty(profile);
		return profile;
	}

	// storm sky through the gallery windows, a faint moon, dark flat ambient and fog; candles do the rest
	private static void SetupLighting(Transform root, VolumeProfile profile)
	{
		var skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
		if (skybox != null) RenderSettings.skybox = skybox;
		RenderSettings.ambientMode = AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(0.07f, 0.06f, 0.085f);
		RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
		RenderSettings.reflectionIntensity = 0.3f;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogDensity = 0.018f;
		RenderSettings.fogColor = new Color(0.04f, 0.035f, 0.05f);

		var moon = new GameObject("Moonlight").AddComponent<Light>();
		moon.transform.SetParent(root, false);
		moon.transform.rotation = Quaternion.Euler(35f, -60f, 0f);
		moon.type = LightType.Directional;
		moon.color = new Color(0.55f, 0.62f, 0.85f);
		moon.intensity = 0.35f;
		moon.shadows = LightShadows.Soft;
		RenderSettings.sun = moon;

		var volume = new GameObject("PostProcessing").AddComponent<Volume>();
		volume.transform.SetParent(root, false);
		volume.isGlobal = true;
		volume.sharedProfile = profile;
	}

	// ---------------------------------------------------------------- rooms

	// 4 m wide, 18 m long, z -32.5 .. -14.5. Locked front doors behind the player, the hall table with the
	// curator's letter, candles, portraits, and the gallery doors (open) at the far end
	private static void BuildCorridor(Transform root, out Door entranceDoor)
	{
		Transform room = Group(root, "EntranceCorridor");
		const float z0 = -32.5f, z1 = -14.5f;
		float zc = (z0 + z1) * 0.5f, length = z1 - z0;

		CreateBox(room, "Floor", new Vector3(0f, -0.25f, zc - 0.25f), new Vector3(4f, 0.5f, length + 0.5f), _wood, 2f);
		CreateBox(room, "Ceiling", new Vector3(0f, CorridorHeight + 0.25f, zc), new Vector3(5f, 0.5f, length), _wood, 2f);
		CreateBox(room, "Wall_W", new Vector3(-2f - Wall * 0.5f, CorridorHeight * 0.5f, zc), new Vector3(Wall, CorridorHeight, length), _stone, 2f);
		CreateBox(room, "Wall_E", new Vector3(2f + Wall * 0.5f, CorridorHeight * 0.5f, zc), new Vector3(Wall, CorridorHeight, length), _stone, 2f);
		BuildWallX(room, "Wall_S", -32.25f, -2.5f, 2.5f, 0f, CorridorHeight, _stone, new Opening(0f, 2.2f, 0f, 3.2f));
		CreateBox(room, "Carpet", new Vector3(0f, 0.01f, zc), new Vector3(1.6f, 0.02f, length - 1f), _fabric, 1f, false);

		// front doors: locked for good, with a wall behind so there's no void
		Door frontDoor = CreateDoor(room, "FrontDoor", new Vector3(0f, 0f, -32.25f), 0f, 2.2f, 3.2f);
		frontDoor.Locked = true;
		frontDoor.LockedMessage = "The front doors are locked from outside.";
		CreateBox(room, "FrontDoorBacking", new Vector3(0f, 1.6f, -32.75f), new Vector3(2.6f, 3.2f, 0.5f), _stone, 2f);

		entranceDoor = CreateDoor(room, "GalleryDoor", new Vector3(0f, 0f, -14.25f), 0f, 2.4f, 3f);
		entranceDoor.StartOpen = true;

		// candle stands down both sides
		foreach (float z in new[] { -28f, -21f })
		{
			foreach (float x in new[] { -1.55f, 1.55f })
			{
				GameObject stand = PlaceProp(room, Pegasus + "Candle Stand.prefab", new Vector3(x, 0f, z), 0f, 1.7f);
				AddCandleLight(room, stand, 1.6f, 6f);
			}
		}

		// hall table with the letter, just ahead of the start
		GameObject table = PlaceProp(room, Pegasus + "Royal Table.prefab", new Vector3(1.45f, 0f, -26.5f), 90f, 0.85f);
		float tableTop = table != null ? WorldBounds(table).max.y : 0.85f;
		GameObject letter = PlaceProp(room, Dungeon + "Misc/Item/NGF_Sheet_01.prefab", new Vector3(1.4f, tableTop, -26.5f), 15f, 0f, false);
		AddReadable(room, "Letter", new Vector3(1.4f, tableTop + 0.1f, -26.5f), new Vector3(0.6f, 0.3f, 0.6f), letter,
			"A letter, left on the hall table",
			"Hunter,\n\n" +
			"You came, as I hoped. The Morvayne Collection is closed to the public after dark, but I have left the doors open for you.\n\n" +
			"Eleven people have gone missing in the valley since spring. The constable says they ran off. You and I know better.\n\n" +
			"Begin in the Grand Gallery, and look closely at the exhibits. I assure you that every piece is authentic.\n\n" +
			"- The Curator");

		PlaceOnWall(room, Pegasus + "Painting.prefab", new Vector3(-2f, 2.3f, -23.5f), Vector3.right, 1.4f);
		PlaceOnWall(room, Frames + "SM_frame_01.prefab", new Vector3(2f, 2.3f, -19f), Vector3.left, 1.1f);
		PlaceOnWall(room, Frames + "SM_frame_02.prefab", new Vector3(-2f, 2.2f, -17f), Vector3.right, 0.9f);

		PlaceHanging(room, Cobwebs + "SM_Cobwebs_01.prefab", new Vector3(-1.6f, CorridorHeight, -32f), 0f, 0.9f);
		PlaceHanging(room, Cobwebs + "SM_Cobwebs_03.prefab", new Vector3(1.6f, CorridorHeight, -15f), 180f, 0.9f);
	}

	// 24 x 28 m, 9 m high, z -14.5 .. 14.5. Columns, two chandeliers, tall windows onto the storm, exhibits
	// down both side aisles, and a staircase up to a dais with the History Wing door at the top
	private static void BuildGallery(Transform root, GameObject enemyPrefab, KeyData wingKey, Door entranceDoor, out Door wingDoor)
	{
		Transform room = Group(root, "GrandGallery");

		CreateBox(room, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(25f, 0.5f, 29f), _marble, 2f);
		CreateBox(room, "Ceiling", new Vector3(0f, GalleryHeight + 0.25f, 0f), new Vector3(25f, 0.5f, 29f), _wood, 2f);
		// tall windows between the columns on both long walls
		var windows = new[] { new Opening(-5f, 1.6f, 4f, 8f), new Opening(1f, 1.6f, 4f, 8f), new Opening(7f, 1.6f, 4f, 8f) };
		BuildWallZ(room, "Wall_W", -12.25f, -14.5f, 14.5f, 0f, GalleryHeight, _stone, windows);
		BuildWallZ(room, "Wall_E", 12.25f, -14.5f, 14.5f, 0f, GalleryHeight, _stone, windows);
		BuildWallX(room, "Wall_S", -14.25f, -12.5f, 12.5f, 0f, GalleryHeight, _stone, new Opening(0f, 2.4f, 0f, 3f));
		BuildWallX(room, "Wall_N", 14.25f, -12.5f, 12.5f, 0f, GalleryHeight, _stone, new Opening(0f, 2f, DaisHeight, DaisHeight + 2.8f));
		CreateBox(room, "Carpet", new Vector3(0f, 0.01f, -4.8f), new Vector3(3f, 0.02f, 18.6f), _fabric, 1f, false);

		// dais along the north wall (z 8 .. 14) with a balustrade, reached by nine 0.2 m steps
		CreateBox(room, "Dais", new Vector3(0f, DaisHeight * 0.5f, 11.25f), new Vector3(24f, DaisHeight, 6.5f), _stone, 2f);
		const int steps = 9;
		for (int i = 0; i < steps; i++)
		{
			float height = DaisHeight * (i + 1) / steps;
			float depth = 0.4f * (steps - i);
			CreateBox(room, $"Step_{i}", new Vector3(0f, height * 0.5f, 8f - depth * 0.5f), new Vector3(8f, height, depth), _marble, 2f);
		}
		CreateBox(room, "Balustrade_W", new Vector3(-8f, DaisHeight + 0.5f, 8.1f), new Vector3(8f, 1f, 0.2f), _wood, 2f);
		CreateBox(room, "Balustrade_E", new Vector3(8f, DaisHeight + 0.5f, 8.1f), new Vector3(8f, 1f, 0.2f), _wood, 2f);
		CreateCarpetStairs(room);

		// columns down both sides, with wooden bases and capitals
		foreach (float z in new[] { -8f, -2f, 4f })
		{
			foreach (float x in new[] { -8f, 8f })
			{
				CreateBox(room, "Column", new Vector3(x, GalleryHeight * 0.5f, z), new Vector3(1.2f, GalleryHeight, 1.2f), _stone, 2f);
				CreateBox(room, "ColumnBase", new Vector3(x, 0.25f, z), new Vector3(1.6f, 0.5f, 1.6f), _wood, 1f);
				CreateBox(room, "ColumnCapital", new Vector3(x, GalleryHeight - 0.2f, z), new Vector3(1.6f, 0.4f, 1.6f), _wood, 1f);
			}
		}

		// two chandeliers down the centre (the only shadowed point lights), lamps in the side aisles
		foreach (float z in new[] { -7f, 3f })
		{
			GameObject chandelier = PlaceHanging(room, Pegasus + "Chandelier.prefab", new Vector3(0f, GalleryHeight, z), 0f, 2.2f);
			float lightHeight = chandelier != null ? WorldBounds(chandelier).center.y : GalleryHeight - 1.5f;
			AddPointLight(room, "ChandelierLight", new Vector3(0f, lightHeight, z), ChandelierColor, 22f, 18f, true, false);
		}
		foreach (float z in new[] { -11f, 1f })
		{
			foreach (float x in new[] { -11f, 11f })
			{
				GameObject lamp = PlaceProp(room, Pegasus + "Pillar Lamp.prefab", new Vector3(x, 0f, z), 0f, 2f);
				AddCandleLight(room, lamp, 3f, 7f);
			}
		}
		foreach (float x in new[] { -1.9f, 1.9f })
		{
			GameObject stand = PlaceProp(room, Pegasus + "Candle Stand.prefab", new Vector3(x, DaisHeight, 13.3f), 0f, 1.8f);
			AddCandleLight(room, stand, 2f, 6f);
		}

		BuildGalleryExhibits(room);

		// decoration: portraits either side of the entrance, vases on the dais, cobwebs high in the corners
		PlaceOnWall(room, Frames + "SM_frame_01.prefab", new Vector3(-6f, 2.8f, -14f), Vector3.forward, 1.8f);
		PlaceOnWall(room, Frames + "SM_frame_02.prefab", new Vector3(6f, 2.8f, -14f), Vector3.forward, 1.8f);
		PlaceProp(room, Pegasus + "Vase.prefab", new Vector3(-6f, DaisHeight, 12.8f), 0f, 1.1f);
		PlaceProp(room, Pegasus + "Vase.prefab", new Vector3(6f, DaisHeight, 12.8f), 0f, 1.1f);
		PlaceHanging(room, Cobwebs + "SM_Cobwebs_02.prefab", new Vector3(-11.6f, GalleryHeight, -13.9f), 0f, 1.6f);
		PlaceHanging(room, Cobwebs + "SM_Cobwebs_05.prefab", new Vector3(11.6f, GalleryHeight, -13.9f), 90f, 1.6f);
		PlaceHanging(room, Cobwebs + "SM_Cobwebs_07.prefab", new Vector3(11.6f, GalleryHeight, 13.9f), 180f, 1.6f);
		PlaceHanging(room, Cobwebs + "SM_Cobwebs_02.prefab", new Vector3(-11.6f, GalleryHeight, 13.9f), 270f, 1.6f);

		// History Wing door: locked until the player has the key the gallery thralls drop
		wingDoor = CreateDoor(room, "WingDoor", new Vector3(0f, DaisHeight, 14.25f), 180f, 2f, 2.8f);
		wingDoor.Locked = true;
		wingDoor.RequiredKey = wingKey;

		// the fight: crossing into the gallery slams the corridor door and two thralls appear by the stairs
		GameObject key = CreateKeyPickup(room, wingKey, new Vector3(0f, 0f, 2f));
		CreateEncounter(room, "GalleryEncounter", new Vector3(0f, 1.5f, -10.5f), new Vector3(24f, 3f, 3f), enemyPrefab,
			new[] { new Vector3(-5f, 0f, 5.5f), new Vector3(5f, 0f, 5.5f) }, entranceDoor,
			"The doors slam shut behind you.", "The gallery falls silent. Something glints on the floor.", key);

		PlacePickup(room, PrototypeArenaBuilder.AmmoPickupPrefabPath, new Vector3(10.5f, 0f, -12.5f));
		PlacePickup(room, PrototypeArenaBuilder.HealthPickupPrefabPath, new Vector3(-10.5f, 0f, 6.5f));
		PlacePickup(room, PrototypeArenaBuilder.AmmoPickupPrefabPath, new Vector3(-10.5f, 0f, -12.5f));
	}

	// the carpet runs up the stairs as one strip per step, just proud of each tread
	private static void CreateCarpetStairs(Transform room)
	{
		const int steps = 9;
		for (int i = 0; i < steps; i++)
		{
			float height = DaisHeight * (i + 1) / steps;
			float front = 8f - 0.4f * (steps - i);
			CreateBox(room, "StairCarpet", new Vector3(0f, height + 0.01f, front + 0.2f), new Vector3(3f, 0.02f, 0.4f), _fabric, 1f, false);
		}
		CreateBox(room, "DaisCarpet", new Vector3(0f, DaisHeight + 0.01f, 11f), new Vector3(3f, 0.02f, 6f), _fabric, 1f, false);
	}

	// four exhibits in the side aisles (between the columns and the walls), each with a placard
	private static void BuildGalleryExhibits(Transform room)
	{
		// I: the coffin, west aisle
		CreateBox(room, "Plinth_Coffin", new Vector3(-10.2f, 0.2f, -5f), new Vector3(1.6f, 0.4f, 3f), _marble, 1f);
		PlaceProp(room, Coffins + "Coffins/Coffin_Wood_A.prefab", new Vector3(-10.2f, 0.4f, -5f), 0f, 0.8f, true, 2.6f);
		CreatePlacard(room, new Vector3(-8.7f, 0f, -5f), 90f,
			"Exhibit I - The Morvayne Coffin",
			"Oak, iron and lead. c. 1640.\n\n" +
			"Recovered from the family crypt beneath this house, where it had lain sealed for two centuries.\n\n" +
			"Visitors often ask why the lock is fitted to the inside of the lid. The museum has no explanation to offer.");

		// II: the Countess's portrait, east wall
		PlaceOnWall(room, Pegasus + "Painting.prefab", new Vector3(12f, 2.6f, -5f), Vector3.left, 2.4f);
		CreatePlacard(room, new Vector3(8.7f, 0f, -5f), -90f,
			"Exhibit II - Countess Ilse Morvayne",
			"Oil on canvas. Painted in 1702 by Matthias Kell, and again by the same hand in 1781.\n\n" +
			"Kell was nineteen at the first sitting and ninety-eight at the second. The Countess does not appear to have aged between them.\n\n" +
			"The artist's remains were never found.");

		// III: the reliquary of broken stakes, east aisle
		CreateBox(room, "Plinth_Reliquary", new Vector3(10.2f, 0.5f, 1f), new Vector3(1.2f, 1f, 1.2f), _marble, 1f);
		PlaceProp(room, Dungeon + "Misc/Item/NGF_Casket_01.prefab", new Vector3(10.2f, 1f, 1f), 90f, 0f, true, 0.8f);
		CreatePlacard(room, new Vector3(8.7f, 0f, 1f), -90f,
			"Exhibit III - A Hunter's Reliquary",
			"Walnut casket with silver fittings, containing eleven ash stakes, each snapped in two.\n\n" +
			"Donated by the Morvayne family, who gave its provenance only as \"the east lawn\".\n\n" +
			"The stakes' previous owners have not been identified.");

		// IV: the funeral flowers, west aisle
		CreateBox(room, "Plinth_Flowers", new Vector3(-10.2f, 0.5f, 1f), new Vector3(1.2f, 1f, 1.2f), _marble, 1f);
		PlaceProp(room, Coffins + "FlowerArrangements/FA_1.prefab", new Vector3(-10.2f, 1f, 1f), 0f, 0.9f);
		CreatePlacard(room, new Vector3(-8.7f, 0f, 1f), 90f,
			"Exhibit IV - Funeral Arrangement",
			"Lilies and nightshade, laid at the Countess's interment in 1793.\n\n" +
			"The arrangement is replaced every night. No member of staff will admit to replacing it.\n\n" +
			"Please do not touch the flowers. They have not been cut.");
	}

	// 12 x 12 m at dais height, z 14.5 .. 26. The Morvayne tomb, coffins along the walls, the curator's
	// journal, a second ambush, and the locked Archive door teasing what's next
	private static void BuildWing(Transform root, GameObject enemyPrefab, KeyData archiveKey, Door wingDoor)
	{
		Transform room = Group(root, "HistoryWing");
		const float floor = DaisHeight, top = DaisHeight + WingHeight;

		CreateBox(room, "Floor", new Vector3(0f, floor - 0.25f, 20.25f), new Vector3(13f, 0.5f, 12.5f), _wood, 2f);
		CreateBox(room, "Ceiling", new Vector3(0f, top + 0.25f, 20.25f), new Vector3(13f, 0.5f, 12.5f), _wood, 2f);
		CreateBox(room, "Wall_W", new Vector3(-6.25f, floor + WingHeight * 0.5f, 20.25f), new Vector3(Wall, WingHeight, 12.5f), _stone, 2f);
		CreateBox(room, "Wall_E", new Vector3(6.25f, floor + WingHeight * 0.5f, 20.25f), new Vector3(Wall, WingHeight, 12.5f), _stone, 2f);
		BuildWallX(room, "Wall_N", 26.25f, -6.5f, 6.5f, floor, WingHeight, _stone, new Opening(0f, 2f, 0f, 2.8f));
		CreateBox(room, "Carpet", new Vector3(0f, floor + 0.01f, 16.5f), new Vector3(2f, 0.02f, 3.5f), _fabric, 1f, false);

		Door archiveDoor = CreateDoor(room, "ArchiveDoor", new Vector3(0f, floor, 26.25f), 180f, 2f, 2.8f);
		archiveDoor.Locked = true;
		archiveDoor.RequiredKey = archiveKey;
		archiveDoor.LockedMessage = "A brass plate reads: ARCHIVE - STAFF ONLY. It needs the Archive Key.";
		CreateBox(room, "ArchiveDoorBacking", new Vector3(0f, floor + 1.4f, 26.75f), new Vector3(2.4f, 2.8f, 0.5f), _stone, 2f);

		// the tomb on a low step, candles at its corners
		CreateBox(room, "TombStep", new Vector3(0f, floor + 0.1f, 21.5f), new Vector3(3.6f, 0.2f, 4.4f), _marble, 1f);
		PlaceProp(room, Coffins + "Coffins/Coffin_Stone_Tomb.prefab", new Vector3(0f, floor + 0.2f, 21.5f), 0f, 1.2f, true, 3f);
		foreach (Vector2 corner in new[] { new Vector2(-1.5f, 19.6f), new Vector2(1.5f, 19.6f), new Vector2(-1.5f, 23.4f), new Vector2(1.5f, 23.4f) })
		{
			GameObject candle = PlaceProp(room, Dungeon + "Misc/Light/NGF_Light_Candle_large_02.prefab", new Vector3(corner.x, floor + 0.2f, corner.y), 0f, 0.5f, false);
			AddCandleLight(room, candle, 1.5f, 5f);
		}
		CreatePlacard(room, new Vector3(0f, floor, 18.6f), 180f,
			"Exhibit V - The Morvayne Tomb",
			"Carved limestone, fifteenth century. The founder of the line is said to rest here.\n\n" +
			"The case is sealed for the visitor's protection.\n\n" +
			"It does not open from this side.");

		// coffins lying along the side walls
		foreach (float z in new[] { 18.5f, 23.5f })
		{
			PlaceProp(room, Coffins + "Coffins/Coffin_Wood_B_1.prefab", new Vector3(-5f, floor, z), 0f, 0.7f, true, 2.2f);
			PlaceProp(room, Coffins + "Coffins/Coffin_Wood_B_1.prefab", new Vector3(5f, floor, z), 0f, 0.7f, true, 2.2f);
		}

		// the curator's desk and journal, by the door
		GameObject desk = PlaceProp(room, Dungeon + "Misc/Furniture/NGF_Table_01.prefab", new Vector3(4.6f, floor, 15.6f), 0f, 0.85f);
		float deskTop = desk != null ? WorldBounds(desk).max.y : floor + 0.85f;
		GameObject book = PlaceProp(room, Dungeon + "Misc/Item/Books/NGF_Book_open_01.prefab", new Vector3(4.6f, deskTop, 15.6f), -20f, 0f, false);
		PlaceProp(room, Dungeon + "Misc/Item/NGF_Inkstand_01.prefab", new Vector3(5.1f, deskTop, 15.9f), 0f, 0f, false);
		GameObject deskCandle = PlaceProp(room, Dungeon + "Misc/Light/NGF_Light_Candle_small_02.prefab", new Vector3(4.1f, deskTop, 15.9f), 0f, 0f, false);
		AddCandleLight(room, deskCandle, 1.2f, 4f);
		AddReadable(room, "Journal", new Vector3(4.6f, deskTop + 0.1f, 15.6f), new Vector3(0.7f, 0.3f, 0.6f), book,
			"The Curator's journal - last entry",
			"They ask whether the exhibits are real. I tell them every piece is authentic, and they laugh, and move on to the gift shop.\n\n" +
			"The family is waking early this year. The thralls in the gallery are restless, and the Countess has stopped pretending to be a painting.\n\n" +
			"I have sent for a hunter. If they reach the Archive, the truth is in the ledgers. I have hidden its key where the family will never look: among their own dead.\n\n" +
			"God forgive me for what this house will do to them.",
			"Read journal");

		GameObject hangingLamp = PlaceHanging(room, Dungeon + "Misc/Light/NGF_Light_Hanging_lamp.prefab", new Vector3(0f, top, 16.5f), 0f, 1.2f);
		if (hangingLamp != null)
		{
			Bounds lampBounds = WorldBounds(hangingLamp);
			AddPointLight(room, "HangingLampLight", new Vector3(0f, lampBounds.min.y + 0.15f, 16.5f), CandleColor, 4f, 8f, false, true);
		}

		PlaceHanging(room, Cobwebs + "SM_Cobwebs_04.prefab", new Vector3(-5.6f, top, 25.6f), 0f, 1.2f);
		PlaceHanging(room, Cobwebs + "SM_Cobwebs_06.prefab", new Vector3(5.6f, top, 25.6f), 90f, 1.2f);
		PlaceHanging(room, Cobwebs + "SM_Cobwebs_08.prefab", new Vector3(5.6f, top, 15f), 180f, 1.2f);

		// stepping past the desk wakes two more thralls from behind the tomb, and the wing door slams
		CreateEncounter(room, "WingEncounter", new Vector3(0f, floor + 1.5f, 17.5f), new Vector3(12f, 3f, 1.5f), enemyPrefab,
			new[] { new Vector3(-3.5f, floor, 24.5f), new Vector3(3.5f, floor, 24.5f) }, wingDoor,
			"Something stirs behind the tomb.", "The wing is quiet again.", null);

		PlacePickup(room, PrototypeArenaBuilder.AmmoPickupPrefabPath, new Vector3(-4.8f, floor, 15.5f));
		PlacePickup(room, PrototypeArenaBuilder.HealthPickupPrefabPath, new Vector3(-4.8f, floor, 21f));
	}

	// ---------------------------------------------------------------- interactive pieces

	// a doorway's door: root at the bottom centre of the opening, facing along `yaw`; the hinge on its left
	// edge turns a leaf that carries the collider, a navmesh obstacle and the Door model (or a plank)
	private static Door CreateDoor(Transform parent, string name, Vector3 bottomCentre, float yaw, float width, float height)
	{
		var root = new GameObject(name);
		root.transform.SetParent(parent, false);
		root.transform.SetPositionAndRotation(bottomCentre, Quaternion.Euler(0f, yaw, 0f));

		// doors move, so they stay out of the bake and carve the navmesh instead
		root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

		var audio = root.AddComponent<AudioSource>();
		audio.playOnAwake = false;
		audio.spatialBlend = 1f;
		audio.minDistance = 2f;
		audio.maxDistance = 30f;

		var hinge = new GameObject("Hinge").transform;
		hinge.SetParent(root.transform, false);
		hinge.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

		var leaf = new GameObject("Leaf").transform;
		leaf.SetParent(hinge, false);
		leaf.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);
		const float thickness = 0.12f;
		leaf.gameObject.AddComponent<BoxCollider>().size = new Vector3(width, height, thickness);
		var obstacle = leaf.gameObject.AddComponent<NavMeshObstacle>();
		obstacle.shape = NavMeshObstacleShape.Box;
		obstacle.size = new Vector3(width, height, thickness * 2f);
		obstacle.carving = true;

		if (!FitModel(leaf, Pegasus + "Door.prefab", width, height))
		{
			CreateBox(leaf, "Plank", leaf.position, new Vector3(width, height, thickness), _wood, 1f, false);
		}

		SetStatic(root, false);
		var door = root.AddComponent<Door>();
		door.Hinge = hinge;
		door.OpenClip = PrototypeArenaBuilder.LoadClip(DoorOpenClip);
		door.SlamClip = PrototypeArenaBuilder.LoadClip(DoorSlamClip);
		door.LockedClip = PrototypeArenaBuilder.LoadClip(DoorLockedClip);
		return door;
	}

	// Copies a model under `parent`, turned so its wider horizontal side runs along the parent's X, and
	// stretched to exactly width x height, centred. Returns false if the model is missing
	private static bool FitModel(Transform parent, string path, float width, float height)
	{
		var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (source == null)
		{
			Debug.LogWarning("[MuseumBuilder] Model not found at " + path + ", using a plain box.");
			return false;
		}

		GameObject model = CopyModel(source, parent);
		model.transform.localPosition = Vector3.zero;

		Bounds bounds = LocalBounds(model, parent);
		if (bounds.size.z > bounds.size.x)
		{
			model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
			bounds = LocalBounds(model, parent);
		}
		if (bounds.size.x <= 0f || bounds.size.y <= 0f) return true;

		// stretch in the parent's axes via an intermediate holder, so the model's own rotation is kept
		var holder = new GameObject("Model").transform;
		holder.SetParent(parent, false);
		model.transform.SetParent(holder, true);
		float depthScale = height / bounds.size.y;
		holder.localScale = new Vector3(width / bounds.size.x, height / bounds.size.y, depthScale);
		holder.localPosition = -Vector3.Scale(bounds.center, holder.localScale);
		return true;
	}

	// a lectern-style placard: a dark wood stand with a sloped marble plate, readable from either side
	private static void CreatePlacard(Transform parent, Vector3 floor, float yaw, string title, string body)
	{
		var placard = new GameObject("Placard").transform;
		placard.SetParent(parent, false);
		placard.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));

		CreateBox(placard, "Stand", floor + Vector3.up * 0.5f, new Vector3(0.12f, 1f, 0.12f), _wood, 1f);
		GameObject plate = CreateBox(placard, "Plate", floor + Vector3.up * 1.05f, new Vector3(0.55f, 0.04f, 0.4f), _marble, 1f);
		plate.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);

		var readable = placard.gameObject.AddComponent<Readable>();
		readable.Title = title;
		readable.Body = body;
		readable.ReadClip = _pageClip;
	}

	// a readable trigger box around a prop (a letter, a book) that has no collider of its own
	private static void AddReadable(Transform parent, string name, Vector3 centre, Vector3 size, GameObject prop, string title, string body, string verb = "Read")
	{
		var go = new GameObject(name);
		go.layer = IgnoreRaycastLayer;
		go.transform.SetParent(parent, false);
		go.transform.position = centre;
		var trigger = go.AddComponent<BoxCollider>();
		trigger.isTrigger = true;
		trigger.size = size;
		if (prop != null) prop.transform.SetParent(go.transform, true);

		var readable = go.AddComponent<Readable>();
		readable.Title = title;
		readable.Body = body;
		readable.ReadClip = _pageClip;
		readable.Verb = verb;
	}

	// inactive until an Encounter reveals it: the rust key, bobbing and turning, with a warm glow
	private static GameObject CreateKeyPickup(Transform parent, KeyData key, Vector3 position)
	{
		var root = new GameObject("KeyPickup_" + key.name);
		root.layer = IgnoreRaycastLayer;
		root.transform.SetParent(parent, false);
		root.transform.position = position;
		var trigger = root.AddComponent<SphereCollider>();
		trigger.isTrigger = true;
		trigger.center = new Vector3(0f, 0.5f, 0f);
		trigger.radius = 0.4f;

		var visual = new GameObject("Visual").transform;
		visual.gameObject.layer = IgnoreRaycastLayer;
		visual.SetParent(root.transform, false);
		visual.localPosition = new Vector3(0f, 0.5f, 0f);

		var source = AssetDatabase.LoadAssetAtPath<GameObject>(KeyModelPath);
		if (source != null)
		{
			GameObject model = CopyModel(source, visual);
			model.transform.localPosition = Vector3.zero;
			Bounds bounds = LocalBounds(model, visual);
			float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
			float scale = longest > 0f ? 0.3f / longest : 1f;
			model.transform.localScale *= scale;
			model.transform.localPosition = -bounds.center * scale;
			foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;

			var keyMaterial = AssetDatabase.LoadAssetAtPath<Material>(KeyMaterialPath);
			if (keyMaterial != null)
			{
				foreach (Renderer keyRenderer in model.GetComponentsInChildren<Renderer>(true))
				{
					var materials = new Material[keyRenderer.sharedMaterials.Length];
					for (int i = 0; i < materials.Length; i++) materials[i] = keyMaterial;
					keyRenderer.sharedMaterials = materials;
				}
			}
			else
			{
				Debug.LogWarning("[MuseumBuilder] Key material not found at " + KeyMaterialPath + ", the key may render pink.");
			}
		}
		else
		{
			Debug.LogWarning("[MuseumBuilder] Key model not found at " + KeyModelPath + ", the key will only show its glow.");
		}

		var glow = new GameObject("Glow").AddComponent<Light>();
		glow.gameObject.layer = IgnoreRaycastLayer;
		glow.transform.SetParent(visual, false);
		glow.type = LightType.Point;
		glow.color = new Color(1f, 0.8f, 0.4f);
		glow.intensity = 1.5f;
		glow.range = 2f;
		glow.shadows = LightShadows.None;

		SetStatic(root, false);
		var pickup = root.AddComponent<KeyPickup>();
		pickup.Key = key;
		pickup.Visual = visual;
		pickup.PickupClip = PrototypeArenaBuilder.LoadClip(KeyClip);
		root.SetActive(false);
		return root;
	}

	private static void CreateEncounter(Transform parent, string name, Vector3 centre, Vector3 size, GameObject enemyPrefab,
		Vector3[] spawnPositions, Door barDoor, string startMessage, string clearedMessage, GameObject reward)
	{
		var go = new GameObject(name);
		go.layer = IgnoreRaycastLayer;
		go.transform.SetParent(parent, false);
		go.transform.position = centre;
		var trigger = go.AddComponent<BoxCollider>();
		trigger.isTrigger = true;
		trigger.size = size;
		// kinematic body so the trigger reliably fires against the CharacterController
		var body = go.AddComponent<Rigidbody>();
		body.isKinematic = true;
		body.useGravity = false;

		var points = new Transform[spawnPositions.Length];
		for (int i = 0; i < spawnPositions.Length; i++)
		{
			points[i] = new GameObject($"Spawn_{i}").transform;
			points[i].SetParent(go.transform, false);
			points[i].position = spawnPositions[i];
			Vector3 toTrigger = centre - spawnPositions[i];
			toTrigger.y = 0f;
			points[i].rotation = Quaternion.LookRotation(toTrigger);
		}

		var encounter = go.AddComponent<Encounter>();
		encounter.EnemyPrefab = enemyPrefab;
		encounter.SpawnPoints = points;
		encounter.BarDoors = barDoor != null ? new[] { barDoor } : new Door[0];
		encounter.StartMessage = startMessage;
		encounter.ClearedMessage = clearedMessage;
		encounter.Reward = reward;
	}

	private static void PlacePickup(Transform parent, string prefabPath, Vector3 position)
	{
		var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
		if (prefab == null) return;
		var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
		instance.transform.position = position;
	}

	// ---------------------------------------------------------------- lights

	// a flickering candle light just above the top of a prop
	private static void AddCandleLight(Transform parent, GameObject prop, float intensity, float range)
	{
		if (prop == null) return;
		Bounds bounds = WorldBounds(prop);
		AddPointLight(parent, "CandleLight", new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z), CandleColor, intensity, range, false, true);
	}

	private static void AddPointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range, bool shadows, bool flicker)
	{
		var light = new GameObject(name).AddComponent<Light>();
		light.transform.SetParent(parent, false);
		light.transform.position = position;
		light.type = LightType.Point;
		light.color = color;
		light.intensity = intensity;
		light.range = range;
		light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
		// a shadowed point light takes 6 maps; at Medium (512) both chandeliers' 12 fit the 2048 atlas
		// (the tier is read-only from code, so it's set through the serialized field)
		if (shadows)
		{
			var lightData = new SerializedObject(light.GetUniversalAdditionalLightData());
			lightData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue =
				UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
			lightData.ApplyModifiedPropertiesWithoutUndo();
		}
		if (flicker) light.gameObject.AddComponent<LightFlicker>();
	}

	// ---------------------------------------------------------------- props

	// A copy of a prop, turned by `yaw`, scaled uniformly so it's `height` m tall (or so its longest side is
	// `length` m, or left at native size when both are 0), standing with its bounds' bottom centre on `floor`.
	// Its own colliders are replaced by one box around it unless `collide` is false. With a `length`, the
	// prop is also turned so its long side runs along Z (coffins on their plinths)
	private static GameObject PlaceProp(Transform parent, string path, Vector3 floor, float yaw, float height, bool collide = true, float length = 0f)
	{
		var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (source == null)
		{
			Debug.LogWarning("[MuseumBuilder] Prop not found at " + path + ", skipping it.");
			return null;
		}

		GameObject prop = CopyModel(source, parent);
		prop.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f) * source.transform.rotation);

		Bounds bounds = WorldBounds(prop);
		if (length > 0f && bounds.size.x > bounds.size.z)
		{
			prop.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * prop.transform.rotation;
			bounds = WorldBounds(prop);
		}
		float scale = 1f;
		if (length > 0f) scale = length / Mathf.Max(bounds.size.x, bounds.size.z, 0.001f);
		else if (height > 0f) scale = height / Mathf.Max(bounds.size.y, 0.001f);
		prop.transform.localScale *= scale;

		bounds = WorldBounds(prop);
		prop.transform.position += floor - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
		if (collide) AddBoundsCollider(prop);
		return prop;
	}

	// like PlaceProp, but hung with the top of its bounds at `ceiling`, and never solid
	private static GameObject PlaceHanging(Transform parent, string path, Vector3 ceiling, float yaw, float height)
	{
		GameObject prop = PlaceProp(parent, path, ceiling, yaw, height, false);
		if (prop == null) return null;
		prop.transform.position += Vector3.down * WorldBounds(prop).size.y;
		return prop;
	}

	// A flat prop (painting, frame) hung on a wall: its thin side is turned to face `normal` (into the room),
	// it's scaled to `height` m tall, centred on `centre` and pulled flush against the wall. Which flat face
	// is the front is guessed from the mesh: the back of a frame is a plain panel, so most of the vertices
	// (the moulding, the canvas) sit towards the front
	private static GameObject PlaceOnWall(Transform parent, string path, Vector3 centre, Vector3 normal, float height)
	{
		var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (source == null)
		{
			Debug.LogWarning("[MuseumBuilder] Prop not found at " + path + ", skipping it.");
			return null;
		}

		GameObject prop = CopyModel(source, parent);
		prop.transform.SetPositionAndRotation(Vector3.zero, source.transform.rotation);

		// in world space at the prop's rest rotation: which horizontal axis is thin, and which way is its front
		Bounds bounds = WorldBounds(prop);
		Vector3 centroid = VertexCentroid(prop);
		Vector3 front = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
		if (Vector3.Dot(centroid - bounds.center, front) < 0f) front = -front;
		prop.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(front, normal, Vector3.up), Vector3.up) * prop.transform.rotation;

		bounds = WorldBounds(prop);
		prop.transform.localScale *= height / Mathf.Max(bounds.size.y, 0.001f);
		bounds = WorldBounds(prop);

		// centre on the point, then slide out along the normal until the back touches the wall
		Vector3 offset = centre - bounds.center;
		float backDistance = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)));
		offset += normal * (backDistance + 0.01f);
		prop.transform.position += offset;
		return prop;
	}

	// a plain copy rather than a prefab instance, so the builder owns it; colliders and rigidbodies stripped
	private static GameObject CopyModel(GameObject source, Transform parent)
	{
		GameObject copy = Object.Instantiate(source, parent);
		copy.name = source.name;
		foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
		foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
		foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
		return copy;
	}

	private static void SetStatic(GameObject go, bool value)
	{
		foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = value;
	}

	private static void AddBoundsCollider(GameObject prop)
	{
		Bounds local = LocalBounds(prop, prop.transform);
		var box = prop.AddComponent<BoxCollider>();
		box.center = local.center;
		box.size = local.size;
	}

	// mesh bounds in world space, from every mesh corner (renderer bounds can be stale in edit mode)
	private static Bounds WorldBounds(GameObject go) => MeshBounds(go, null);

	// mesh bounds in `space`'s local coordinates
	private static Bounds LocalBounds(GameObject go, Transform space) => MeshBounds(go, space);

	private static Bounds MeshBounds(GameObject go, Transform space)
	{
		Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
		bool first = true;
		foreach (MeshFilter meshFilter in go.GetComponentsInChildren<MeshFilter>(true))
		{
			if (meshFilter.sharedMesh == null) continue;
			Bounds meshBounds = meshFilter.sharedMesh.bounds;
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
					new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
				Vector3 point = meshFilter.transform.TransformPoint(corner);
				if (space != null) point = space.InverseTransformPoint(point);
				if (first) bounds = new Bounds(point, Vector3.zero);
				else bounds.Encapsulate(point);
				first = false;
			}
		}
		return bounds;
	}

	private static Vector3 VertexCentroid(GameObject go)
	{
		Vector3 sum = Vector3.zero;
		int count = 0;
		foreach (MeshFilter meshFilter in go.GetComponentsInChildren<MeshFilter>(true))
		{
			if (meshFilter.sharedMesh == null) continue;
			foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
			{
				sum += meshFilter.transform.TransformPoint(vertex);
				count++;
			}
		}
		return count > 0 ? sum / count : go.transform.position;
	}

	// ---------------------------------------------------------------- geometry

	private static Transform Group(Transform parent, string name)
	{
		var group = new GameObject(name).transform;
		group.SetParent(parent, false);
		return group;
	}

	// a gap in a wall: centred `along` metres from the wall's middle, `bottom` and `top` above the wall's base
	private struct Opening
	{
		public float Along, Width, Bottom, Top;
		public Opening(float along, float width, float bottom, float top)
		{
			Along = along;
			Width = width;
			Bottom = bottom;
			Top = top;
		}
	}

	// a wall running along X at z, from x0 to x1
	private static void BuildWallX(Transform parent, string name, float z, float x0, float x1, float baseY, float height, Material material, params Opening[] openings)
	{
		BuildWall(parent, name, (along, y, length, h) => CreateBox(parent, name, new Vector3((x0 + x1) * 0.5f + along, y, z), new Vector3(length, h, Wall), material, 2f),
			x1 - x0, baseY, height, openings);
	}

	// a wall running along Z at x, from z0 to z1
	private static void BuildWallZ(Transform parent, string name, float x, float z0, float z1, float baseY, float height, Material material, params Opening[] openings)
	{
		BuildWall(parent, name, (along, y, length, h) => CreateBox(parent, name, new Vector3(x, y, (z0 + z1) * 0.5f + along), new Vector3(Wall, h, length), material, 2f),
			z1 - z0, baseY, height, openings);
	}

	// Splits a wall into boxes around its openings: full-height pieces between them, and the parts below and
	// above each one. `box` makes a piece from its centre along the wall, centre height, length and height
	private static void BuildWall(Transform parent, string name, System.Action<float, float, float, float> box, float length, float baseY, float height, Opening[] openings)
	{
		var sorted = new List<Opening>(openings);
		sorted.Sort((a, b) => a.Along.CompareTo(b.Along));

		float half = length * 0.5f;
		float cursor = -half;
		foreach (Opening opening in sorted)
		{
			float start = opening.Along - opening.Width * 0.5f;
			float end = opening.Along + opening.Width * 0.5f;
			if (start > cursor) box((cursor + start) * 0.5f, baseY + height * 0.5f, start - cursor, height);
			if (opening.Bottom > 0f) box(opening.Along, baseY + opening.Bottom * 0.5f, opening.Width, opening.Bottom);
			if (opening.Top < height) box(opening.Along, baseY + (opening.Top + height) * 0.5f, opening.Width, height - opening.Top);
			cursor = end;
		}
		if (cursor < half) box((cursor + half) * 0.5f, baseY + height * 0.5f, half - cursor, height);
	}

	// An axis-aligned box whose UVs come from world position (one texture repeat per `tile` metres), so
	// the texture tiles evenly and lines up across neighbouring boxes. The mesh is saved in the scene
	private static GameObject CreateBox(Transform parent, string name, Vector3 centre, Vector3 size, Material material, float tile, bool collide = true)
	{
		var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
		go.isStatic = true;
		go.transform.SetParent(parent, false);
		go.transform.position = centre;
		go.GetComponent<MeshFilter>().sharedMesh = CreateBoxMesh(name, centre, size, tile);
		go.GetComponent<MeshRenderer>().sharedMaterial = material;
		if (collide) go.AddComponent<BoxCollider>().size = size;
		return go;
	}

	// faces as (normal, t1, t2) with t1 x t2 = normal, so each quad winds clockwise seen from outside
	private static readonly Vector3[,] Faces =
	{
		{ Vector3.right, Vector3.up, Vector3.forward },
		{ Vector3.left, Vector3.forward, Vector3.up },
		{ Vector3.up, Vector3.forward, Vector3.right },
		{ Vector3.down, Vector3.right, Vector3.forward },
		{ Vector3.forward, Vector3.right, Vector3.up },
		{ Vector3.back, Vector3.up, Vector3.right },
	};

	private static Mesh CreateBoxMesh(string name, Vector3 centre, Vector3 size, float tile)
	{
		Vector3 extents = size * 0.5f;
		var vertices = new Vector3[24];
		var normals = new Vector3[24];
		var uvs = new Vector2[24];
		var triangles = new int[36];

		for (int face = 0; face < 6; face++)
		{
			Vector3 normal = Faces[face, 0], t1 = Faces[face, 1], t2 = Faces[face, 2];
			Vector3 faceCentre = Vector3.Scale(normal, extents);
			Vector3 a = Vector3.Scale(t1, extents), b = Vector3.Scale(t2, extents);
			Vector3[] corners = { faceCentre - a - b, faceCentre - a + b, faceCentre + a + b, faceCentre + a - b };

			for (int i = 0; i < 4; i++)
			{
				int v = face * 4 + i;
				vertices[v] = corners[i];
				normals[v] = normal;
				// planar projection in world space, dropping the axis the face looks along
				Vector3 world = centre + corners[i];
				uvs[v] = (normal.x != 0f ? new Vector2(world.z, world.y)
					: normal.y != 0f ? new Vector2(world.x, world.z)
					: new Vector2(world.x, world.y)) / tile;
			}

			int t = face * 6, f = face * 4;
			triangles[t] = f; triangles[t + 1] = f + 2; triangles[t + 2] = f + 1;
			triangles[t + 3] = f; triangles[t + 4] = f + 3; triangles[t + 5] = f + 2;
		}

		var mesh = new Mesh { name = name };
		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uvs;
		mesh.triangles = triangles;
		mesh.RecalculateTangents();
		mesh.RecalculateBounds();
		return mesh;
	}
}
