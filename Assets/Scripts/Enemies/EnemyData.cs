using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Crimson Nights/Enemies/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
	// One telegraphed attack. The enemy picks it when the player is between MinRange and MaxRange.
	[Serializable]
	public class Attack
	{
		public float MinRange = 0f;
		public float MaxRange = 1.8f;
		[Tooltip("Extra distance the player can be at when the attack lands and still get hit")]
		public float ReachGrace = 0.6f;
		[Tooltip("Maximum angle from the enemy's forward for the attack to land")]
		public float Angle = 70f;
		public float WindUpTime = 0.6f;
		public float Damage = 15f;
		public float RecoverTime = 0.8f;
		[Tooltip("Seconds after using this attack before it can be chosen again")]
		public float Cooldown = 0f;
		[Tooltip("A hit needs at least this much DamageInfo.Stagger to interrupt the wind-up")]
		public float StaggerThreshold = 10f;
		public Color TelegraphColor = new Color(1f, 0.85f, 0.1f);
	}

	[Header("Stats")]
	public float MaxHealth = 60f;
	public float MoveSpeed = 4.5f;
	public float Acceleration = 20f;
	public float DetectionRange = 25f;

	[Header("Attacks")]
	[Tooltip("Close-range swipe. Low stagger threshold, so a revolver shot interrupts it")]
	public Attack Strike = new Attack();
	[Tooltip("Mid-range dash. High stagger threshold: only a shotgun blast or heavy punch interrupts it")]
	public Attack Lunge = new Attack
	{
		MinRange = 4f,
		MaxRange = 7f,
		ReachGrace = 0.5f,
		Angle = 60f,
		WindUpTime = 0.9f,
		Damage = 20f,
		RecoverTime = 1.2f,
		Cooldown = 4f,
		StaggerThreshold = 40f,
		TelegraphColor = new Color(1f, 0.3f, 0.05f),
	};

	[Header("Lunge Dash")]
	[Tooltip("Metres travelled past the player's position when the lunge starts")]
	public float LungeOvershoot = 1f;
	[Tooltip("Longest the lunge can travel, in metres")]
	public float LungeMaxDistance = 8f;
	public float LungeSpeed = 18f;
	[Tooltip("The lunge hits the player when they are this close to the enemy")]
	public float LungeHitRadius = 1.2f;

	[Header("Reactions")]
	public float StaggerTime = 0.25f;
	[Tooltip("Stagger needed to interrupt the enemy outside an attack wind-up (0 = any hit)")]
	public float BaseStaggerThreshold = 0f;
	[Tooltip("Multiplier on incoming knockback distance")]
	public float KnockbackScale = 1f;

	[Header("Audio (optional)")]
	[Tooltip("Played when an attack lands on the player")]
	public AudioClip AttackHitClip;

	[Header("Look")]
	public Color BodyColor = new Color(0.45f, 0.05f, 0.08f);
	public Color HitFlashColor = Color.white;
}
