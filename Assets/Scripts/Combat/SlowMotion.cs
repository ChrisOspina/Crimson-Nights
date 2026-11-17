using UnityEngine;

// The one place that changes Time.timeScale. Times itself on unscaled time so the slow-down can't stretch itself,
// eases back to normal speed at the end, and survives scene reloads so time is always restored.
// Also owns pausing (PauseMenu): Paused holds time at zero on top of any slow-down, which waits out the pause.
public static class SlowMotion
{
	private static Runner _runner;
	private static float _defaultFixedDeltaTime = -1f;
	private static float _scale = 1f;
	private static bool _paused;

	public static bool IsActive => _runner != null && _runner.Active;

	public static bool Paused
	{
		get => _paused;
		set
		{
			if (_paused == value) return;
			if (_defaultFixedDeltaTime < 0f) _defaultFixedDeltaTime = Time.fixedDeltaTime;
			_paused = value;
			Apply(_scale);
		}
	}

	// scale: target Time.timeScale; realDuration: seconds of real time before returning to normal
	public static void Play(float scale, float realDuration)
	{
		if (_runner == null)
		{
			var go = new GameObject("SlowMotion");
			go.hideFlags = HideFlags.HideInHierarchy;
			Object.DontDestroyOnLoad(go);
			_runner = go.AddComponent<Runner>();
		}
		if (_defaultFixedDeltaTime < 0f) _defaultFixedDeltaTime = Time.fixedDeltaTime;

		_runner.Run(Mathf.Clamp(scale, 0.01f, 1f), Mathf.Max(0f, realDuration));
	}

	public static void Stop()
	{
		if (_runner != null) _runner.StopNow();
	}

	private static void Apply(float scale)
	{
		_scale = scale;
		Time.timeScale = _paused ? 0f : scale;
		if (_defaultFixedDeltaTime > 0f) Time.fixedDeltaTime = _defaultFixedDeltaTime * scale;
	}

	private class Runner : MonoBehaviour
	{
		private const float EaseOutTime = 0.15f;

		public bool Active { get; private set; }

		private float _scale;
		private float _endTime;

		public void Run(float scale, float duration)
		{
			Active = true;
			_scale = scale;
			_endTime = Time.unscaledTime + duration;
			Apply(scale);
		}

		public void StopNow()
		{
			if (!Active) return;
			Active = false;
			Apply(1f);
		}

		private void Update()
		{
			if (!Active) return;
			// the slow-down is timed in real time, so push its end back while the game is paused
			if (_paused)
			{
				_endTime += Time.unscaledDeltaTime;
				return;
			}

			float remaining = _endTime - Time.unscaledTime;
			if (remaining <= 0f) StopNow();
			else if (remaining < EaseOutTime) Apply(Mathf.Lerp(1f, _scale, remaining / EaseOutTime));
		}

		private void OnDestroy()
		{
			if (Active) Apply(1f);
		}
	}
}
