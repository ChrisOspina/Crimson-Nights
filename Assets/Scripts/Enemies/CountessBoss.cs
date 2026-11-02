using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The Countess Ilse Morvayne, the slice's mini-boss. She's an EnemyController like a thrall (her Strike and
// Lunge are tuned in her own EnemyData: harder hitting, and the revolver alone won't stagger her), and this
// scripts what a thrall doesn't have:
//   Intro:   she bursts out of her portrait, gets up and shrieks; untouchable until then. Boss bar and music start.
//   Enrage:  at EnrageAt health she shrieks again, calls thralls out of the blood and speeds up (Tempo).
//   Blink:   once enraged, if the player keeps their distance she bursts into blood and reforms behind them,
//            straight into a Strike (a chance for a perfect dodge).
// When she dies her thralls die with her, the music fades out and the bar goes. The Encounter that spawned
// her handles the doors and the cleared message.
[RequireComponent(typeof(EnemyController))]
public class CountessBoss : MonoBehaviour
{
	public string DisplayName = "Countess Ilse Morvayne";

	[Header("Intro")]
	[Tooltip("Seconds of the WakeUp animation before she shrieks")]
	public float WakeTime = 2.2f;
	[Tooltip("Seconds of the Agr (shriek) animation before she moves; also used for the enrage shriek")]
	public float RoarTime = 1.6f;

	[Header("Enrage")]
	[Range(0f, 1f)] public float EnrageAt = 0.5f;
	[Tooltip("EnemyController.Tempo once enraged: faster movement, wind-ups and recoveries")]
	public float EnragedTempo = 1.3f;
	public GameObject ThrallPrefab;
	public int SummonCount = 2;
	public float SummonRadius = 4f;

	[Header("Blood Blink (enraged)")]
	[Tooltip("Seconds between blinks (random in this range)")]
	public Vector2 BlinkInterval = new Vector2(6f, 10f);
	[Tooltip("She only blinks when the player is at least this far away")]
	public float BlinkMinDistance = 4f;
	[Tooltip("Seconds she's gone before reforming")]
	public float BlinkTime = 0.7f;
	[Tooltip("How far behind the player she reforms")]
	public float BlinkBehindDistance = 2.2f;

	[Header("Audio")]
	[Tooltip("Her shriek: intro, enrage and death")]
	public AudioClip ShriekClip;
	[Tooltip("Played where she vanishes and where she reforms")]
	public AudioClip BlinkClip;
	public AudioClip Music;
	[Range(0f, 1f)] public float MusicVolume = 0.45f;
	public float MusicFadeTime = 2f;

	public bool Enraged { get; private set; }

	private EnemyController _enemy;
	private EnemyAnimator _animator;
	private Health _health;
	private Renderer[] _renderers;
	private Collider[] _colliders;
	private AudioSource _voice;
	private AudioSource _music;

	private Transform _player;
	private Health _playerHealth;
	private PlayerHUD _hud;

	private readonly List<Health> _thralls = new List<Health>();
	private bool _busy = true; // the intro runs first
	private float _blinkTimer;

	private void Awake()
	{
		_enemy = GetComponent<EnemyController>();
		_animator = GetComponent<EnemyAnimator>();
		_health = GetComponent<Health>();
		Transform visual = _enemy.Visual != null ? _enemy.Visual : transform;
		_renderers = visual.GetComponentsInChildren<Renderer>(true);
		_colliders = GetComponentsInChildren<Collider>(true);

		_voice = gameObject.AddComponent<AudioSource>();
		_voice.playOnAwake = false;
		_voice.spatialBlend = 1f;
		_voice.minDistance = 4f;
		_voice.maxDistance = 40f;

		// on a child, so it keeps fading while the body lies there after death
		_music = new GameObject("Music").AddComponent<AudioSource>();
		_music.transform.SetParent(transform, false);
		_music.playOnAwake = false;
		_music.loop = true;
		_music.spatialBlend = 0f;
		_music.clip = Music;
	}

	private void OnEnable()
	{
		_health.Died += OnDied;
	}

	private void OnDisable()
	{
		_health.Died -= OnDied;
	}

	private void Start()
	{
		GameObject player = GameObject.FindGameObjectWithTag("Player");
		if (player != null)
		{
			_player = player.transform;
			_playerHealth = player.GetComponent<Health>();
			_hud = player.GetComponent<PlayerHUD>();
		}
		StartCoroutine(Intro());
	}

	private void Update()
	{
		if (_busy || _health.IsDead) return;

		if (!Enraged && _health.Normalized <= EnrageAt)
		{
			StartCoroutine(Enrage());
			return;
		}

		if (!Enraged || _player == null || (_playerHealth != null && _playerHealth.IsDead)) return;
		_blinkTimer -= Time.deltaTime;
		if (_blinkTimer <= 0f && _enemy.CurrentState == EnemyController.State.Chase && FlatDistanceToPlayer() >= BlinkMinDistance)
		{
			StartCoroutine(Blink());
		}
	}

	// ---------------------------------------------------------------- phases

	private IEnumerator Intro()
	{
		_busy = true;
		_enemy.Hold();
		_health.Invulnerable = true;
		FX.Spawn(_enemy.Data.DeathFX, transform.position + Vector3.up * 1.4f, transform.forward);
		if (_hud != null) _hud.ShowBoss(DisplayName, _health);
		PlayState("WakeUp");
		yield return new WaitForSeconds(WakeTime);

		Shriek();
		StartCoroutine(FadeMusic(MusicVolume, MusicFadeTime * 0.5f));
		yield return new WaitForSeconds(RoarTime);

		_health.Invulnerable = false;
		_enemy.Release();
		_busy = false;
	}

	private IEnumerator Enrage()
	{
		_busy = true;
		Enraged = true;
		_enemy.Hold();
		_health.Invulnerable = true;
		Shriek();
		Summon();
		yield return new WaitForSeconds(RoarTime);

		_health.Invulnerable = false;
		_enemy.SetTempo(EnragedTempo);
		_enemy.Release();
		_blinkTimer = Random.Range(BlinkInterval.x, BlinkInterval.y) * 0.5f;
		_busy = false;
	}

	// bursts into blood, and reforms behind the player facing them; the chase that follows goes straight into a Strike
	private IEnumerator Blink()
	{
		_busy = true;
		_enemy.Hold();
		_health.Invulnerable = true;
		Vector3 chest = Vector3.up * 1.2f;
		FX.Spawn(_enemy.Data.DeathFX, transform.position + chest, Vector3.up);
		PlayAt(BlinkClip, transform.position);
		SetPresent(false);
		yield return new WaitForSeconds(BlinkTime);

		if (_player != null && TryFindBlinkPoint(out Vector3 point))
		{
			_enemy.Teleport(point, _player.position - point);
		}
		SetPresent(true);
		FX.Spawn(_enemy.Data.DeathFX, transform.position + chest, Vector3.up);
		PlayAt(BlinkClip, transform.position);

		_health.Invulnerable = false;
		_enemy.Release();
		_blinkTimer = Random.Range(BlinkInterval.x, BlinkInterval.y);
		_busy = false;
	}

	private void OnDied()
	{
		// she can't die mid-blink or mid-shriek (invulnerable), so she's visible here
		StopAllCoroutines();
		_busy = true;
		Shriek();

		// her thralls go with her
		foreach (Health thrall in _thralls)
		{
			if (thrall != null && !thrall.IsDead)
			{
				thrall.TakeDamage(new DamageInfo(thrall.Current + 1f, thrall.transform.position + Vector3.up, Vector3.up, 0f, gameObject));
			}
		}
		_thralls.Clear();

		if (_hud != null) _hud.HideBoss();
		StartCoroutine(FadeMusic(0f, MusicFadeTime));
	}

	// ---------------------------------------------------------------- helpers

	private void Summon()
	{
		if (ThrallPrefab == null) return;
		for (int i = 0; i < SummonCount; i++)
		{
			// spread around her, on the NavMesh
			float angle = (360f / SummonCount) * i + 90f;
			Vector3 around = transform.position + Quaternion.Euler(0f, angle, 0f) * transform.forward * SummonRadius;
			if (!NavMesh.SamplePosition(around, out NavMeshHit hit, SummonRadius, NavMesh.AllAreas)) continue;

			Vector3 facing = _player != null ? _player.position - hit.position : transform.forward;
			facing.y = 0f;
			Quaternion rotation = facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : transform.rotation;
			GameObject thrall = Instantiate(ThrallPrefab, hit.position, rotation);
			FX.Spawn(_enemy.Data.DeathFX, hit.position + Vector3.up * 0.2f, Vector3.up);
			Health health = thrall.GetComponent<Health>();
			if (health != null) _thralls.Add(health);
		}
	}

	// behind the player first, then to their sides; a point needs clear NavMesh between it and the player
	private bool TryFindBlinkPoint(out Vector3 point)
	{
		point = transform.position;
		if (!NavMesh.SamplePosition(_player.position, out NavMeshHit playerHit, 2f, NavMesh.AllAreas)) return false;

		Camera view = Camera.main;
		Vector3 forward = view != null ? view.transform.forward : _player.forward;
		forward.y = 0f;
		if (forward.sqrMagnitude < 0.001f) forward = _player.forward;
		forward.Normalize();

		foreach (float angle in new[] { 180f, 150f, 210f, 90f, 270f })
		{
			Vector3 candidate = playerHit.position + Quaternion.Euler(0f, angle, 0f) * forward * BlinkBehindDistance;
			if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.75f, NavMesh.AllAreas)) continue;
			if (NavMesh.Raycast(playerHit.position, hit.position, out _, NavMesh.AllAreas)) continue;
			point = hit.position;
			return true;
		}
		return false;
	}

	// hides or shows her body and turns her colliders off/on, so shots pass through the blood mist
	private void SetPresent(bool present)
	{
		foreach (Renderer r in _renderers)
		{
			if (r != null) r.enabled = present;
		}
		foreach (Collider c in _colliders)
		{
			if (c != null) c.enabled = present;
		}
	}

	private void Shriek()
	{
		PlayState("Agr");
		if (ShriekClip != null) _voice.PlayOneShot(ShriekClip);
	}

	private void PlayState(string state)
	{
		if (_animator != null) _animator.PlayState(state);
	}

	private void PlayAt(AudioClip clip, Vector3 position)
	{
		if (clip != null) AudioSource.PlayClipAtPoint(clip, position);
	}

	private IEnumerator FadeMusic(float target, float time)
	{
		if (_music.clip == null) yield break;
		if (target > 0f && !_music.isPlaying)
		{
			_music.volume = 0f;
			_music.Play();
		}
		float start = _music.volume;
		for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
		{
			_music.volume = Mathf.Lerp(start, target, t / time);
			yield return null;
		}
		_music.volume = target;
		if (target <= 0f) _music.Stop();
	}

	private float FlatDistanceToPlayer()
	{
		Vector3 toPlayer = _player.position - transform.position;
		toPlayer.y = 0f;
		return toPlayer.magnitude;
	}
}
