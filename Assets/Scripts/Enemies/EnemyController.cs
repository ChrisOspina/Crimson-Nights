using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Prototype melee enemy (Vampire Thrall stand-in): chase, then one of two telegraphed attacks, then recover.
// Strike: close-range swipe, interrupted by any revolver shot. Lunge: mid-range dash with a longer red-orange
// telegraph that only a high-stagger hit (shotgun, heavy punch) interrupts. Hits below the current stagger
// threshold still do damage but don't interrupt.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
	public enum State { Idle, Chase, WindUp, Lunge, Recover, Stagger, Dead }

	public EnemyData Data;
	[Tooltip("Visual child that gets tinted and scaled")]
	public Renderer Body;

	public State CurrentState { get; private set; } = State.Idle;

	private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

	private NavMeshAgent _agent;
	private Health _health;
	private Transform _player;
	private Health _playerHealth;
	private PlayerDodge _playerDodge;

	private float _stateTimer;
	private float _repathTimer;
	private float _hitFlashTimer;
	private Vector3 _knockback;
	private Vector3 _bodyRestScale;
	private MaterialPropertyBlock _block;

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

		if (Body != null) _bodyRestScale = Body.transform.localScale;
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
				if (playerAlive && DistanceToPlayer() <= Data.DetectionRange) Enter(State.Chase);
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
			case State.WindUp: _stateTimer = _attack.WindUpTime; break;
			case State.Recover: _stateTimer = _attack.RecoverTime; break;
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
		Enter(State.WindUp);
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
		return true;
	}

	// ---------------------------------------------------------------- reactions

	// staggers for at least `duration`; never shortens a stagger already running (e.g. a long perfect-dodge stagger)
	public void Stagger(float duration)
	{
		if (CurrentState == State.Dead) return;
		float remaining = CurrentState == State.Stagger ? _stateTimer : 0f;
		Enter(State.Stagger);
		_stateTimer = Mathf.Max(remaining, duration);
	}

	private void OnDamaged(DamageInfo info)
	{
		_hitFlashTimer = 0.08f;

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
		StartCoroutine(DeathRoutine());
	}

	private IEnumerator DeathRoutine()
	{
		SetColor(Data.HitFlashColor);
		Vector3 start = Body != null ? Body.transform.localScale : Vector3.one;
		for (float t = 0f; t < 0.3f; t += Time.deltaTime)
		{
			if (Body != null) Body.transform.localScale = Vector3.Lerp(start, Vector3.zero, t / 0.3f);
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
			float t = 1f - Mathf.Clamp01(_stateTimer / _attack.WindUpTime);
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
		if (Body != null) Body.transform.localScale = _bodyRestScale * scale;
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
		if (Body == null) return;
		Body.GetPropertyBlock(_block);
		_block.SetColor(BaseColorId, color);
		Body.SetPropertyBlock(_block);
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
