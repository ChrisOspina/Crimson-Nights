using System.Collections.Generic;
using UnityEngine;

// A one-off fight set off by the player walking into this trigger: slams and bars the listed doors, spawns
// one enemy per spawn point, and once they're all dead unbars the doors and reveals the reward (a key
// pickup, say), optionally moved to where the last enemy fell.
[RequireComponent(typeof(Collider))]
public class Encounter : MonoBehaviour
{
	public GameObject EnemyPrefab;
	public Transform[] SpawnPoints;
	[Tooltip("Seconds between the doors slamming and the enemies appearing")]
	public float SpawnDelay = 0.6f;

	[Header("Doors")]
	public Door[] BarDoors;
	public string BarredMessage = "The door won't budge.";

	[Header("Messages")]
	public string StartMessage;
	public string ClearedMessage;

	[Header("Reward")]
	[Tooltip("Inactive until the encounter is cleared")]
	public GameObject Reward;
	public bool MoveRewardToLastKill = true;

	public bool Started { get; private set; }
	public bool Cleared { get; private set; }

	private readonly List<Health> _alive = new List<Health>();
	private PlayerInteractor _player;
	private Vector3 _lastKillPosition;
	private bool _anyKilled;

	private void Awake()
	{
		GetComponent<Collider>().isTrigger = true;
		if (Reward != null) Reward.SetActive(false);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (Started || !other.CompareTag("Player")) return;
		Started = true;
		GetComponent<Collider>().enabled = false;
		_player = other.GetComponent<PlayerInteractor>();

		foreach (Door door in BarDoors)
		{
			if (door != null) door.Close(true, BarredMessage);
		}
		if (_player != null) _player.ShowMessage(StartMessage);
		Invoke(nameof(Spawn), SpawnDelay);
	}

	private void Spawn()
	{
		foreach (Transform point in SpawnPoints)
		{
			GameObject enemy = Instantiate(EnemyPrefab, point.position, point.rotation);
			Health health = enemy.GetComponent<Health>();
			if (health == null) continue;

			_alive.Add(health);
			health.Died += () => OnEnemyDied(health);
		}
		if (_alive.Count == 0) Clear();
	}

	private void OnEnemyDied(Health health)
	{
		_alive.Remove(health);
		_lastKillPosition = health.transform.position;
		_anyKilled = true;
		if (_alive.Count == 0) Clear();
	}

	private void Clear()
	{
		if (Cleared) return;
		Cleared = true;

		foreach (Door door in BarDoors)
		{
			if (door != null) door.Unlock();
		}
		if (Reward != null)
		{
			if (MoveRewardToLastKill && _anyKilled) Reward.transform.position = _lastKillPosition;
			Reward.SetActive(true);
		}
		if (_player != null) _player.ShowMessage(ClearedMessage);
	}
}
