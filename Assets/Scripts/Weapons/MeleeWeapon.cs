using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

// Off-hand punches. Tapping melee chains MeleeData.LightCombo (jab, cross, hook finisher) as long as each
// press follows within the combo window. Holding past MeleeData.HeavyHoldTime cocks the fist back and
// releasing throws a heavy haymaker, which ends the chain. Presses made just before the fist is free are buffered.
[RequireComponent(typeof(StarterAssetsInputs))]
public class MeleeWeapon : MonoBehaviour
{
	public const string WeaponName = "Fists";

	public MeleeData Data;
	[Tooltip("Fist viewmodel animated by code")]
	public Transform Fist;
	[Tooltip("Where the fist is held while a heavy is charged, as an offset from its rest position")]
	public Vector3 PullBackOffset = new Vector3(-0.06f, -0.04f, -0.15f);

	public bool IsCharging => _charging;
	public bool IsHeavyReady => _charging && _holdTime >= Data.HeavyHoldTime;
	public bool IsSwinging { get; private set; }
	// index into LightCombo of the next light punch, 0 when the chain has reset
	public int ComboStep => Time.time <= _comboExpireTime ? _comboStep : 0;

	// raised as each punch is thrown
	public event Action<MeleeData.Attack> Swung;
	public event Action<AttackHit> Hit;

	private StarterAssetsInputs _input;
	private PlayerDodge _dodge;
	private Transform _camera;
	private AudioSource _audioSource;

	private bool _wasHeld;
	private bool _charging;
	private float _holdTime;
	private float _cooldownEndTime;

	private bool _hasQueuedPunch;
	private bool _queuedHeavy;
	private float _queuedTime;

	private int _comboStep;
	private float _comboExpireTime;

	private Vector3 _fistRestPos;
	private readonly HashSet<Health> _hitThisSwing = new HashSet<Health>();
	private readonly Collider[] _overlapBuffer = new Collider[32];

	private void Awake()
	{
		_input = GetComponent<StarterAssetsInputs>();
		_dodge = GetComponent<PlayerDodge>();
		_audioSource = gameObject.AddComponent<AudioSource>();
		_audioSource.playOnAwake = false;
		_audioSource.spatialBlend = 0f;

		if (Fist != null) _fistRestPos = Fist.localPosition;
	}

	private void Start()
	{
		_camera = Camera.main != null ? Camera.main.transform : transform;
	}

	private void OnDisable()
	{
		StopAllCoroutines();
		_charging = false;
		_hasQueuedPunch = false;
		IsSwinging = false;
	}

	private void Update()
	{
		bool held = _input.melee;
		bool pressedThisFrame = held && !_wasHeld;
		_wasHeld = held;

		// charging is tracked even mid-punch, so a press during a punch is buffered rather than lost
		if (!_charging && pressedThisFrame)
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
				_hasQueuedPunch = true;
				_queuedHeavy = _holdTime >= Data.HeavyHoldTime;
				_queuedTime = Time.time;
			}
		}

		if (_hasQueuedPunch && !IsSwinging && Time.time >= _cooldownEndTime)
		{
			_hasQueuedPunch = false;
			// a press from too long ago (during a long cooldown) is dropped rather than thrown late
			if (Time.time - _queuedTime <= Data.InputBufferTime) StartCoroutine(Punch(_queuedHeavy));
		}

		if (!IsSwinging) UpdateIdlePose();
	}

	private void UpdateIdlePose()
	{
		if (Fist == null) return;

		// cock the fist back while a heavy is charged
		Vector3 target = IsHeavyReady ? _fistRestPos + PullBackOffset : _fistRestPos;
		Fist.localPosition = Vector3.Lerp(Fist.localPosition, target, Time.deltaTime * 15f);
	}

	private IEnumerator Punch(bool heavy)
	{
		MeleeData.Attack attack;
		bool finisher;
		if (heavy || Data.LightCombo.Length == 0)
		{
			attack = Data.Heavy;
			finisher = true;
			_comboStep = 0;
		}
		else
		{
			int step = ComboStep;
			attack = Data.LightCombo[step];
			finisher = Data.LightCombo.Length > 1 && step == Data.LightCombo.Length - 1;
			_comboStep = finisher ? 0 : step + 1;
		}

		IsSwinging = true;
		_hitThisSwing.Clear();
		Swung?.Invoke(attack);
		if (Data.SwingClip != null) _audioSource.PlayOneShot(Data.SwingClip);

		Vector3 from = Fist != null ? Fist.localPosition : _fistRestPos;
		Vector3 extended = _fistRestPos + attack.PunchOffset;
		bool hitChecked = false;

		for (float t = 0f; t < attack.SwingTime; t += Time.deltaTime)
		{
			if (Fist != null)
			{
				// snap out to full extension by the hit, then ease back to rest
				float extendTime = Mathf.Max(attack.HitDelay, 0.01f);
				Fist.localPosition = t < extendTime
					? Vector3.Lerp(from, extended, t / extendTime)
					: Vector3.Lerp(extended, _fistRestPos, Mathf.SmoothStep(0f, 1f, (t - extendTime) / (attack.SwingTime - extendTime)));
			}

			if (!hitChecked && t >= attack.HitDelay)
			{
				hitChecked = true;
				DoHit(attack, finisher);
			}
			yield return null;
		}

		if (!hitChecked) DoHit(attack, finisher);

		IsSwinging = false;
		_cooldownEndTime = Time.time + attack.Cooldown;
		_comboExpireTime = _cooldownEndTime + Data.ComboWindow;
	}

	private void DoHit(MeleeData.Attack attack, bool finisher)
	{
		Vector3 center = _camera.position + _camera.forward * (attack.Reach - attack.Radius * 0.5f);
		int count = Physics.OverlapSphereNonAlloc(center, attack.Radius, _overlapBuffer, Data.HitMask, QueryTriggerInteraction.Ignore);

		Vector3 flatForward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
		float damage = attack.Damage * (_dodge != null ? _dodge.DamageMultiplier : 1f);

		for (int i = 0; i < count; i++)
		{
			Health health = _overlapBuffer[i].GetComponentInParent<Health>();
			if (health == null || health.gameObject == gameObject || health.IsDead || !_hitThisSwing.Add(health)) continue;

			Vector3 point = _overlapBuffer[i].ClosestPoint(_camera.position);
			health.TakeDamage(new DamageInfo(damage, point, flatForward, attack.Knockback, gameObject, attack.Stagger));
			Hit?.Invoke(new AttackHit(health, attack.Name, WeaponName, true, finisher));
		}

		AudioClip hitClip = finisher && Data.HeavyHitClip != null ? Data.HeavyHitClip : Data.HitClip;
		if (_hitThisSwing.Count > 0 && hitClip != null) _audioSource.PlayOneShot(hitClip);
	}

	private void OnDrawGizmosSelected()
	{
		if (Data == null || Data.LightCombo.Length == 0) return;
		Transform cam = Camera.main != null ? Camera.main.transform : transform;
		MeleeData.Attack jab = Data.LightCombo[0];
		Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
		Gizmos.DrawSphere(cam.position + cam.forward * (jab.Reach - jab.Radius * 0.5f), jab.Radius);
	}
}
