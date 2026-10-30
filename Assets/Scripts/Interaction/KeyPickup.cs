using UnityEngine;

// A key lying in the world. Bobs and turns so it catches the eye; taking it adds it to the player's KeyRing.
public class KeyPickup : Interactable
{
	public KeyData Key;
	public AudioClip PickupClip;
	[Tooltip("Moved by the bob and spin; the collider should stay on the root")]
	public Transform Visual;
	public float BobHeight = 0.06f;
	public float BobSpeed = 2f;
	public float SpinSpeed = 60f;

	public override string Prompt => "Take " + (Key != null ? Key.DisplayName : "key");

	private Vector3 _visualRestPosition;

	private void Awake()
	{
		if (Visual != null) _visualRestPosition = Visual.localPosition;
	}

	private void Update()
	{
		if (Visual == null) return;
		Visual.localPosition = _visualRestPosition + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight);
		Visual.Rotate(0f, SpinSpeed * Time.deltaTime, 0f, Space.World);
	}

	public override void Interact(PlayerInteractor player)
	{
		if (player.Keys != null) player.Keys.Add(Key);
		player.PlaySound(PickupClip);
		player.ShowMessage("Picked up the " + (Key != null ? Key.DisplayName : "key"));
		Destroy(gameObject);
	}
}
