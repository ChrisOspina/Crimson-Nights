using UnityEngine;

// Everything a Health component needs to know about a hit.
public struct DamageInfo
{
	public float Amount;
	public Vector3 Point;
	// normalised direction the hit travelled in (attacker -> target)
	public Vector3 Direction;
	// knockback distance in metres (enemies) / impulse strength
	public float Knockback;
	public GameObject Source;
	// how hard the hit interrupts; compared against the target's stagger threshold (0 = only staggers unarmoured targets)
	public float Stagger;

	public DamageInfo(float amount, Vector3 point, Vector3 direction, float knockback, GameObject source, float stagger = 0f)
	{
		Amount = amount;
		Point = point;
		Direction = direction;
		Knockback = knockback;
		Source = source;
		Stagger = stagger;
	}
}
