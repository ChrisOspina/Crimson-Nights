using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

// Relic blade. Tap melee for a light swing; hold past MeleeData.HeavyHoldTime and release for a heavy swing.
[RequireComponent(typeof(StarterAssetsInputs))]
public class MeleeWeapon : MonoBehaviour
{
	public MeleeData Data;
	[Tooltip("Blade viewmodel animated by code")]
	public Transform Blade;

	[Header("Blade Poses (relative to its rest rotation)")]
	public Vector3 WindUpEuler = new Vector3(-30f, -40f, 25f);
	public Vector3 SwingEndEuler = new Vector3(30f, 60f, -30f);

	public bool IsCharging => _charging;
	public bool IsHeavyReady => _charging && _holdTime >= Data.HeavyHoldTime;
	public bool IsSwinging { get; private set; }

	// true for a heavy attack
	public event Action<bool> Swung;
	public event Action<Health> Hit;

	private StarterAssetsInputs _input;
	private PlayerDodge _dodge;
	private Transform _camera;
	private AudioSource _audioSource;

	private bool _wasHeld;
	private bool _charging;
	private float _holdTime;
	private float _cooldownEndTime;

	private Quaternion _bladeRestRot;
	private Vector3 _bladeRestPos;
	private readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
	private readonly Collider[] _overlapBuffer = new Collider[32];

	private void Awake()
	{
		_input = GetComponent<StarterAssetsInputs>();
		_dodge = GetComponent<PlayerDodge>();
		_audioSource = gameObject.AddComponent<AudioSource>();
		_audioSource.playOnAwake = false;
		_audioSource.spatialBlend = 0f;

		if (Blade != null)
		{
			_bladeRestRot = Blade.localRotation;
			_bladeRestPos = Blade.localPosition;
		}
	}

	private void Start()
	{
		_camera = Camera.main != null ? Camera.main.transform : transform;
	}

	private void OnDisable()
	{
		StopAllCoroutines();
		_charging = false;
		IsSwinging = false;
	}

	private void Update()
	{
		bool held = _input.melee;
		bool pressedThisFrame = held && !_wasHeld;
		_wasHeld = held;

		if (!_charging && !IsSwinging && pressedThisFrame && Time.time >= _cooldownEndTime)
		{
			_charging = true;
			_holdTime = 0f;
		}

		if (_charging)
		{
			_holdTime += Time.deltaTime;

			if (!held)
			{
				_charging = false;
				StartCoroutine(Swing(_holdTime >= Data.HeavyHoldTime));
			}
		}

		if (!IsSwinging) UpdateIdlePose();
	}

	private void UpdateIdlePose()
	{
		if (Blade == null) return;

		// pull back while a heavy is charged
		Quaternion target = IsHeavyReady ? _bladeRestRot * Quaternion.Euler(WindUpEuler) : _bladeRestRot;
		Blade.localRotation = Quaternion.Slerp(Blade.localRotation, target, Time.deltaTime * 15f);
		Blade.localPosition = Vector3.Lerp(Blade.localPosition, _bladeRestPos, Time.deltaTime * 15f);
	}

	private IEnumerator Swing(bool heavy)
	{
		MeleeData.Attack attack = heavy ? Data.Heavy : Data.Light;

		IsSwinging = true;
		_hitThisSwing.Clear();
		Swung?.Invoke(heavy);
		if (Data.SwingClip != null) _audioSource.PlayOneShot(Data.SwingClip);

		Quaternion from = _bladeRestRot * Quaternion.Euler(WindUpEuler);
		Quaternion to = _bladeRestRot * Quaternion.Euler(SwingEndEuler);
		bool hitChecked = false;

		for (float t = 0f; t < attack.SwingTime; t += Time.deltaTime)
		{
			if (Blade != null)
			{
				Blade.localRotation = Quaternion.Slerp(from, to, t / attack.SwingTime);
			}

			if (!hitChecked && t >= attack.HitDelay)
			{
				hitChecked = true;
				DoHit(attack);
			}
			yield return null;
		}

		if (!hitChecked) DoHit(attack);

		IsSwinging = false;
		_cooldownEndTime = Time.time + attack.Cooldown;
	}

	private void DoHit(MeleeData.Attack attack)
	{
		Vector3 center = _camera.position + _camera.forward * (attack.Reach - attack.Radius * 0.5f);
		int count = Physics.OverlapSphereNonAlloc(center, attack.Radius, _overlapBuffer, Data.HitMask, QueryTriggerInteraction.Ignore);

		Vector3 flatForward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
		float damage = attack.Damage * (_dodge != null ? _dodge.DamageMultiplier : 1f);

		for (int i = 0; i < count; i++)
		{
			Health health = _overlapBuffer[i].GetComponentInParent<Health>();
			if (health == null || health.gameObject == gameObject || !_hitThisSwing.Add(health)) continue;

			Vector3 point = _overlapBuffer[i].ClosestPoint(_camera.position);
			health.TakeDamage(new DamageInfo(damage, point, flatForward, attack.Knockback, gameObject, attack.Stagger));
			Hit?.Invoke(health);
		}

		if (_hitThisSwing.Count > 0 && Data.HitClip != null) _audioSource.PlayOneShot(Data.HitClip);
	}

	private void OnDrawGizmosSelected()
	{
		if (Data == null) return;
		Transform cam = Camera.main != null ? Camera.main.transform : transform;
		Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
		Gizmos.DrawSphere(cam.position + cam.forward * (Data.Light.Reach - Data.Light.Radius * 0.5f), Data.Light.Radius);
	}
}
