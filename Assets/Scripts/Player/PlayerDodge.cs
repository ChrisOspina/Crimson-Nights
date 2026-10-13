using System;
using StarterAssets;
using UnityEngine;

// Quick dash with i-frames (RMB / gamepad South) in the move direction, or backwards when standing still.
// Dodging just before an enemy strike lands is a perfect dodge: slow motion, the attacker staggers,
// and the player's hits deal bonus damage for a short counter window.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(StarterAssetsInputs))]
[RequireComponent(typeof(Health))]
public class PlayerDodge : MonoBehaviour
{
	public DodgeData Data;

	public bool IsDodging { get; private set; }
	public bool IsCounterWindow => Time.time < _counterEndTime;
	// weapons multiply their damage by this
	public float DamageMultiplier => IsCounterWindow ? Data.CounterDamageMultiplier : 1f;
	// 1 right after dodging, 0 when the next dodge is ready
	public float CooldownRemaining01 => Data.Cooldown > 0f ? Mathf.Clamp01((_cooldownEndTime - Time.time) / Data.Cooldown) : 0f;

	public event Action Dodged;
	public event Action<EnemyController> PerfectDodged;

	private CharacterController _controller;
	private StarterAssetsInputs _input;
	private FirstPersonController _firstPersonController;
	private Health _health;
	private AudioSource _audioSource;

	private Vector3 _direction;
	private Vector3 _origin;
	private float _startTime = -100f;
	private float _progress; // eased 0..1 fraction of Distance already moved
	private float _cooldownEndTime;
	private float _counterEndTime;

	private void Awake()
	{
		_controller = GetComponent<CharacterController>();
		_input = GetComponent<StarterAssetsInputs>();
		_firstPersonController = GetComponent<FirstPersonController>();
		_health = GetComponent<Health>();

		_audioSource = gameObject.AddComponent<AudioSource>();
		_audioSource.playOnAwake = false;
		_audioSource.spatialBlend = 0f;
	}

	private void OnDisable()
	{
		EndDash();
		if (_health != null) _health.Invulnerable = false;
	}

	private void Update()
	{
		if (_input.dodge)
		{
			_input.dodge = false;
			TryStartDodge();
		}

		if (IsDodging) UpdateDash();

		_health.Invulnerable = Time.time < _startTime + Data.InvulnerableTime;
	}

	private void TryStartDodge()
	{
		if (IsDodging || Time.time < _cooldownEndTime) return;

		Vector2 move = _input.move;
		_direction = move.sqrMagnitude > 0.01f
			? (transform.right * move.x + transform.forward * move.y).normalized
			: -transform.forward;

		IsDodging = true;
		_origin = transform.position;
		_startTime = Time.time;
		_progress = 0f;
		_cooldownEndTime = Time.time + Data.Cooldown;
		if (_firstPersonController != null) _firstPersonController.MovementLocked = true;

		PlayClip(Data.DodgeClip);
		Dodged?.Invoke();
	}

	private void UpdateDash()
	{
		float t = Data.Duration > 0f ? Mathf.Clamp01((Time.time - _startTime) / Data.Duration) : 1f;
		// ease out: fast start, soft stop
		float eased = 1f - (1f - t) * (1f - t);

		_controller.Move(_direction * ((eased - _progress) * Data.Distance));
		_progress = eased;

		if (t >= 1f) EndDash();
	}

	private void EndDash()
	{
		IsDodging = false;
		if (_firstPersonController != null) _firstPersonController.MovementLocked = false;
	}

	// Called by an enemy as its strike lands. Succeeds if the player started dodging within PerfectWindow
	// and was inside the attack's reach and arc when they did.
	public bool TryPerfectDodge(EnemyController attacker, float reach, float maxAngle)
	{
		if (!enabled || Time.time - _startTime > Data.PerfectWindow) return false;

		Vector3 toOrigin = _origin - attacker.transform.position;
		toOrigin.y = 0f;
		if (toOrigin.magnitude > reach || Vector3.Angle(attacker.transform.forward, toOrigin) > maxAngle) return false;

		_counterEndTime = Time.time + Data.CounterWindow;
		attacker.Stagger(Data.AttackerStaggerTime);
		SlowMotion.Play(Data.SlowMotionScale, Data.SlowMotionDuration);
		PlayClip(Data.PerfectDodgeClip);
		PerfectDodged?.Invoke(attacker);
		return true;
	}

	private void PlayClip(AudioClip clip)
	{
		if (clip != null) _audioSource.PlayOneShot(clip);
	}
}
