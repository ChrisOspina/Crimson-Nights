using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Crimson Nights/Weapons/Melee Data", fileName = "Fists")]
public class MeleeData : ScriptableObject
{
	[Serializable]
	public class Attack
	{
		[Tooltip("Shown by the style meter; repeating the same attack scores less")]
		public string Name = "Jab";
		public float Damage = 20f;
		[Tooltip("How far in front of the camera the hit sphere reaches")]
		public float Reach = 2f;
		public float Radius = 0.8f;
		[Tooltip("Knockback applied to enemies, in metres")]
		public float Knockback = 0.5f;
		[Tooltip("How hard a hit interrupts enemies (compared to EnemyData stagger thresholds)")]
		public float Stagger = 15f;
		[Tooltip("Punch animation length")]
		public float SwingTime = 0.16f;
		[Tooltip("Seconds into the punch when the hit is checked")]
		public float HitDelay = 0.05f;
		[Tooltip("Seconds after the punch before another attack")]
		public float Cooldown = 0.12f;
		[Tooltip("Where the fist ends up at full extension, as an offset from its rest position")]
		public Vector3 PunchOffset = new Vector3(0.12f, 0.1f, 0.45f);
	}

	[Tooltip("What melee can hit. Should be the Enemy layer")]
	public LayerMask HitMask = 1 << 9;
	[Tooltip("Holding the melee button at least this long turns the attack into a heavy")]
	public float HeavyHoldTime = 0.3f;

	[Header("Combo")]
	[Tooltip("Light punches play in this order while the chain is kept going. The last one is the finisher")]
	public Attack[] LightCombo =
	{
		new Attack(),
		new Attack { Name = "Cross", PunchOffset = new Vector3(0.2f, 0.12f, 0.5f) },
		new Attack
		{
			Name = "Hook",
			Damage = 35f,
			Reach = 2.1f,
			Radius = 1f,
			Knockback = 1.5f,
			Stagger = 30f,
			SwingTime = 0.24f,
			HitDelay = 0.09f,
			Cooldown = 0.6f,
			PunchOffset = new Vector3(0.32f, 0.16f, 0.4f),
		},
	};
	[Tooltip("Seconds after a punch's cooldown in which the next light punch continues the chain")]
	public float ComboWindow = 0.5f;
	[Tooltip("A press made this long before the fist is free is remembered and thrown as soon as it is")]
	public float InputBufferTime = 0.25f;

	[Header("Heavy")]
	public Attack Heavy = new Attack
	{
		Name = "Haymaker",
		Damage = 55f,
		Reach = 2.3f,
		Radius = 1.2f,
		Knockback = 2.5f,
		Stagger = 60f,
		SwingTime = 0.25f,
		HitDelay = 0.1f,
		Cooldown = 1.0f,
		PunchOffset = new Vector3(0.22f, 0.14f, 0.55f),
	};

	[Header("Audio (optional)")]
	[Tooltip("Whoosh played as a punch is thrown")]
	public AudioClip SwingClip;
	[Tooltip("Played when a light punch connects")]
	public AudioClip HitClip;
	[Tooltip("Played when a finisher or heavy punch connects. Falls back to HitClip")]
	public AudioClip HeavyHitClip;
}
