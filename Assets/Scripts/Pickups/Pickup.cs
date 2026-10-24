using UnityEngine;

// Prototype health/ammo pickup. Walk through it to collect; ignored while the player is already full.
// Placed pickups respawn after RespawnTime; enemy drops set RespawnTime to 0 so they're used up.
[RequireComponent(typeof(SphereCollider))]
public class Pickup : MonoBehaviour
{
	public enum Kind { Health, Ammo }

	public Kind Type = Kind.Health;
	[Tooltip("Hit points restored. Health pickups only: ammo pickups give each firearm its FirearmData.AmmoPerPickup")]
	public int Amount = 25;
	[Tooltip("Seconds until it reappears after being collected. 0 = collected for good")]
	public float RespawnTime = 20f;
	[Tooltip("Visual child that bobs and spins (with its own children), and is hidden while respawning")]
	public Renderer Visual;
	[Tooltip("Tint the Visual with Color. Off for textured models")]
	public bool TintVisual = true;
	public Color Color = new Color(0.9f, 0.1f, 0.15f);
	public AudioClip PickupClip;

	[Header("Motion")]
	public float BobHeight = 0.12f;
	public float BobSpeed = 2f;
	public float SpinSpeed = 90f;

	private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

	private Collider _collider;
	private Vector3 _visualRestPos;
	private float _respawnAt = -1f;
	private float _phase;

	private void Awake()
	{
		_collider = GetComponent<SphereCollider>();
		_collider.isTrigger = true;
		_phase = Random.value * Mathf.PI * 2f;

		if (Visual != null)
		{
			_visualRestPos = Visual.transform.localPosition;
			if (!TintVisual) return;
			var block = new MaterialPropertyBlock();
			block.SetColor(BaseColorId, Color);
			Visual.SetPropertyBlock(block);
		}
	}

	private void Update()
	{
		if (_respawnAt >= 0f)
		{
			if (Time.time >= _respawnAt) SetAvailable(true);
			return;
		}

		if (Visual != null)
		{
			Transform visual = Visual.transform;
			visual.localPosition = _visualRestPos + Vector3.up * (Mathf.Sin(Time.time * BobSpeed + _phase) * BobHeight);
			visual.Rotate(0f, SpinSpeed * Time.deltaTime, 0f, Space.World);
		}
	}

	// Stay rather than Enter, so standing on it while full still collects it once you need it
	private void OnTriggerStay(Collider other)
	{
		if (_respawnAt >= 0f || !other.CompareTag("Player")) return;

		if (!TryApply(other.gameObject)) return;

		if (PickupClip != null) AudioSource.PlayClipAtPoint(PickupClip, transform.position);

		if (RespawnTime > 0f)
		{
			SetAvailable(false);
			_respawnAt = Time.time + RespawnTime;
		}
		else
		{
			Destroy(gameObject);
		}
	}

	private bool TryApply(GameObject player)
	{
		switch (Type)
		{
			case Kind.Health:
				Health health = player.GetComponent<Health>();
				if (health == null || health.IsDead || health.Current >= health.MaxHealth) return false;
				health.Heal(Amount);
				return true;

			case Kind.Ammo:
				// tops up every firearm, holstered or not; only used if at least one had room
				bool taken = false;
				foreach (Firearm firearm in player.GetComponents<Firearm>())
				{
					if (firearm.AddReserveAmmo(firearm.Data.AmmoPerPickup) > 0) taken = true;
				}
				return taken;
		}
		return false;
	}

	private void SetAvailable(bool available)
	{
		if (available) _respawnAt = -1f;
		_collider.enabled = available;
		if (Visual != null) Visual.gameObject.SetActive(available);
	}
}
