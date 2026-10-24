using UnityEngine;

// Tuning for one firearm (the revolver, the relic shotgun). A shotgun is just several pellets with spread.
[CreateAssetMenu(menuName = "Crimson Nights/Weapons/Firearm Data", fileName = "Firearm")]
public class FirearmData : ScriptableObject
{
	[Tooltip("Shown on the HUD")]
	public string DisplayName = "Revolver";

	[Header("Damage")]
	[Tooltip("Damage per pellet")]
	public float Damage = 25f;
	[Tooltip("Knockback applied to enemies per pellet, in metres")]
	public float Knockback = 0.3f;
	[Tooltip("How hard each pellet interrupts enemies (compared to EnemyData stagger thresholds). Pellets that hit the same enemy add up")]
	public float Stagger = 15f;
	public float Range = 100f;
	[Tooltip("What the shot can hit. Should exclude the Player layer")]
	public LayerMask HitMask = ~(1 << 8);

	[Header("Spread")]
	[Tooltip("Rays per shot. 1 for the revolver")]
	[Min(1)] public int PelletCount = 1;
	[Tooltip("Half-angle of the spread cone, in degrees")]
	public float SpreadAngle = 0f;

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
	[Tooltip("Spare rounds each ammo pickup gives this weapon")]
	public int AmmoPerPickup = 12;

	[Header("Feedback")]
	[Tooltip("Unlit material for tracers, muzzle flash and impacts")]
	public Material FxMaterial;
	public float TracerDuration = 0.05f;
	public float KickDistance = 0.08f;
	public float KickAngle = 8f;

	[Header("Rounds (optional)")]
	[Tooltip("Spent round (with a Rigidbody and Casing) thrown from the Firearm's EjectPort after each shot")]
	public GameObject CasingPrefab;
	[Tooltip("HUD icon for one round in the magazine. Without it the HUD shows the magazine as a number")]
	public Sprite RoundIcon;
	[Tooltip("Sideways and upward speed of ejected casings, in m/s")]
	public Vector2 EjectSpeed = new Vector2(2f, 1.5f);

	[Header("Audio (optional)")]
	public AudioClip FireClip;
	public AudioClip ReloadClip;
	public AudioClip EmptyClip;
	[Tooltip("Played in 3D where the shot lands (once per shot)")]
	public AudioClip ImpactClip;
}
