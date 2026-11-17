using UnityEngine;
using UnityEngine.InputSystem;

// M mutes and unmutes all sound, in every scene (main menu, levels, while paused). Sets itself up when the game starts,
// so no scene needs it. Mutes through AudioListener.volume, which sits on top of any per-source or mixer volumes,
// and lasts for the session (it survives scene loads but not a restart).
public static class AudioMute
{
	public static bool Muted { get; private set; }

	public static void SetMuted(bool muted)
	{
		Muted = muted;
		AudioListener.volume = muted ? 0f : 1f;
	}

	public static void Toggle()
	{
		SetMuted(!Muted);
		// say so in-game; there's no HUD in the menus, where the silence speaks for itself
		PlayerHUD hud = Object.FindAnyObjectByType<PlayerHUD>();
		if (hud != null) hud.ShowMessage(Muted ? "Sound off (M)" : "Sound on");
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Init()
	{
		var go = new GameObject("AudioMute");
		go.hideFlags = HideFlags.HideInHierarchy;
		Object.DontDestroyOnLoad(go);
		go.AddComponent<Listener>();
		SetMuted(false);
	}

	private class Listener : MonoBehaviour
	{
		private InputAction _muteAction;

		private void Awake()
		{
			_muteAction = new InputAction("Mute", InputActionType.Button, "<Keyboard>/m");
		}

		private void OnEnable() => _muteAction.Enable();

		private void OnDisable() => _muteAction.Disable();

		private void OnDestroy() => _muteAction.Dispose();

		private void Update()
		{
			if (_muteAction.WasPressedThisFrame()) Toggle();
		}
	}
}
