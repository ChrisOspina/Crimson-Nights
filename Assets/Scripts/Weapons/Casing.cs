using UnityEngine;

// A spent round thrown out of a firearm's ejection port. Tumbles and settles, then shrinks away.
// Purely cosmetic: it sits on the Player layer, so firearms and melee never hit it.
[RequireComponent(typeof(Rigidbody))]
public class Casing : MonoBehaviour
{
	[Tooltip("Seconds before it's removed")]
	public float Lifetime = 4f;
	[Tooltip("Seconds at the end of its life spent shrinking out of view")]
	public float ShrinkTime = 0.4f;

	private float _removeAt;
	private Vector3 _scale;

	private void Awake()
	{
		_removeAt = Time.time + Lifetime;
		_scale = transform.localScale;
	}

	private void Update()
	{
		float left = _removeAt - Time.time;
		if (left <= 0f)
		{
			Destroy(gameObject);
			return;
		}
		if (left < ShrinkTime) transform.localScale = _scale * (left / ShrinkTime);
	}
}
