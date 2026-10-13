using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns waves of enemies at the spawn points. The next wave starts a few seconds after the last one is cleared.
// Killed enemies may drop a health or ammo pickup.
public class EnemySpawner : MonoBehaviour
{
	public GameObject EnemyPrefab;
	public Transform[] SpawnPoints;
	[Tooltip("Enemies per wave; the last entry repeats forever")]
	public int[] WaveSizes = { 3, 5, 7 };
	public float FirstWaveDelay = 1.5f;
	public float TimeBetweenWaves = 3f;

	[Header("Drops")]
	public Pickup HealthDropPrefab;
	public Pickup AmmoDropPrefab;
	[Range(0f, 1f)] public float HealthDropChance = 0.15f;
	[Range(0f, 1f)] public float AmmoDropChance = 0.35f;
	[Tooltip("Seconds before an uncollected drop disappears")]
	public float DropLifetime = 20f;

	public int CurrentWave { get; private set; }

	private readonly List<Health> _alive = new List<Health>();

	private void Start()
	{
		StartCoroutine(RunWaves());
	}

	private IEnumerator RunWaves()
	{
		yield return new WaitForSeconds(FirstWaveDelay);

		while (true)
		{
			SpawnWave();
			yield return new WaitUntil(() => _alive.Count == 0);
			yield return new WaitForSeconds(TimeBetweenWaves);
		}
	}

	private void SpawnWave()
	{
		if (EnemyPrefab == null || SpawnPoints == null || SpawnPoints.Length == 0) return;

		int size = WaveSizes[Mathf.Min(CurrentWave, WaveSizes.Length - 1)];
		CurrentWave++;

		for (int i = 0; i < size; i++)
		{
			Transform point = SpawnPoints[i % SpawnPoints.Length];
			// spread out enemies that share a spawn point
			Vector2 jitter = Random.insideUnitCircle * 1.5f;
			Vector3 position = point.position + new Vector3(jitter.x, 0f, jitter.y);

			GameObject enemy = Instantiate(EnemyPrefab, position, point.rotation);
			Health health = enemy.GetComponent<Health>();
			if (health == null) continue;

			_alive.Add(health);
			health.Died += () =>
			{
				_alive.Remove(health);
				SpawnDrop(health.transform.position);
			};
		}

		Debug.Log($"[EnemySpawner] Wave {CurrentWave}: {size} enemies");
	}

	// rolls ammo first, then health, so at most one drop per kill
	private void SpawnDrop(Vector3 position)
	{
		float roll = Random.value;
		Pickup prefab = null;
		if (roll < AmmoDropChance) prefab = AmmoDropPrefab;
		else if (roll < AmmoDropChance + HealthDropChance) prefab = HealthDropPrefab;
		if (prefab == null) return;

		Pickup drop = Instantiate(prefab, position, Quaternion.identity);
		drop.RespawnTime = 0f;
		Destroy(drop.gameObject, DropLifetime);
	}
}
