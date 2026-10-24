using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Builds the Phase 1 combat prototype into SampleScene: layers, data assets, the cube enemy prefab,
// health/ammo pickup prefabs, player combat components, the arena geometry, NavMesh, enemy spawner,
// placed pickups and a Cinemachine 3 camera rig.
// Safe to re-run: the arena root is rebuilt, existing assets are reused rather than overwritten.
public static class PrototypeArenaBuilder
{
	private const string ScenePath = "Assets/Scenes/SampleScene.unity";
	private const string PlayerPrefabPath = "Assets/StarterAssets/FirstPersonController/Prefabs/PlayerCapsule.prefab";
	private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/VampireThrall_Proto.prefab";
	private const string HealthPickupPrefabPath = "Assets/Prefabs/Pickups/HealthPickup_Proto.prefab";
	private const string AmmoPickupPrefabPath = "Assets/Prefabs/Pickups/AmmoPickup_Proto.prefab";
	private const string RevolverDataPath = "Assets/Data/Weapons/Revolver.asset";
	private const string ShotgunDataPath = "Assets/Data/Weapons/RelicShotgun.asset";
	private const string MeleeDataPath = "Assets/Data/Weapons/Fists.asset";
	private const string EnemyDataPath = "Assets/Data/Enemies/VampireThrall_Proto.asset";
	private const string DodgeDataPath = "Assets/Data/Player/Dodge.asset";
	private const string StyleDataPath = "Assets/Data/Player/Style.asset";
	// gun models from Low Poly Weapons VOL.1; the cube viewmodels are used if these are missing
	private const string RevolverModelPath = "Assets/Low Poly Weapons VOL.1/Prefabs/M1911.prefab";
	private const string ShotgunModelPath = "Assets/Low Poly Weapons VOL.1/Prefabs/Bennelli_M4.prefab";
	// the pack's guns point down -Z, so they're turned to face the camera's +Z
	private const float GunModelYaw = 180f;
	private const string MaterialFolder = "Assets/Materials/Proto";
	private const string AudioFolder = "Assets/Audio";
	private const string PickupClipName = "Magic Spell_Coins_2.wav";

	private const string ArenaRootName = "PrototypeArena";
	private const string FollowCameraName = "PlayerFollowCamera";
	private const int PlayerLayer = 8;
	private const int EnemyLayer = 9;
	// built-in; keeps pickups out of the NavMesh bake and the revolver's raycasts
	private const int IgnoreRaycastLayer = 2;

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
		Material fistMat = GetOrCreateMaterial("Viewmodel_Fist", LitShader, new Color(0.62f, 0.45f, 0.36f));
		Material woodMat = GetOrCreateMaterial("Viewmodel_Wood", LitShader, new Color(0.3f, 0.16f, 0.08f));

		FirearmData revolverData = GetOrCreateAsset<FirearmData>(RevolverDataPath, out bool revolverCreated);
		if (revolverCreated) revolverData.HitMask = ~(1 << PlayerLayer);
		if (string.IsNullOrEmpty(revolverData.DisplayName)) revolverData.DisplayName = "Revolver";
		if (revolverData.FxMaterial == null) revolverData.FxMaterial = fxMat;
		if (revolverData.ImpactClip == null) revolverData.ImpactClip = LoadClip("ribhavagrawal-bulletshot-impact-sound-effect-230462.mp3");
		if (revolverData.FireClip == null) revolverData.FireClip = LoadClip("freesound_community-single-pistol-gunshot-33-37187.mp3");
		if (revolverData.ReloadClip == null) revolverData.ReloadClip = LoadClip("freesound_community-revolver-cocking-104722.mp3");
		EditorUtility.SetDirty(revolverData);

		FirearmData shotgunData = GetOrCreateAsset<FirearmData>(ShotgunDataPath, out bool shotgunCreated);
		if (shotgunCreated) ApplyShotgunDefaults(shotgunData);
		if (shotgunData.FxMaterial == null) shotgunData.FxMaterial = fxMat;
		if (shotgunData.ImpactClip == null) shotgunData.ImpactClip = LoadClip("finntastico-impact-152508.mp3");
		if (shotgunData.FireClip == null) shotgunData.FireClip = LoadClip("shotgun.mp3");
		if (shotgunData.ReloadClip == null) shotgunData.ReloadClip = LoadClip("freesound_community-realistic-shotgun-cocking-sound-38640.mp3");
		EditorUtility.SetDirty(shotgunData);

		MeleeData meleeData = GetOrCreateAsset<MeleeData>(MeleeDataPath, out bool meleeCreated);
		if (meleeCreated) meleeData.HitMask = 1 << EnemyLayer;
		if (meleeData.HitClip == null) meleeData.HitClip = LoadClip("Indiana Jones Punch.wav");
		if (meleeData.HeavyHitClip == null) meleeData.HeavyHitClip = LoadClip("dragon-studio-hard-punch-sfx-515251.mp3");
		EditorUtility.SetDirty(meleeData);

		EnemyData enemyData = GetOrCreateAsset<EnemyData>(EnemyDataPath, out _);
		if (enemyData.AttackHitClip == null) enemyData.AttackHitClip = LoadClip("Monster Bite.wav");
		EditorUtility.SetDirty(enemyData);

		GameObject enemyPrefab = GetOrCreateEnemyPrefab(enemyData, enemyMat);
		Pickup healthPickup = GetOrCreatePickupPrefab(HealthPickupPrefabPath, Pickup.Kind.Health, 25, new Color(0.9f, 0.1f, 0.15f), fxMat);
		Pickup ammoPickup = GetOrCreatePickupPrefab(AmmoPickupPrefabPath, Pickup.Kind.Ammo, 12, new Color(1f, 0.75f, 0.25f), fxMat);
		DodgeData dodgeData = GetOrCreateAsset<DodgeData>(DodgeDataPath, out _);
		StyleData styleData = GetOrCreateAsset<StyleData>(StyleDataPath, out _);

		SetupPlayerPrefab(revolverData, shotgunData, meleeData, dodgeData, styleData, revolverMat, woodMat, fistMat);
		AssetDatabase.SaveAssets();

		Transform cameraRoot = BuildScene(enemyPrefab, healthPickup, ammoPickup, floorMat, wallMat, coverMat);
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

	// optional: missing clips just leave the slot empty
	private static AudioClip LoadClip(string fileName)
	{
		var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{fileName}");
		if (clip == null) Debug.LogWarning($"[PrototypeArenaBuilder] Audio clip not found: {AudioFolder}/{fileName}");
		return clip;
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

	private static Pickup GetOrCreatePickupPrefab(string path, Pickup.Kind kind, int amount, Color color, Material material)
	{
		GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (existing != null)
		{
			Pickup existingPickup = existing.GetComponent<Pickup>();
			if (existingPickup.PickupClip == null)
			{
				existingPickup.PickupClip = LoadClip(PickupClipName);
				EditorUtility.SetDirty(existingPickup);
			}
			return existingPickup;
		}

		EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));

		// root sits on the floor with a generous trigger; the glowing sphere floats above it
		var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
		root.layer = IgnoreRaycastLayer;

		GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		visual.name = "Visual";
		visual.layer = IgnoreRaycastLayer;
		Object.DestroyImmediate(visual.GetComponent<Collider>());
		visual.transform.SetParent(root.transform, false);
		visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
		visual.transform.localScale = Vector3.one * 0.35f;
		var meshRenderer = visual.GetComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = material;
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		var trigger = root.AddComponent<SphereCollider>();
		trigger.isTrigger = true;
		trigger.center = new Vector3(0f, 0.6f, 0f);
		trigger.radius = 0.8f;

		// kinematic body so the trigger reliably fires against the CharacterController
		var body = root.AddComponent<Rigidbody>();
		body.isKinematic = true;
		body.useGravity = false;

		var pickup = root.AddComponent<Pickup>();
		pickup.Type = kind;
		pickup.Amount = amount;
		pickup.Color = color;
		pickup.Visual = meshRenderer;
		pickup.PickupClip = LoadClip(PickupClipName);

		GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
		Object.DestroyImmediate(root);
		return prefab.GetComponent<Pickup>();
	}

	// Close-range spread weapon: 8 pellets, and a lunge (stagger threshold 40) is only interrupted when 7+ land
	private static void ApplyShotgunDefaults(FirearmData data)
	{
		data.DisplayName = "Relic Shotgun";
		data.HitMask = ~(1 << PlayerLayer);
		data.Damage = 7f;
		data.Knockback = 0.15f;
		data.Stagger = 6f;
		data.Range = 25f;
		data.PelletCount = 8;
		data.SpreadAngle = 6f;
		data.FireInterval = 0.9f;
		data.MagazineSize = 2;
		data.ReloadTime = 1.8f;
		data.StartingReserveAmmo = 8;
		data.MaxReserveAmmo = 16;
		data.AmmoPerPickup = 4;
		data.KickDistance = 0.14f;
		data.KickAngle = 18f;
	}

	private static void SetupPlayerPrefab(FirearmData revolverData, FirearmData shotgunData, MeleeData meleeData, DodgeData dodgeData, StyleData styleData,
		Material metalMat, Material woodMat, Material fistMat)
	{
		GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
		try
		{
			GetOrAdd<Health>(root).MaxHealth = 100f;
			GetOrAdd<PlayerDeath>(root);

			Transform cameraRoot = root.transform.Find("PlayerCameraRoot");

			// revolver viewmodel: pivot -> M1911 model (or placeholder cubes) + muzzle point
			Transform revolverPivot = GetOrCreateChild(cameraRoot, "RevolverViewmodel", new Vector3(0.25f, -0.22f, 0.45f));
			Transform muzzle = GetOrCreateChild(revolverPivot, "Muzzle", new Vector3(0f, 0.02f, 0.17f));
			if (!SetupGunModel(revolverPivot, muzzle, RevolverModelPath, 0.26f) && revolverPivot.Find("Body") == null)
			{
				CreateViewmodelCube(revolverPivot, "Body", Vector3.zero, new Vector3(0.07f, 0.12f, 0.3f), metalMat);
				CreateViewmodelCube(revolverPivot, "Grip", new Vector3(0f, -0.1f, -0.1f), new Vector3(0.06f, 0.14f, 0.07f), metalMat);
			}

			// shotgun viewmodel: Benelli M4 model (or placeholder cubes), hidden until switched to
			Transform shotgunPivot = GetOrCreateChild(cameraRoot, "ShotgunViewmodel", new Vector3(0.25f, -0.24f, 0.45f));
			Transform shotgunMuzzle = GetOrCreateChild(shotgunPivot, "Muzzle", new Vector3(0f, 0.02f, 0.36f));
			if (!SetupGunModel(shotgunPivot, shotgunMuzzle, ShotgunModelPath, 0.62f) && shotgunPivot.Find("BarrelL") == null)
			{
				CreateViewmodelCube(shotgunPivot, "BarrelL", new Vector3(-0.025f, 0.02f, 0.1f), new Vector3(0.045f, 0.045f, 0.5f), metalMat);
				CreateViewmodelCube(shotgunPivot, "BarrelR", new Vector3(0.025f, 0.02f, 0.1f), new Vector3(0.045f, 0.045f, 0.5f), metalMat);
				CreateViewmodelCube(shotgunPivot, "Stock", new Vector3(0f, -0.04f, -0.15f), new Vector3(0.08f, 0.1f, 0.3f), woodMat);
			}
			shotgunPivot.gameObject.SetActive(false);

			// the old relic blade viewmodel, replaced by the fist
			Transform oldBlade = cameraRoot.Find("BladeViewmodel");
			if (oldBlade != null) Object.DestroyImmediate(oldBlade.gameObject);

			// off-hand fist viewmodel: knuckles forward, forearm trailing back out of view
			Transform fistPivot = GetOrCreateChild(cameraRoot, "FistViewmodel", new Vector3(-0.28f, -0.3f, 0.4f));
			if (fistPivot.Find("Fist") == null)
			{
				CreateViewmodelCube(fistPivot, "Fist", Vector3.zero, new Vector3(0.1f, 0.1f, 0.12f), fistMat);
				CreateViewmodelCube(fistPivot, "Forearm", new Vector3(-0.01f, -0.02f, -0.2f), new Vector3(0.08f, 0.08f, 0.3f), fistMat);
			}

			Firearm revolver = GetOrAddFirearm(root, revolverData);
			revolver.Viewmodel = revolverPivot;
			revolver.Muzzle = muzzle;

			Firearm shotgun = GetOrAddFirearm(root, shotgunData);
			shotgun.Viewmodel = shotgunPivot;
			shotgun.Muzzle = shotgunMuzzle;

			GetOrAdd<WeaponSwitcher>(root).Firearms = new[] { revolver, shotgun };

			MeleeWeapon melee = GetOrAdd<MeleeWeapon>(root);
			melee.Data = meleeData;
			melee.Fist = fistPivot;

			GetOrAdd<PlayerDodge>(root).Data = dodgeData;
			GetOrAdd<StyleMeter>(root).Data = styleData;

			GetOrAdd<PlayerHUD>(root);

			LowHealthBreathing breathing = GetOrAdd<LowHealthBreathing>(root);
			if (breathing.BreathingClip == null) breathing.BreathingClip = LoadClip("breathing-6091.mp3");

			PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// Replaces everything under a gun pivot (except its muzzle) with a copy of the model, turned to face +Z,
	// scaled so it's `length` metres long and centred on the pivot. The muzzle goes on the model's front face.
	// Returns false if the model asset is missing.
	private static bool SetupGunModel(Transform pivot, Transform muzzle, string modelPath, float length)
	{
		GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
		if (source == null)
		{
			Debug.LogWarning("[PrototypeArenaBuilder] Gun model not found at " + modelPath + ", using placeholder cubes.");
			return false;
		}

		for (int i = pivot.childCount - 1; i >= 0; i--)
		{
			Transform child = pivot.GetChild(i);
			if (child != muzzle) Object.DestroyImmediate(child.gameObject);
		}

		// a plain copy rather than a prefab instance, so the builder owns it
		GameObject model = Object.Instantiate(source, pivot);
		model.name = "Model";
		model.transform.localPosition = Vector3.zero;
		model.transform.localRotation = Quaternion.Euler(0f, GunModelYaw, 0f);
		model.transform.localScale = Vector3.one;

		foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
		foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PlayerLayer;
		foreach (MeshRenderer meshRenderer in model.GetComponentsInChildren<MeshRenderer>(true))
		{
			meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}

		// every vertex in the pivot's space, at the model's native size
		var points = new System.Collections.Generic.List<Vector3>();
		foreach (MeshFilter meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
		{
			if (meshFilter.sharedMesh == null) continue;
			foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
			{
				points.Add(pivot.InverseTransformPoint(meshFilter.transform.TransformPoint(vertex)));
			}
		}
		if (points.Count == 0) return true;

		Bounds bounds = new Bounds(points[0], Vector3.zero);
		foreach (Vector3 point in points) bounds.Encapsulate(point);

		float scale = bounds.size.z > 0f ? length / bounds.size.z : 1f;
		Vector3 offset = -bounds.center * scale;
		model.transform.localScale = Vector3.one * scale;
		model.transform.localPosition = offset;

		// muzzle: the average of the frontmost few percent of the vertices (the barrel's end face)
		float frontLimit = bounds.max.z - bounds.size.z * 0.03f;
		Vector3 front = Vector3.zero;
		int frontCount = 0;
		foreach (Vector3 point in points)
		{
			if (point.z < frontLimit) continue;
			front += point;
			frontCount++;
		}
		front /= Mathf.Max(frontCount, 1);
		muzzle.localPosition = front * scale + offset + Vector3.forward * 0.01f;
		return true;
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

	private static Transform BuildScene(GameObject enemyPrefab, Pickup healthPickup, Pickup ammoPickup, Material floorMat, Material wallMat, Material coverMat)
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
		spawner.HealthDropPrefab = healthPickup;
		spawner.AmmoDropPrefab = ammoPickup;

		// placed pickups (respawning): health on the platform and behind cover, ammo by the pillars
		Transform pickupRoot = new GameObject("Pickups").transform;
		pickupRoot.SetParent(arena.transform, false);
		PlacePickup(pickupRoot, healthPickup, new Vector3(-13f, 1.5f, -13f));
		PlacePickup(pickupRoot, healthPickup, new Vector3(0f, 0f, 11.5f));
		PlacePickup(pickupRoot, ammoPickup, new Vector3(6f, 0f, -7.5f));
		PlacePickup(pickupRoot, ammoPickup, new Vector3(-6f, 0f, 7.5f));
		PlacePickup(pickupRoot, ammoPickup, new Vector3(13.5f, 0f, 0f));

		// player
		var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
		var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
		player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -16f), Quaternion.identity);

		return player.transform.Find("PlayerCameraRoot");
	}

	private static void PlacePickup(Transform parent, Pickup prefab, Vector3 position)
	{
		var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
		instance.transform.position = position;
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

	// the player has one Firearm component per weapon, told apart by their data asset
	private static Firearm GetOrAddFirearm(GameObject root, FirearmData data)
	{
		foreach (Firearm existing in root.GetComponents<Firearm>())
		{
			if (existing.Data == data) return existing;
		}
		Firearm firearm = root.AddComponent<Firearm>();
		firearm.Data = data;
		return firearm;
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
