using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Crimson Nights/Weapons/Melee Data", fileName = "RelicBlade")]
public class MeleeData : ScriptableObject
{
	[Serializable]
	public class Attack
	{
		public float Damage = 20f;
		[Tooltip("How far in front of the camera the hit sphere reaches")]
		public float Reach = 2.2f;
		public float Radius = 0.8f;
		[Tooltip("Knockback applied to enemies, in metres")]
		public float Knockback = 0.5f;
		[Tooltip("How hard a hit interrupts enemies (compared to EnemyData stagger thresholds)")]
		public float Stagger = 15f;
		[Tooltip("Swing animation length")]
		public float SwingTime = 0.18f;
		[Tooltip("Seconds into the swing when the hit is checked")]
		public float HitDelay = 0.06f;
		[Tooltip("Seconds after the swing before another attack")]
		public float Cooldown = 0.45f;
	}

	[Tooltip("What melee can hit. Should be the Enemy layer")]
	public LayerMask HitMask = 1 << 9;
	[Tooltip("Holding the melee button at least this long turns the attack into a heavy")]
	public float HeavyHoldTime = 0.3f;

	public Attack Light = new Attack();
	public Attack Heavy = new Attack
	{
		Damage = 55f,
		Reach = 2.6f,
		Radius = 1.2f,
		Knockback = 2.5f,
		Stagger = 60f,
		SwingTime = 0.25f,
		HitDelay = 0.1f,
		Cooldown = 1.0f,
	};

	[Header("Audio (optional)")]
	public AudioClip SwingClip;
	public AudioClip HitClip;
}
