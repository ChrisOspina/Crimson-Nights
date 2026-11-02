using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Shared by PlayerDeath and LevelExit: takes the controls away from the player for good (this life or this level).
public static class PlayerControl
{
	public static void Freeze(GameObject player)
	{
		SetEnabled<FirstPersonController>(player, false);
		// switcher first, so a switch in progress can't re-enable a firearm
		SetEnabled<WeaponSwitcher>(player, false);
		foreach (Firearm firearm in player.GetComponents<Firearm>()) firearm.enabled = false;
		SetEnabled<MeleeWeapon>(player, false);
		SetEnabled<PlayerMovementAudio>(player, false);
		SetEnabled<LowHealthBreathing>(player, false);
		SetEnabled<PlayerDodge>(player, false);
		SetEnabled<PlayerInteractor>(player, false);
#if ENABLE_INPUT_SYSTEM
		SetEnabled<PlayerInput>(player, false);
#endif
	}

	private static void SetEnabled<T>(GameObject player, bool value) where T : Behaviour
	{
		T component = player.GetComponent<T>();
		if (component != null) component.enabled = value;
	}
}
