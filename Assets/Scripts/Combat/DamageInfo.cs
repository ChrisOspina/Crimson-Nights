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

	public DamageInfo(float amount, Vector3 point, Vector3 direction, float knockback, GameObject source)
	{
		Amount = amount;
		Point = point;
		Direction = direction;
		Knockback = knockback;
		Source = source;
	}
}
