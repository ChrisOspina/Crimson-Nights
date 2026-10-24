using System;
using System.Collections.Generic;
using UnityEngine;

// Scores combat performance from D to SSS. Hits, kills, multi-kills, combo finishers, perfect dodges and
// switching weapons between hits raise it; repeating the same attack scores less, taking damage knocks it
// down and it drains when the player stops fighting. It only rewards skill and gates nothing.
[RequireComponent(typeof(Health))]
public class StyleMeter : MonoBehaviour
{
	public StyleData Data;

	public float Points { get; private set; }
	public int RankCount => Data.RankLetters.Length;
	// -1 while the meter is empty
	public int Rank => Points <= 0f ? -1 : Mathf.Min(Mathf.FloorToInt(Points / Data.PointsPerRank), RankCount - 1);
	// progress through the current rank, 0..1
	public float RankProgress => Rank < 0 ? 0f : Mathf.Clamp01((Points - Rank * Data.PointsPerRank) / Data.PointsPerRank);
	public string RankLetter => Rank < 0 ? "" : Data.RankLetters[Rank];
	public string RankName => Rank < 0 || Rank >= Data.RankNames.Length ? "" : Data.RankNames[Rank];

	public event Action<int> RankChanged;
	// a notable bonus worth calling out on the HUD ("FINISHER", "DOUBLE KILL"...)
	public event Action<string> Callout;

	private Health _health;
	private Firearm[] _firearms;
	private MeleeWeapon _melee;
	private PlayerDodge _dodge;

	private readonly Queue<string> _recentActions = new Queue<string>();
	private string _lastWeapon;
	private float _lastGainTime = -100f;
	private float _lastKillTime = -100f;
	private int _killChain;
	private int _lastRank = -1;

	private void Awake()
	{
		_health = GetComponent<Health>();
		_firearms = GetComponents<Firearm>();
		_melee = GetComponent<MeleeWeapon>();
		_dodge = GetComponent<PlayerDodge>();
	}

	private void OnEnable()
	{
		_health.Damaged += OnDamaged;
		foreach (Firearm firearm in _firearms) firearm.Hit += OnHit;
		if (_melee != null) _melee.Hit += OnHit;
		if (_dodge != null) _dodge.PerfectDodged += OnPerfectDodged;
	}

	private void OnDisable()
	{
		_health.Damaged -= OnDamaged;
		foreach (Firearm firearm in _firearms) firearm.Hit -= OnHit;
		if (_melee != null) _melee.Hit -= OnHit;
		if (_dodge != null) _dodge.PerfectDodged -= OnPerfectDodged;
	}

	private void Update()
	{
		if (Points > 0f && Time.time > _lastGainTime + Data.DecayDelay)
		{
			float rate = Data.DecayRate + Data.DecayRatePerRank * Mathf.Max(Rank, 0);
			SetPoints(Points - rate * Time.deltaTime);
		}
	}

	private void OnHit(AttackHit hit)
	{
		float gain = (hit.IsMelee ? Data.MeleeHitPoints : Data.GunHitPoints) * RepeatMultiplier(hit.Action);
		Remember(hit.Action);

		if (hit.IsFinisher)
		{
			gain += Data.FinisherBonus;
			Callout?.Invoke(hit.Action.ToUpperInvariant());
		}

		if (_lastWeapon != null && hit.Weapon != _lastWeapon)
		{
			gain += Data.WeaponSwitchBonus;
			if (!hit.IsFinisher) Callout?.Invoke("SWITCH UP");
		}
		_lastWeapon = hit.Weapon;

		if (hit.Killed) gain += KillGain();

		Gain(gain);
	}

	private float KillGain()
	{
		_killChain = Time.time <= _lastKillTime + Data.MultiKillWindow ? _killChain + 1 : 1;
		_lastKillTime = Time.time;

		if (_killChain < 2) return Data.KillPoints;

		Callout?.Invoke(_killChain == 2 ? "DOUBLE KILL" : _killChain == 3 ? "TRIPLE KILL" : "MULTI KILL");
		return Data.KillPoints + Data.MultiKillBonus * (_killChain - 1);
	}

	private void OnPerfectDodged(EnemyController attacker)
	{
		Callout?.Invoke("PERFECT DODGE");
		Gain(Data.PerfectDodgePoints);
	}

	private void OnDamaged(DamageInfo info)
	{
		SetPoints(Points - Data.DamageTakenLoss);
	}

	// 1 for a fresh attack, lower each time it already appears among the recent ones
	private float RepeatMultiplier(string action)
	{
		int repeats = 0;
		foreach (string recent in _recentActions)
		{
			if (recent == action) repeats++;
		}
		return Mathf.Max(Data.MinRepeatMultiplier, 1f - Data.RepeatPenalty * repeats);
	}

	private void Remember(string action)
	{
		_recentActions.Enqueue(action);
		while (_recentActions.Count > Data.RepeatMemory) _recentActions.Dequeue();
	}

	private void Gain(float amount)
	{
		if (_dodge != null && _dodge.IsCounterWindow) amount *= Data.CounterMultiplier;
		_lastGainTime = Time.time;
		SetPoints(Points + amount);
	}

	private void SetPoints(float points)
	{
		// capped just under the top so the last rank still shows a full bar
		Points = Mathf.Clamp(points, 0f, RankCount * Data.PointsPerRank - 0.01f);

		int rank = Rank;
		if (rank != _lastRank)
		{
			_lastRank = rank;
			RankChanged?.Invoke(rank);
		}
	}
}
