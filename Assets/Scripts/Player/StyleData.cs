using UnityEngine;

[CreateAssetMenu(menuName = "Crimson Nights/Player/Style Data", fileName = "Style")]
public class StyleData : ScriptableObject
{
	[Header("Ranks")]
	public string[] RankLetters = { "D", "C", "B", "A", "S", "SS", "SSS" };
	public string[] RankNames = { "Dreadful", "Capable", "Brutal", "Astonishing", "Stylish", "Supernatural", "Spectacular" };
	[Tooltip("Style points needed to climb one rank")]
	public float PointsPerRank = 100f;

	[Header("Gains")]
	public float GunHitPoints = 8f;
	public float MeleeHitPoints = 12f;
	[Tooltip("Extra points when a combo finisher or heavy punch lands")]
	public float FinisherBonus = 25f;
	[Tooltip("Extra points for hitting with a different weapon than the last hit (fists count as one weapon)")]
	public float WeaponSwitchBonus = 15f;
	public float KillPoints = 25f;
	[Tooltip("Extra points per additional kill inside the multi-kill window")]
	public float MultiKillBonus = 25f;
	[Tooltip("Seconds between kills for them to chain into a multi-kill")]
	public float MultiKillWindow = 2f;
	public float PerfectDodgePoints = 50f;
	[Tooltip("Gains are multiplied by this during a perfect dodge's counter window")]
	public float CounterMultiplier = 1.5f;

	[Header("Repetition")]
	[Tooltip("How many recent attacks are remembered when checking for repetition")]
	public int RepeatMemory = 5;
	[Tooltip("Hit points are reduced by this fraction for each time the same attack appears in recent memory")]
	[Range(0f, 1f)] public float RepeatPenalty = 0.25f;
	[Tooltip("Hit points never drop below this fraction, however repetitive")]
	[Range(0f, 1f)] public float MinRepeatMultiplier = 0.2f;

	[Header("Losses")]
	[Tooltip("Points lost when the player takes damage")]
	public float DamageTakenLoss = 80f;
	[Tooltip("Seconds without gaining style before the meter starts draining")]
	public float DecayDelay = 3f;
	[Tooltip("Points drained per second once decay starts")]
	public float DecayRate = 12f;
	[Tooltip("Extra drain per second for each rank above D, so high ranks need constant pressure")]
	public float DecayRatePerRank = 6f;
}
