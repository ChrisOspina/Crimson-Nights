using UnityEngine;

// What an enemy leaves behind, from its EnemyData drop table. EnemyController calls Drop when it dies, so every
// kill pays out however the enemy was spawned (waves, encounters, ambushes, the Countess's summons).
// One roll picks ammo or health, weighted towards whatever the player is shorter of; sometimes it drops both,
// and now and then the health is the big kit. Drops are used up (no respawn) and vanish if left too long.
public static class EnemyLoot
{
	// a little weight on each side even when the player is full, so drops never become fully predictable
	private const float BaseWeight = 0.25f;

	public static void Drop(EnemyData data, Vector3 position, GameObject player)
	{
		if (data.AmmoDrop == null && data.HealthDrop == null) return;
		if (Random.value > data.DropChance) return;

		Pickup health = data.HealthDrop;
		if (data.MajorHealthDrop != null && Random.value < data.MajorHealthChance) health = data.MajorHealthDrop;

		if (Random.value < data.BothChance)
		{
			// side by side, so they don't sit inside each other
			Vector3 side = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.right * 0.45f;
			Spawn(data, data.AmmoDrop, position + side);
			Spawn(data, health, position - side);
			return;
		}

		float healthWeight = BaseWeight + HealthNeed(player);
		float ammoWeight = BaseWeight + AmmoNeed(player);
		bool giveHealth = Random.value * (healthWeight + ammoWeight) < healthWeight;
		Spawn(data, giveHealth ? health : data.AmmoDrop, position + Jitter());
	}

	// 0 at full health, 1 at none
	private static float HealthNeed(GameObject player)
	{
		Health health = player != null ? player.GetComponent<Health>() : null;
		return health != null ? 1f - health.Normalized : 0.5f;
	}

	// 0 with every reserve full, 1 with every reserve empty
	private static float AmmoNeed(GameObject player)
	{
		if (player == null) return 0.5f;
		int reserve = 0, max = 0;
		foreach (Firearm firearm in player.GetComponents<Firearm>())
		{
			reserve += firearm.ReserveAmmo;
			max += firearm.MaxReserveAmmo;
		}
		return max > 0 ? 1f - reserve / (float)max : 0.5f;
	}

	private static Vector3 Jitter()
	{
		Vector2 offset = Random.insideUnitCircle * 0.3f;
		return new Vector3(offset.x, 0f, offset.y);
	}

	private static void Spawn(EnemyData data, Pickup prefab, Vector3 position)
	{
		if (prefab == null) return;
		Pickup drop = Object.Instantiate(prefab, position, Quaternion.identity);
		drop.RespawnTime = 0f;
		Object.Destroy(drop.gameObject, data.DropLifetime);
	}
}
