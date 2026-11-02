using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// On death: freeze the player, scream, show "YOU DIED", then reload the scene.
[RequireComponent(typeof(Health))]
public class PlayerDeath : MonoBehaviour
{
	public float ReloadDelay = 2f;
	[Tooltip("Played once, in 2D, as the player dies. Keep it shorter than ReloadDelay or the reload cuts it off")]
	public AudioClip DeathClip;

	private Health _health;

	private void Awake()
	{
		_health = GetComponent<Health>();
	}

	private void OnEnable() => _health.Died += OnDied;
	private void OnDisable() => _health.Died -= OnDied;

	private void OnDied()
	{
		PlayerControl.Freeze(gameObject);
		RunStats.AddDeath();

		if (DeathClip != null)
		{
			var source = gameObject.AddComponent<AudioSource>();
			source.spatialBlend = 0f;
			source.PlayOneShot(DeathClip);
		}

		PlayerHUD hud = GetComponent<PlayerHUD>();
		if (hud != null) hud.ShowDeath();

		StartCoroutine(ReloadAfterDelay());
	}

	private IEnumerator ReloadAfterDelay()
	{
		yield return new WaitForSeconds(ReloadDelay);
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}
}
