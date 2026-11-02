using UnityEngine;

// Totals for one run of the game, kept across the scene reloads that follow a death: time since the run
// began, deaths, and the best style rank reached. Reset when play starts and by EndScreen's Play Again.
public static class RunStats
{
	public static float StartTime { get; private set; }
	public static int Deaths { get; private set; }
	// -1 until the style meter scores anything
	public static int BestStyleRank { get; private set; } = -1;

	public static float Elapsed => Time.realtimeSinceStartup - StartTime;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	public static void Begin()
	{
		StartTime = Time.realtimeSinceStartup;
		Deaths = 0;
		BestStyleRank = -1;
	}

	public static void AddDeath() => Deaths++;

	public static void ReportStyleRank(int rank)
	{
		if (rank > BestStyleRank) BestStyleRank = rank;
	}
}
