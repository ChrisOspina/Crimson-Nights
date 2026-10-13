using System;
using System.Collections;
using StarterAssets;
using UnityEngine;

// Hitscan revolver. Fires from the centre of the screen, reloads with R or automatically when empty.
// Reloads draw from a limited reserve, which ammo pickups refill.
[RequireComponent(typeof(StarterAssetsInputs))]
public class Revolver : MonoBehaviour
{
	public RevolverData Data;
	[Tooltip("Viewmodel that kicks back when firing")]
	public Transform Viewmodel;
	[Tooltip("Where tracers and the muzzle flash start")]
	public Transform Muzzle;

	public int Ammo { get; private set; }
	public int MagazineSize => Data.MagazineSize;
	public int ReserveAmmo { get; private set; }
	public int MaxReserveAmmo => Data.MaxReserveAmmo;
	public bool IsReloading { get; private set; }

	// raised whenever Ammo, ReserveAmmo or IsReloading changes (for the HUD)
	public event Action Changed;

	private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

	private StarterAssetsInputs _input;
	private PlayerDodge _dodge;
	private Transform _camera;
	private AudioSource _audioSource;
	private float _nextFireTime;
	private float _reloadEndTime;

	private Vector3 _viewmodelRestPos;
	private Quaternion _viewmodelRestRot;
	private float _kick; // 0..1, decays back to rest

	private GameObject _muzzleFlash;

	private void Awake()
	{
		_input = GetComponent<StarterAssetsInputs>();
		_dodge = GetComponent<PlayerDodge>();
		_audioSource = gameObject.AddComponent<AudioSource>();
		_audioSource.playOnAwake = false;
		_audioSource.spatialBlend = 0f;

		Ammo = Data.MagazineSize;
		ReserveAmmo = Mathf.Min(Data.StartingReserveAmmo, Data.MaxReserveAmmo);

		if (Viewmodel != null)
		{
			_viewmodelRestPos = Viewmodel.localPosition;
			_viewmodelRestRot = Viewmodel.localRotation;
		}

		CreateMuzzleFlash();
	}

	private void Start()
	{
		_camera = Camera.main != null ? Camera.main.transform : transform;
		Changed?.Invoke();
	}

	private void OnDisable()
	{
		StopAllCoroutines();
		if (_muzzleFlash != null) _muzzleFlash.SetActive(false);
	}

	private void Update()
	{
		if (IsReloading && Time.time >= _reloadEndTime)
		{
			IsReloading = false;
			int loaded = Mathf.Min(Data.MagazineSize - Ammo, ReserveAmmo);
			Ammo += loaded;
			ReserveAmmo -= loaded;
			Changed?.Invoke();
		}

		if (_input.reload)
		{
			_input.reload = false;
			StartReload();
		}

		if (_input.fire)
		{
			_input.fire = false;
			TryFire();
		}

		UpdateViewmodel();
	}

	private void TryFire()
	{
		if (IsReloading || Time.time < _nextFireTime) return;

		if (Ammo <= 0)
		{
			PlayClip(Data.EmptyClip);
			StartReload();
			return;
		}

		_nextFireTime = Time.time + Data.FireInterval;
		Ammo--;
		Changed?.Invoke();

		Vector3 origin = _camera.position;
		Vector3 direction = _camera.forward;
		Vector3 end = origin + direction * Data.Range;

		if (Physics.Raycast(origin, direction, out RaycastHit hit, Data.Range, Data.HitMask, QueryTriggerInteraction.Ignore))
		{
			end = hit.point;

			Health health = hit.collider.GetComponentInParent<Health>();
			if (health != null)
			{
				float damage = Data.Damage * (_dodge != null ? _dodge.DamageMultiplier : 1f);
				health.TakeDamage(new DamageInfo(damage, hit.point, direction, Data.Knockback, gameObject));
			}

			SpawnImpact(hit.point, health != null ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.85f, 0.5f));
			if (Data.ImpactClip != null) AudioSource.PlayClipAtPoint(Data.ImpactClip, hit.point);
		}

		SpawnTracer(Muzzle != null ? Muzzle.position : origin, end);
		StartCoroutine(FlashMuzzle());
		PlayClip(Data.FireClip);
		_kick = 1f;

		if (Ammo <= 0) StartReload();
	}

	private void StartReload()
	{
		if (IsReloading || Ammo >= Data.MagazineSize || ReserveAmmo <= 0) return;

		IsReloading = true;
		_reloadEndTime = Time.time + Data.ReloadTime;
		PlayClip(Data.ReloadClip);
		Changed?.Invoke();
	}

	// Adds spare rounds up to the reserve limit and returns how many were taken
	public int AddReserveAmmo(int amount)
	{
		int added = Mathf.Clamp(Data.MaxReserveAmmo - ReserveAmmo, 0, Mathf.Max(0, amount));
		if (added == 0) return 0;

		ReserveAmmo += added;
		Changed?.Invoke();

		// picked up ammo with an empty gun: load it straight away
		if (Ammo <= 0) StartReload();
		return added;
	}

	private void UpdateViewmodel()
	{
		if (Viewmodel == null) return;

		_kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime * 8f);

		// tilt the gun down while reloading
		float reloadTilt = IsReloading ? 35f : 0f;

		Vector3 targetPos = _viewmodelRestPos + Vector3.back * (Data.KickDistance * _kick);
		Quaternion targetRot = _viewmodelRestRot * Quaternion.Euler(-Data.KickAngle * _kick + reloadTilt, 0f, 0f);

		Viewmodel.localPosition = targetPos;
		Viewmodel.localRotation = Quaternion.Slerp(Viewmodel.localRotation, targetRot, Time.deltaTime * 20f);
	}

	private void CreateMuzzleFlash()
	{
		if (Muzzle == null) return;

		_muzzleFlash = new GameObject("MuzzleFlash");
		_muzzleFlash.transform.SetParent(Muzzle, false);

		GameObject quad = CreateFxCube("Flash", new Color(1f, 0.8f, 0.4f), 0.12f);
		quad.transform.SetParent(_muzzleFlash.transform, false);

		Light flashLight = _muzzleFlash.AddComponent<Light>();
		flashLight.type = LightType.Point;
		flashLight.color = new Color(1f, 0.75f, 0.4f);
		flashLight.range = 6f;
		flashLight.intensity = 3f;

		_muzzleFlash.SetActive(false);
	}

	private IEnumerator FlashMuzzle()
	{
		if (_muzzleFlash == null) yield break;
		_muzzleFlash.SetActive(true);
		yield return new WaitForSeconds(0.04f);
		_muzzleFlash.SetActive(false);
	}

	private void SpawnTracer(Vector3 from, Vector3 to)
	{
		GameObject tracer = new GameObject("Tracer");
		LineRenderer line = tracer.AddComponent<LineRenderer>();
		line.positionCount = 2;
		line.SetPosition(0, from);
		line.SetPosition(1, to);
		line.startWidth = 0.02f;
		line.endWidth = 0.01f;
		line.sharedMaterial = Data.FxMaterial;
		line.startColor = new Color(1f, 0.9f, 0.6f, 1f);
		line.endColor = new Color(1f, 0.9f, 0.6f, 0.2f);
		line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		Destroy(tracer, Data.TracerDuration);
	}

	private void SpawnImpact(Vector3 point, Color color)
	{
		GameObject impact = CreateFxCube("Impact", color, 0.08f);
		impact.transform.position = point;
		impact.transform.rotation = UnityEngine.Random.rotation;
		Destroy(impact, 0.4f);
	}

	private GameObject CreateFxCube(string name, Color color, float size)
	{
		GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		cube.name = name;
		Destroy(cube.GetComponent<Collider>());
		cube.transform.localScale = Vector3.one * size;

		MeshRenderer meshRenderer = cube.GetComponent<MeshRenderer>();
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		if (Data.FxMaterial != null) meshRenderer.sharedMaterial = Data.FxMaterial;

		var block = new MaterialPropertyBlock();
		block.SetColor(BaseColorId, color);
		meshRenderer.SetPropertyBlock(block);
		return cube;
	}

	private void PlayClip(AudioClip clip)
	{
		if (clip != null) _audioSource.PlayOneShot(clip);
	}
}
