using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Prototype melee enemy (Vampire Thrall stand-in): chase, telegraphed wind-up, strike, recover.
// Any hit staggers it, so shooting during the wind-up interrupts the attack.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
	public enum State { Idle, Chase, WindUp, Recover, Stagger, Dead }

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

	private void Awake()
	{
		_agent = GetComponent<NavMeshAgent>();
		_health = GetComponent<Health>();
		_block = new MaterialPropertyBlock();

		_health.ResetHealth(Data.MaxHealth);
		_agent.speed = Data.MoveSpeed;
		_agent.acceleration = Data.Acceleration;
		_agent.stoppingDistance = Data.AttackRange * 0.8f;

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
				if (DistanceToPlayer() <= Data.AttackRange) Enter(State.WindUp);
				break;

			case State.WindUp:
				FacePlayer(6f);
				if (_stateTimer <= 0f)
				{
					Strike();
					// a perfect dodge staggers us from inside Strike()
					if (CurrentState == State.WindUp) Enter(State.Recover);
				}
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
			case State.WindUp: _stateTimer = Data.WindUpTime; break;
			case State.Recover: _stateTimer = Data.RecoverTime; break;
			case State.Stagger: _stateTimer = Data.StaggerTime; break;
			case State.Chase: _repathTimer = 0f; break;
		}
	}

	private void Strike()
	{
		if (_player == null || _playerHealth == null) return;

		float reach = Data.AttackRange + Data.AttackReachGrace;
		if (_playerDodge != null && _playerDodge.TryPerfectDodge(this, reach, Data.AttackAngle)) return;

		Vector3 toPlayer = _player.position - transform.position;
		toPlayer.y = 0f;
		bool inRange = toPlayer.magnitude <= reach;
		bool inFront = Vector3.Angle(transform.forward, toPlayer) <= Data.AttackAngle;

		if (inRange && inFront)
		{
			_playerHealth.TakeDamage(new DamageInfo(Data.AttackDamage, _player.position, toPlayer.normalized, 0f, gameObject));
			if (Data.AttackHitClip != null) AudioSource.PlayClipAtPoint(Data.AttackHitClip, transform.position);
		}
	}

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

	private void UpdateVisuals()
	{
		Color color = Data.BodyColor;
		float scale = 1f;

		if (CurrentState == State.WindUp)
		{
			float t = 1f - Mathf.Clamp01(_stateTimer / Data.WindUpTime);
			// flicker faster as the strike gets closer
			float pulse = 0.5f + 0.5f * Mathf.Sin(t * t * 40f);
			color = Color.Lerp(Data.BodyColor, Data.TelegraphColor, Mathf.Lerp(0.4f, 1f, pulse * t + t * 0.5f));
			scale = 1f + 0.12f * t;
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
		Vector3 toPlayer = _player.position - transform.position;
		toPlayer.y = 0f;
		if (toPlayer.sqrMagnitude < 0.001f) return;
		transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * speed);
	}

	private float DistanceToPlayer()
	{
		Vector3 toPlayer = _player.position - transform.position;
		toPlayer.y = 0f;
		return toPlayer.magnitude;
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
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, Data.AttackRange);
		Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
		Gizmos.DrawWireSphere(transform.position, Data.DetectionRange);
	}
}
