using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Plays an enemy model's animations from EnemyController's state, so the controller itself doesn't know
// about models. The Animator needs states named Locomotion (a blend tree on the Speed parameter), Attack_L,
// Attack_R, Attack_LR, Hit and Death; attack states use the AttackSpeed parameter as their speed, which is
// set so the blow in the clip lands as the wind-up ends.
[RequireComponent(typeof(EnemyController))]
public class EnemyAnimator : MonoBehaviour
{
	public Animator Animator;
	[Tooltip("How far through a Strike clip (0-1) the blow lands")]
	[Range(0.1f, 0.9f)] public float StrikeImpact = 0.5f;
	[Tooltip("How far through the Lunge clip (0-1) the blow lands")]
	[Range(0.1f, 0.9f)] public float LungeImpact = 0.5f;
	public float CrossFadeTime = 0.12f;

	private static readonly int SpeedId = Animator.StringToHash("Speed");
	private static readonly int AttackSpeedId = Animator.StringToHash("AttackSpeed");

	private EnemyController _enemy;
	private NavMeshAgent _agent;
	private EnemyController.State _lastState;
	private bool _leftNext = true;
	private readonly Dictionary<string, float> _clipLengths = new Dictionary<string, float>();

	private void Awake()
	{
		_enemy = GetComponent<EnemyController>();
		_agent = GetComponent<NavMeshAgent>();
		if (Animator == null) Animator = GetComponentInChildren<Animator>();
		if (Animator == null) return;

		// the agent moves the enemy; the clips only animate the body
		Animator.applyRootMotion = false;
		if (Animator.runtimeAnimatorController != null)
		{
			foreach (AnimationClip clip in Animator.runtimeAnimatorController.animationClips) _clipLengths[clip.name] = clip.length;
		}
		_lastState = _enemy.CurrentState;
	}

	// after EnemyController.Update, so a state change shows the same frame
	private void LateUpdate()
	{
		if (Animator == null) return;

		EnemyController.State state = _enemy.CurrentState;
		float speed = state == EnemyController.State.Lunge ? _enemy.Data.LungeSpeed : (_agent.enabled ? _agent.velocity.magnitude : 0f);
		Animator.SetFloat(SpeedId, speed);

		if (state == _lastState) return;
		_lastState = state;

		switch (state)
		{
			case EnemyController.State.WindUp:
				PlayAttack();
				break;
			case EnemyController.State.Stagger:
				Play("Hit", 1f);
				break;
			case EnemyController.State.Dead:
				Play("Death", 1f);
				break;
			case EnemyController.State.Idle:
			case EnemyController.State.Chase:
				Play("Locomotion", 1f);
				break;
			// Lunge and Recover let the attack clip play on
		}
	}

	// Strikes alternate hands; the clip is timed so its impact point lines up with the end of the wind-up
	private void PlayAttack()
	{
		EnemyData.Attack attack = _enemy.CurrentAttack;
		bool lunge = attack == _enemy.Data.Lunge;
		string clip = lunge ? "Attack_LR" : (_leftNext ? "Attack_L" : "Attack_R");
		if (!lunge) _leftNext = !_leftNext;

		float speed = 1f;
		if (_clipLengths.TryGetValue(clip, out float length) && attack.WindUpTime > 0f)
		{
			speed = length * (lunge ? LungeImpact : StrikeImpact) / attack.WindUpTime;
		}
		Play(clip, speed);
	}

	private void Play(string state, float speed)
	{
		Animator.SetFloat(AttackSpeedId, speed);
		Animator.CrossFadeInFixedTime(state, CrossFadeTime, 0, 0f);
	}
}
