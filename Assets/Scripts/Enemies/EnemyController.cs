using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Prototype melee enemy (Vampire Thrall stand-in): chase, then one of two telegraphed attacks, then recover.
// Strike: close-range swipe, interrupted by any revolver shot. Lunge: mid-range dash with a longer red-orange
// telegraph that only a high-stagger hit (shotgun, heavy punch) interrupts. Hits below the current stagger
// threshold still do damage but don't interrupt.
// A boss script (CountessBoss) can Hold the enemy in the Scripted state for intros and special moves, Release
// it back into the chase, Teleport it, and speed it up with SetTempo.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
	public enum State { Idle, Chase, WindUp, Lunge, Recover, Stagger, Dead, Scripted }

	public EnemyData Data;
	[Tooltip("Visual child that gets tinted and scaled (the prototype cube)")]
	public Renderer Body;
	[Tooltip("Model root that gets scaled; every renderer under it is tinted. Overrides Body")]
	public Transform Visual;

	public State CurrentState { get; private set; } = State.Idle;
	// the attack being wound up or performed (compare with Data.Strike / Data.Lunge)
	public EnemyData.Attack CurrentAttack => _attack;
	// multiplies move speed and divides wind-up and recovery times (1 = as tuned in Data)
	public float Tempo { get; private set; } = 1f;
	// the current attack's wind-up at the current tempo
	public float WindUpDuration => _attack.WindUpTime / Tempo;

	private int _tintId;
	private Renderer[] _renderers = new Renderer[0];

	private NavMeshAgent _agent;
	private Health _health;
	private Transform _player;
	private Health _playerHealth;
	private PlayerDodge _playerDodge;

	private float _stateTimer;
	private float _repathTimer;
	private float _hitFlashTimer;
	private Vector3 _knockback;
	private Vector3 _visualRestScale;
	private MaterialPropertyBlock _block;
	private AudioSource _voice;
	private float _nextShriekTime;
	// shared by every enemy, so a pack noticing the player at once doesn't shriek in a wall of noise
	private static float _anyShriekTime = -100f;
	private const float ShriekSpacing = 0.5f;

	// the attack being wound up or performed
	private EnemyData.Attack _attack;
	private float _lungeReadyTime;
	private Vector3 _lungeDirection;
	private float _lungeRemaining;

	private void Awake()
	{
		_agent = GetComponent<NavMeshAgent>();
		_health = GetComponent<Health>();
		_block = new MaterialPropertyBlock();

		_health.ResetHealth(Data.MaxHealth);
		_agent.speed = Data.MoveSpeed;
		_agent.acceleration = Data.Acceleration;
		_agent.stoppingDistance = Data.Strike.MaxRange * 0.8f;
		_attack = Data.Strike;

		if (Visual == null && Body != null) Visual = Body.transform;
		if (Visual != null)
		{
			_visualRestScale = Visual.localScale;
			_renderers = Visual.GetComponentsInChildren<Renderer>(true);
		}
		if (Data.ShriekClips != null && Data.ShriekClips.Length > 0)
		{
			_voice = gameObject.AddComponent<AudioSource>();
			_voice.playOnAwake = false;
			_voice.spatialBlend = 1f;
			_voice.minDistance = 3f;
			_voice.maxDistance = 35f;
			_voice.rolloffMode = AudioRolloffMode.Linear;
		}
		_tintId = Shader.PropertyToID(string.IsNullOrEmpty(Data.TintProperty) ? "_BaseColor" : Data.TintProperty);
	}

	private void OnEnable()
	{
		_health.Damaged += OnDamaged;
		_health.Died += OnDied;
	}

	private void OnDisable()
	{
		_health.Damaged -= OnDamaged;
		_health.Died -= OnDied;
	}

	private void Start()
	{
		GameObject player = GameObject.FindGameObjectWithTag("Player");
		if (player != null)
		{
			_player = player.transform;
			_playerHealth = player.GetComponent<Health>();
			_playerDodge = player.GetComponent<PlayerDodge>();
		}

		// the NavMesh is baked at runtime, so snap onto it now
		if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
		{
			_agent.Warp(hit.position);
		}

		// don't lunge the instant a wave spawns
		_lungeReadyTime = Time.time + Data.Lunge.Cooldown * 0.5f;

		SetColor(Data.BodyColor);
	}

	private void Update()
	{
		if (CurrentState == State.Dead) return;

		_stateTimer -= Time.deltaTime;
		ApplyKnockback();

		bool playerAlive = _player != null && (_playerHealth == null || !_playerHealth.IsDead);

		switch (CurrentState)
		{
			case State.Idle:
				if (playerAlive && DistanceToPlayer() <= Data.DetectionRange)
				{
					Shriek();
					Enter(State.Chase);
				}
				break;

			case State.Chase:
				if (!playerAlive)
				{
					Enter(State.Idle);
					break;
				}
				_repathTimer -= Time.deltaTime;
				if (_repathTimer <= 0f && _agent.isOnNavMesh)
				{
					_repathTimer = 0.2f;
					_agent.SetDestination(_player.position);
				}

				float distance = DistanceToPlayer();
				if (CanLunge(distance)) BeginAttack(Data.Lunge);
				else if (distance <= Data.Strike.MaxRange) BeginAttack(Data.Strike);
				break;

			case State.WindUp:
				FacePlayer(_attack == Data.Lunge ? 10f : 6f);
				if (_stateTimer <= 0f) ReleaseAttack();
				break;

			case State.Lunge:
				UpdateLunge();
				break;

			case State.Recover:
			case State.Stagger:
				if (_stateTimer <= 0f) Enter(playerAlive ? State.Chase : State.Idle);
				break;

			// held by a script until Release
			case State.Scripted:
				break;
		}

		UpdateVisuals();
	}

	private void Enter(State state)
	{
		CurrentState = state;

		bool moving = state == State.Chase;
		if (_agent.isOnNavMesh)
		{
			_agent.isStopped = !moving;
			if (!moving) _agent.velocity = Vector3.zero;
		}
		_agent.updateRotation = moving;

		switch (state)
		{
			case State.WindUp: _stateTimer = WindUpDuration; break;
			case State.Recover: _stateTimer = _attack.RecoverTime / Tempo; break;
			case State.Stagger: _stateTimer = Data.StaggerTime; break;
			case State.Chase: _repathTimer = 0f; break;
		}
	}

	// ---------------------------------------------------------------- attacks

	private bool CanLunge(float distance)
	{
		if (Time.time < _lungeReadyTime) return false;
		if (distance < Data.Lunge.MinRange || distance > Data.Lunge.MaxRange) return false;
		// only lunge along a clear stretch of NavMesh (Raycast returns true when blocked)
		return !NavMesh.Raycast(transform.position, _player.position, out _, NavMesh.AllAreas);
	}

	private void BeginAttack(EnemyData.Attack attack)
	{
		_attack = attack;
		if (attack == Data.Lunge) Shriek(true);
		Enter(State.WindUp);
	}

	// a random clip from Data.ShriekClips, slightly re-pitched. `force` (the Lunge tell) ignores the cooldowns
	private void Shriek(bool force = false)
	{
		if (_voice == null) return;
		if (!force && (Time.time < _nextShriekTime || Time.time < _anyShriekTime + ShriekSpacing)) return;

		AudioClip clip = Data.ShriekClips[Random.Range(0, Data.ShriekClips.Length)];
		if (clip == null) return;
		_voice.pitch = Random.Range(0.9f, 1.1f);
		_voice.PlayOneShot(clip, Data.ShriekVolume);
		_nextShriekTime = Time.time + Data.ShriekCooldown;
		_anyShriekTime = Time.time;
	}

	// end of the wind-up: a perfect dodge cancels the attack, otherwise strike or start the lunge dash
	private void ReleaseAttack()
	{
		if (_attack == Data.Lunge) _lungeReadyTime = Time.time + Data.Lunge.Cooldown;

		float reach = _attack.MaxRange + _attack.ReachGrace;
		if (_playerDodge != null && _playerDodge.TryPerfectDodge(this, reach, _attack.Angle)) return;

		if (_attack == Data.Lunge)
		{
			StartLunge();
			return;
		}

		Vector3 toPlayer = FlatToPlayer();
		if (toPlayer.magnitude <= reach && Vector3.Angle(transform.forward, toPlayer) <= _attack.Angle)
		{
			DamagePlayer(toPlayer.normalized);
		}
		Enter(State.Recover);
	}

	private void StartLunge()
	{
		Vector3 toPlayer = FlatToPlayer();
		_lungeDirection = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;
		_lungeRemaining = Mathf.Min(toPlayer.magnitude + Data.LungeOvershoot, Data.LungeMaxDistance);
		transform.rotation = Quaternion.LookRotation(_lungeDirection);
		Enter(State.Lunge);
	}

	// the dash commits to the direction chosen at release, so sidestepping or dodging avoids it
	private void UpdateLunge()
	{
		float step = Mathf.Min(Data.LungeSpeed * Time.deltaTime, _lungeRemaining);
		Vector3 before = transform.position;
		if (_agent.isOnNavMesh) _agent.Move(_lungeDirection * step);
		_lungeRemaining -= step;

		if (_player != null && DistanceToPlayer() <= Data.LungeHitRadius && DamagePlayer(_lungeDirection))
		{
			Enter(State.Recover);
			return;
		}

		// finished, or stopped by the edge of the NavMesh
		Vector3 moved = transform.position - before;
		moved.y = 0f;
		if (_lungeRemaining <= 0f || moved.magnitude < step * 0.25f) Enter(State.Recover);
	}

	// returns true if the hit actually did damage (false while the player is dodging)
	private bool DamagePlayer(Vector3 direction)
	{
		if (_playerHealth == null) return false;

		float before = _playerHealth.Current;
		_playerHealth.TakeDamage(new DamageInfo(_attack.Damage, _player.position, direction, 0f, gameObject));
		if (_playerHealth.Current >= before) return false;

		if (Data.AttackHitClip != null) AudioSource.PlayClipAtPoint(Data.AttackHitClip, transform.position);
		// the bite sprays just in front of the camera, between it and the enemy, so the player sees it
		Camera view = Camera.main;
		Vector3 eye = view != null ? view.transform.position : _player.position + Vector3.up * 1.5f;
		FX.Spawn(Data.BiteFX, eye - direction * 0.6f + Vector3.down * 0.15f, direction);
		return true;
	}

	// ---------------------------------------------------------------- scripting (bosses)

	// stops the enemy where it stands, cancelling any attack, until Release. Hits still hurt but don't stagger
	public void Hold()
	{
		if (CurrentState == State.Dead) return;
		_knockback = Vector3.zero;
		Enter(State.Scripted);
	}

	public void Release()
	{
		if (CurrentState != State.Scripted) return;
		Enter(_player != null ? State.Chase : State.Idle);
	}

	public void Teleport(Vector3 position, Vector3 facing)
	{
		if (_agent.isOnNavMesh) _agent.Warp(position);
		else transform.position = position;
		facing.y = 0f;
		if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
	}

	public void SetTempo(float tempo)
	{
		Tempo = Mathf.Max(0.1f, tempo);
		_agent.speed = Data.MoveSpeed * Tempo;
	}

	// ---------------------------------------------------------------- reactions

	// staggers for at least `duration`; never shortens a stagger already running (e.g. a long perfect-dodge stagger)
	public void Stagger(float duration)
	{
		if (CurrentState == State.Dead || CurrentState == State.Scripted) return;
		float remaining = CurrentState == State.Stagger ? _stateTimer : 0f;
		Enter(State.Stagger);
		_stateTimer = Mathf.Max(remaining, duration);
	}

	private void OnDamaged(DamageInfo info)
	{
		_hitFlashTimer = 0.08f;
		FX.Spawn(Data.HitFX, info.Point, info.Direction);
		if (CurrentState == State.Scripted) return;

		// during a wind-up or lunge only a strong enough hit interrupts; the hit still does damage either way
		bool attacking = CurrentState == State.WindUp || CurrentState == State.Lunge;
		float threshold = attacking ? _attack.StaggerThreshold : Data.BaseStaggerThreshold;
		if (info.Stagger < threshold) return;

		Vector3 push = info.Direction;
		push.y = 0f;
		_knockback += push.normalized * (info.Knockback * Data.KnockbackScale);

		if (!_health.IsDead) Stagger(Data.StaggerTime);
	}

	private void OnDied()
	{
		CurrentState = State.Dead;
		if (_agent.isOnNavMesh) _agent.isStopped = true;
		_agent.enabled = false;
		foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
		FX.Spawn(Data.DeathFX, transform.position + Vector3.up * 1.2f, Vector3.up);
		EnemyLoot.Drop(Data, transform.position, _player != null ? _player.gameObject : null);
		StartCoroutine(DeathRoutine());
	}

	private IEnumerator DeathRoutine()
	{
		SetColor(Data.HitFlashColor);
		if (Data.CorpseTime > 0f)
		{
			// a model plays its death animation (EnemyAnimator) and lies there for a while first
			yield return new WaitForSeconds(0.1f);
			SetColor(Data.BodyColor);
			yield return new WaitForSeconds(Data.CorpseTime);
		}
		Vector3 start = Visual != null ? Visual.localScale : Vector3.one;
		for (float t = 0f; t < 0.3f; t += Time.deltaTime)
		{
			if (Visual != null) Visual.localScale = Vector3.Lerp(start, Vector3.zero, t / 0.3f);
			yield return null;
		}
		Destroy(gameObject);
	}

	// knockback is spread over a few frames through the agent so it respects the NavMesh
	private void ApplyKnockback()
	{
		if (_knockback.sqrMagnitude < 0.0001f || !_agent.isOnNavMesh) return;

		Vector3 step = Vector3.Lerp(Vector3.zero, _knockback, Time.deltaTime * 12f);
		_agent.Move(step);
		_knockback -= step;
	}

	// ---------------------------------------------------------------- visuals & helpers

	private void UpdateVisuals()
	{
		Color color = Data.BodyColor;
		float scale = 1f;

		if (CurrentState == State.WindUp)
		{
			float t = 1f - Mathf.Clamp01(_stateTimer / WindUpDuration);
			// flicker faster as the attack gets closer
			float pulse = 0.5f + 0.5f * Mathf.Sin(t * t * 40f);
			color = Color.Lerp(Data.BodyColor, _attack.TelegraphColor, Mathf.Lerp(0.4f, 1f, pulse * t + t * 0.5f));
			scale = 1f + 0.12f * t;
		}
		else if (CurrentState == State.Lunge)
		{
			color = _attack.TelegraphColor;
			scale = 1.12f;
		}

		if (_hitFlashTimer > 0f)
		{
			_hitFlashTimer -= Time.deltaTime;
			color = Data.HitFlashColor;
		}

		SetColor(color);
		if (Visual != null) Visual.localScale = _visualRestScale * scale;
	}

	private void FacePlayer(float speed)
	{
		if (_player == null) return;
		Vector3 toPlayer = FlatToPlayer();
		if (toPlayer.sqrMagnitude < 0.001f) return;
		transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * speed);
	}

	private Vector3 FlatToPlayer()
	{
		Vector3 toPlayer = _player.position - transform.position;
		toPlayer.y = 0f;
		return toPlayer;
	}

	private float DistanceToPlayer()
	{
		return FlatToPlayer().magnitude;
	}

	private void SetColor(Color color)
	{
		foreach (Renderer body in _renderers)
		{
			body.GetPropertyBlock(_block);
			_block.SetColor(_tintId, color);
			body.SetPropertyBlock(_block);
		}
	}

	private void OnDrawGizmosSelected()
	{
		if (Data == null) return;
		Gizmos.color = Data.Strike.TelegraphColor;
		Gizmos.DrawWireSphere(transform.position, Data.Strike.MaxRange);
		Gizmos.color = Data.Lunge.TelegraphColor;
		Gizmos.DrawWireSphere(transform.position, Data.Lunge.MinRange);
		Gizmos.DrawWireSphere(transform.position, Data.Lunge.MaxRange);
		Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
		Gizmos.DrawWireSphere(transform.position, Data.DetectionRange);
	}
}
