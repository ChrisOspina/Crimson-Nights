using UnityEngine;

[CreateAssetMenu(menuName = "Crimson Nights/Player/Dodge Data", fileName = "Dodge")]
public class DodgeData : ScriptableObject
{
	[Header("Dash")]
	[Tooltip("Metres covered by one dodge")]
	public float Distance = 4f;
	[Tooltip("Seconds the dash lasts")]
	public float Duration = 0.2f;
	[Tooltip("Seconds from the start of one dodge until the next is allowed")]
	public float Cooldown = 0.6f;
	[Tooltip("Seconds from the start of the dodge during which damage is ignored")]
	public float InvulnerableTime = 0.25f;

	[Header("Perfect Dodge")]
	[Tooltip("A strike landing within this many seconds of starting a dodge counts as a perfect dodge")]
	public float PerfectWindow = 0.2f;
	[Tooltip("Time.timeScale during the slow motion")]
	public float SlowMotionScale = 0.3f;
	[Tooltip("Real-time seconds of slow motion")]
	public float SlowMotionDuration = 0.5f;
	[Tooltip("How long the attacker is staggered after being perfect-dodged")]
	public float AttackerStaggerTime = 1.2f;
	[Tooltip("Seconds after a perfect dodge during which the player deals bonus damage")]
	public float CounterWindow = 1.5f;
	public float CounterDamageMultiplier = 2f;

	[Header("Audio (optional)")]
	public AudioClip DodgeClip;
	public AudioClip PerfectDodgeClip;
}
