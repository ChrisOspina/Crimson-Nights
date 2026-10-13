using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// On death: freeze the player, show "YOU DIED", then reload the scene.
[RequireComponent(typeof(Health))]
public class PlayerDeath : MonoBehaviour
{
	public float ReloadDelay = 2f;

	private Health _health;

	private void Awake()
	{
		_health = GetComponent<Health>();
	}

	private void OnEnable() => _health.Died += OnDied;
	private void OnDisable() => _health.Died -= OnDied;

	private void OnDied()
	{
		SetEnabled<FirstPersonController>(false);
		SetEnabled<Revolver>(false);
		SetEnabled<MeleeWeapon>(false);
		SetEnabled<PlayerMovementAudio>(false);
		SetEnabled<LowHealthBreathing>(false);
		SetEnabled<PlayerDodge>(false);
#if ENABLE_INPUT_SYSTEM
		SetEnabled<PlayerInput>(false);
#endif

		PlayerHUD hud = GetComponent<PlayerHUD>();
		if (hud != null) hud.ShowDeath();

		StartCoroutine(ReloadAfterDelay());
	}

	private void SetEnabled<T>(bool value) where T : Behaviour
	{
		T component = GetComponent<T>();
		if (component != null) component.enabled = value;
	}

	private IEnumerator ReloadAfterDelay()
	{
		yield return new WaitForSeconds(ReloadDelay);
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}
}
