using UnityEngine;

[CreateAssetMenu(menuName = "Crimson Nights/Weapons/Revolver Data", fileName = "Revolver")]
public class RevolverData : ScriptableObject
{
	[Header("Damage")]
	public float Damage = 25f;
	[Tooltip("Knockback applied to enemies, in metres")]
	public float Knockback = 0.3f;
	public float Range = 100f;
	[Tooltip("What the shot can hit. Should exclude the Player layer")]
	public LayerMask HitMask = ~(1 << 8);

	[Header("Handling")]
	[Tooltip("Minimum seconds between shots")]
	public float FireInterval = 0.25f;
	public int MagazineSize = 6;
	public float ReloadTime = 1.4f;

	[Header("Ammo")]
	[Tooltip("Spare rounds the player starts with (not counting the loaded magazine)")]
	public int StartingReserveAmmo = 24;
	[Tooltip("Most spare rounds the player can carry; ammo pickups top up to this")]
	public int MaxReserveAmmo = 36;

	[Header("Feedback")]
	[Tooltip("Unlit material for tracers, muzzle flash and impacts")]
	public Material FxMaterial;
	public float TracerDuration = 0.05f;
	public float KickDistance = 0.08f;
	public float KickAngle = 8f;

	[Header("Audio (optional)")]
	public AudioClip FireClip;
	public AudioClip ReloadClip;
	public AudioClip EmptyClip;
	[Tooltip("Played in 3D where the bullet lands")]
	public AudioClip ImpactClip;
}
