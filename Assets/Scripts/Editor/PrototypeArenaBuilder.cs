using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Builds the Phase 1 combat prototype into SampleScene: layers, data assets, the cube enemy prefab,
// player combat components, the arena geometry, NavMesh, enemy spawner and a Cinemachine 3 camera rig.
// Safe to re-run: the arena root is rebuilt, existing assets are reused rather than overwritten.
public static class PrototypeArenaBuilder
{
	private const string ScenePath = "Assets/Scenes/SampleScene.unity";
	private const string PlayerPrefabPath = "Assets/StarterAssets/FirstPersonController/Prefabs/PlayerCapsule.prefab";
	private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/VampireThrall_Proto.prefab";
	private const string RevolverDataPath = "Assets/Data/Weapons/Revolver.asset";
	private const string MeleeDataPath = "Assets/Data/Weapons/RelicBlade.asset";
	private const string EnemyDataPath = "Assets/Data/Enemies/VampireThrall_Proto.asset";
	private const string MaterialFolder = "Assets/Materials/Proto";

	private const string ArenaRootName = "PrototypeArena";
	private const string FollowCameraName = "PlayerFollowCamera";
	private const int PlayerLayer = 8;
	private const int EnemyLayer = 9;

	private const string LitShader = "Universal Render Pipeline/Lit";
	private const string FxShader = "Universal Render Pipeline/Particles/Unlit";

	[MenuItem("Tools/Crimson Nights/Build Prototype Arena")]
	public static void Build()
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode)
		{
			Debug.LogError("[PrototypeArenaBuilder] Exit Play Mode first.");
			return;
		}

		var scene = EditorSceneManager.GetActiveScene();
		if (scene.path != ScenePath)
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
			scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
		}

		SetupLayers();

		Material fxMat = GetOrCreateMaterial("FX_Unlit", FxShader, Color.white);
		Material enemyMat = GetOrCreateMaterial("Enemy_Thrall", LitShader, Color.white);
		Material floorMat = GetOrCreateMaterial("Arena_Floor", LitShader, new Color(0.32f, 0.3f, 0.3f));
		Material wallMat = GetOrCreateMaterial("Arena_Wall", LitShader, new Color(0.2f, 0.18f, 0.2f));
		Material coverMat = GetOrCreateMaterial("Arena_Cover", LitShader, new Color(0.45f, 0.4f, 0.36f));
		Material revolverMat = GetOrCreateMaterial("Viewmodel_Revolver", LitShader, new Color(0.15f, 0.15f, 0.17f));
		Material bladeMat = GetOrCreateMaterial("Viewmodel_Blade", LitShader, new Color(0.75f, 0.78f, 0.85f));

		RevolverData revolverData = GetOrCreateAsset<RevolverData>(RevolverDataPath, out bool revolverCreated);
		if (revolverCreated) revolverData.HitMask = ~(1 << PlayerLayer);
		if (revolverData.FxMaterial == null) revolverData.FxMaterial = fxMat;
		EditorUtility.SetDirty(revolverData);

		MeleeData meleeData = GetOrCreateAsset<MeleeData>(MeleeDataPath, out bool meleeCreated);
		if (meleeCreated) meleeData.HitMask = 1 << EnemyLayer;
		EditorUtility.SetDirty(meleeData);

		EnemyData enemyData = GetOrCreateAsset<EnemyData>(EnemyDataPath, out _);

		GameObject enemyPrefab = GetOrCreateEnemyPrefab(enemyData, enemyMat);
		SetupPlayerPrefab(revolverData, meleeData, revolverMat, bladeMat);
		AssetDatabase.SaveAssets();

		Transform cameraRoot = BuildScene(enemyPrefab, floorMat, wallMat, coverMat);
		SetupCameras(cameraRoot);

		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		Debug.Log("[PrototypeArenaBuilder] Prototype arena built into " + ScenePath);
	}

	// ---------------------------------------------------------------- project setup

	private static void SetupLayers()
	{
		var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
		SerializedProperty layers = tagManager.FindProperty("layers");
		SetLayerName(layers, PlayerLayer, "Player");
		SetLayerName(layers, EnemyLayer, "Enemy");
		tagManager.ApplyModifiedProperties();
	}

	private static void SetLayerName(SerializedProperty layers, int index, string name)
	{
		SerializedProperty layer = layers.GetArrayElementAtIndex(index);
		if (string.IsNullOrEmpty(layer.stringValue))
		{
			layer.stringValue = name;
		}
		else if (layer.stringValue != name)
		{
			Debug.LogWarning($"[PrototypeArenaBuilder] Layer {index} is already named '{layer.stringValue}', expected '{name}'.");
		}
	}

	private static Material GetOrCreateMaterial(string name, string shaderName, Color color)
	{
		string path = $"{MaterialFolder}/{name}.mat";
		Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
		if (material != null) return material;

		EnsureFolder(MaterialFolder);
		material = new Material(Shader.Find(shaderName));
		material.SetColor("_BaseColor", color);
		AssetDatabase.CreateAsset(material, path);
		return material;
	}

	private static T GetOrCreateAsset<T>(string path, out bool created) where T : ScriptableObject
	{
		T asset = AssetDatabase.LoadAssetAtPath<T>(path);
		created = asset == null;
		if (!created) return asset;

		EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
		asset = ScriptableObject.CreateInstance<T>();
		AssetDatabase.CreateAsset(asset, path);
		return asset;
	}

	private static void EnsureFolder(string folder)
	{
		if (AssetDatabase.IsValidFolder(folder)) return;
		string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
		EnsureFolder(parent);
		AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
	}

	// ---------------------------------------------------------------- prefabs

	private static GameObject GetOrCreateEnemyPrefab(EnemyData data, Material material)
	{
		GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
		if (existing != null) return existing;

		EnsureFolder(System.IO.Path.GetDirectoryName(EnemyPrefabPath).Replace('\\', '/'));

		var root = new GameObject("VampireThrall_Proto");
		root.layer = EnemyLayer;

		GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
		body.name = "Body";
		body.layer = EnemyLayer;
		body.transform.SetParent(root.transform, false);
		body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
		body.transform.localScale = new Vector3(1f, 1.8f, 1f);
		body.GetComponent<MeshRenderer>().sharedMaterial = material;

		// EnemyController's RequireComponent adds the NavMeshAgent and Health
		var controller = root.AddComponent<EnemyController>();
		controller.Data = data;
		controller.Body = body.GetComponent<MeshRenderer>();

		var agent = root.GetComponent<NavMeshAgent>();
		agent.radius = 0.5f;
		agent.height = 1.8f;
		agent.baseOffset = 0f;
		agent.angularSpeed = 720f;
		agent.speed = data.MoveSpeed;

		GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
		Object.DestroyImmediate(root);
		return prefab;
	}

	private static void SetupPlayerPrefab(RevolverData revolverData, MeleeData meleeData, Material revolverMat, Material bladeMat)
	{
		GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
		try
		{
			GetOrAdd<Health>(root).MaxHealth = 100f;
			GetOrAdd<PlayerDeath>(root);

			Transform cameraRoot = root.transform.Find("PlayerCameraRoot");

			// revolver viewmodel: pivot -> body cube + muzzle point
			Transform revolverPivot = GetOrCreateChild(cameraRoot, "RevolverViewmodel", new Vector3(0.25f, -0.22f, 0.45f));
			if (revolverPivot.Find("Body") == null)
			{
				CreateViewmodelCube(revolverPivot, "Body", Vector3.zero, new Vector3(0.07f, 0.12f, 0.3f), revolverMat);
				CreateViewmodelCube(revolverPivot, "Grip", new Vector3(0f, -0.1f, -0.1f), new Vector3(0.06f, 0.14f, 0.07f), revolverMat);
			}
			Transform muzzle = GetOrCreateChild(revolverPivot, "Muzzle", new Vector3(0f, 0.02f, 0.17f));

			// blade viewmodel: pivot at the hilt, blade extends along +Z
			Transform bladePivot = GetOrCreateChild(cameraRoot, "BladeViewmodel", new Vector3(-0.3f, -0.3f, 0.4f));
			if (bladePivot.Find("Blade") == null)
			{
				bladePivot.localRotation = Quaternion.Euler(-55f, 15f, 0f);
				CreateViewmodelCube(bladePivot, "Blade", new Vector3(0f, 0f, 0.45f), new Vector3(0.03f, 0.06f, 0.8f), bladeMat);
				CreateViewmodelCube(bladePivot, "Guard", new Vector3(0f, 0f, 0.04f), new Vector3(0.2f, 0.04f, 0.04f), revolverMat);
			}

			Revolver revolver = GetOrAdd<Revolver>(root);
			revolver.Data = revolverData;
			revolver.Viewmodel = revolverPivot;
			revolver.Muzzle = muzzle;

			MeleeWeapon melee = GetOrAdd<MeleeWeapon>(root);
			melee.Data = meleeData;
			melee.Blade = bladePivot;

			GetOrAdd<PlayerHUD>(root);

			PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	private static void CreateViewmodelCube(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
	{
		GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		cube.name = name;
		Object.DestroyImmediate(cube.GetComponent<Collider>());
		cube.layer = PlayerLayer;
		cube.transform.SetParent(parent, false);
		cube.transform.localPosition = localPosition;
		cube.transform.localScale = scale;
		var meshRenderer = cube.GetComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = material;
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
	}

	// ---------------------------------------------------------------- scene

	private static Transform BuildScene(GameObject enemyPrefab, Material floorMat, Material wallMat, Material coverMat)
	{
		// clear anything a previous run created
		GameObject oldArena = GameObject.Find(ArenaRootName);
		if (oldArena != null) Object.DestroyImmediate(oldArena);
		foreach (var fpc in Object.FindObjectsByType<StarterAssets.FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			Object.DestroyImmediate(fpc.gameObject);
		}

		// the sample Plane would z-fight with the arena floor; disable rather than delete it
		GameObject samplePlane = GameObject.Find("Plane");
		if (samplePlane != null && samplePlane.transform.parent == null)
		{
			samplePlane.SetActive(false);
			Debug.Log("[PrototypeArenaBuilder] Disabled the sample 'Plane' (replaced by the arena floor).");
		}

		var arena = new GameObject(ArenaRootName);
		Transform geometry = new GameObject("Geometry").transform;
		geometry.SetParent(arena.transform, false);

		// floor and perimeter walls (40 x 40, 4 m high)
		CreateBlock(geometry, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), floorMat);
		CreateBlock(geometry, "Wall_North", new Vector3(0f, 2f, 20.5f), new Vector3(42f, 4f, 1f), wallMat);
		CreateBlock(geometry, "Wall_South", new Vector3(0f, 2f, -20.5f), new Vector3(42f, 4f, 1f), wallMat);
		CreateBlock(geometry, "Wall_East", new Vector3(20.5f, 2f, 0f), new Vector3(1f, 4f, 40f), wallMat);
		CreateBlock(geometry, "Wall_West", new Vector3(-20.5f, 2f, 0f), new Vector3(1f, 4f, 40f), wallMat);

		// pillars
		CreateBlock(geometry, "Pillar_NE", new Vector3(6f, 2f, 6f), new Vector3(1.5f, 4f, 1.5f), coverMat);
		CreateBlock(geometry, "Pillar_NW", new Vector3(-6f, 2f, 6f), new Vector3(1.5f, 4f, 1.5f), coverMat);
		CreateBlock(geometry, "Pillar_SE", new Vector3(6f, 2f, -6f), new Vector3(1.5f, 4f, 1.5f), coverMat);
		CreateBlock(geometry, "Pillar_SW", new Vector3(-6f, 2f, -6f), new Vector3(1.5f, 4f, 1.5f), coverMat);

		// low cover
		CreateBlock(geometry, "Cover_N", new Vector3(0f, 0.6f, 10f), new Vector3(4f, 1.2f, 1f), coverMat);
		CreateBlock(geometry, "Cover_S", new Vector3(0f, 0.6f, -9f), new Vector3(4f, 1.2f, 1f), coverMat);
		CreateBlock(geometry, "Cover_E", new Vector3(12f, 0.6f, 0f), new Vector3(1f, 1.2f, 4f), coverMat);
		CreateBlock(geometry, "Cover_NE", new Vector3(13f, 0.6f, 13f), new Vector3(3f, 1.2f, 1f), coverMat, 45f);

		// raised platform with a ramp (top at 1.5 m)
		CreateBlock(geometry, "Platform", new Vector3(-12f, 0.75f, -12f), new Vector3(8f, 1.5f, 6f), coverMat);
		GameObject ramp = CreateBlock(geometry, "Ramp", new Vector3(-12f, 0.62f, -6.95f), new Vector3(3f, 0.3f, 4.6f), coverMat);
		ramp.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

		// navmesh, baked at runtime from the arena's colliders
		var surface = arena.AddComponent<NavMeshSurface>();
		surface.collectObjects = CollectObjects.Children;
		surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
		surface.layerMask = 1 << 0; // Default only
		arena.AddComponent<RuntimeNavMeshBaker>();

		// enemy spawn points and spawner
		Transform spawnRoot = new GameObject("SpawnPoints").transform;
		spawnRoot.SetParent(arena.transform, false);
		Vector3[] spawnPositions =
		{
			new Vector3(-15f, 0f, 15f),
			new Vector3(0f, 0f, 16f),
			new Vector3(15f, 0f, 15f),
			new Vector3(16f, 0f, 0f),
			new Vector3(-16f, 0f, 2f),
		};
		var spawnPoints = new Transform[spawnPositions.Length];
		for (int i = 0; i < spawnPositions.Length; i++)
		{
			spawnPoints[i] = new GameObject($"Spawn_{i}").transform;
			spawnPoints[i].SetParent(spawnRoot, false);
			spawnPoints[i].position = spawnPositions[i];
			spawnPoints[i].rotation = Quaternion.LookRotation(-spawnPositions[i].normalized);
		}

		var spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
		spawner.transform.SetParent(arena.transform, false);
		spawner.EnemyPrefab = enemyPrefab;
		spawner.SpawnPoints = spawnPoints;

		// player
		var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
		var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
		player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -16f), Quaternion.identity);

		return player.transform.Find("PlayerCameraRoot");
	}

	private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material, float yaw = 0f)
	{
		GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
		block.name = name;
		block.isStatic = true;
		block.transform.SetParent(parent, false);
		block.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
		block.transform.localScale = scale;
		block.GetComponent<MeshRenderer>().sharedMaterial = material;
		return block;
	}

	private static void SetupCameras(Transform cameraRoot)
	{
		Camera mainCamera = Camera.main;
		if (mainCamera == null)
		{
			var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
			go.tag = "MainCamera";
			mainCamera = go.GetComponent<Camera>();
		}
		GetOrAdd<CinemachineBrain>(mainCamera.gameObject);

		GameObject oldFollow = GameObject.Find(FollowCameraName);
		if (oldFollow != null) Object.DestroyImmediate(oldFollow);

		var followGO = new GameObject(FollowCameraName);
		var cmCamera = followGO.AddComponent<CinemachineCamera>();
		cmCamera.Follow = cameraRoot;
		LensSettings lens = cmCamera.Lens;
		lens.FieldOfView = 70f;
		lens.NearClipPlane = 0.05f;
		lens.FarClipPlane = 500f;
		cmCamera.Lens = lens;
		followGO.AddComponent<CinemachineHardLockToTarget>();
		followGO.AddComponent<CinemachineRotateWithFollowTarget>();
		followGO.transform.SetPositionAndRotation(cameraRoot.position, cameraRoot.rotation);
	}

	// ---------------------------------------------------------------- helpers

	private static T GetOrAdd<T>(GameObject go) where T : Component
	{
		T component = go.GetComponent<T>();
		return component != null ? component : go.AddComponent<T>();
	}

	private static Transform GetOrCreateChild(Transform parent, string name, Vector3 localPosition)
	{
		Transform child = parent.Find(name);
		if (child != null) return child;

		child = new GameObject(name).transform;
		child.gameObject.layer = PlayerLayer;
		child.SetParent(parent, false);
		child.localPosition = localPosition;
		return child;
	}
}
