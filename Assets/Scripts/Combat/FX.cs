using UnityEngine;

// One-shot effects: drops a copy of an effect prefab (a ParticleSystem with Stop Action Destroy, so it cleans
// itself up) at a point, its +Z turned along `direction`.
public static class FX
{
	public static void Spawn(GameObject prefab, Vector3 position, Vector3 direction)
	{
		if (prefab == null) return;
		Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
		Object.Instantiate(prefab, position, rotation);
	}
}
