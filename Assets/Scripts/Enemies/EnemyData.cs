using UnityEngine;

[CreateAssetMenu(menuName = "Crimson Nights/Enemies/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
	[Header("Stats")]
	public float MaxHealth = 60f;
	public float MoveSpeed = 4.5f;
	public float Acceleration = 20f;
	public float DetectionRange = 25f;

	[Header("Attack")]
	[Tooltip("Distance at which the enemy stops and starts its wind-up")]
	public float AttackRange = 1.8f;
	[Tooltip("Extra distance the player can be at when the strike lands and still get hit")]
	public float AttackReachGrace = 0.6f;
	[Tooltip("Maximum angle from the enemy's forward for the strike to land")]
	public float AttackAngle = 70f;
	public float WindUpTime = 0.6f;
	public float AttackDamage = 15f;
	public float RecoverTime = 0.8f;

	[Header("Reactions")]
	public float StaggerTime = 0.25f;
	[Tooltip("Multiplier on incoming knockback distance")]
	public float KnockbackScale = 1f;

	[Header("Look")]
	public Color BodyColor = new Color(0.45f, 0.05f, 0.08f);
	public Color TelegraphColor = new Color(1f, 0.85f, 0.1f);
	public Color HitFlashColor = Color.white;
}
