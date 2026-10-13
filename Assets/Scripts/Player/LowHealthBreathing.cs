using UnityEngine;

// Loops a breathing clip while the player's health is low, getting louder as health drops.
[RequireComponent(typeof(Health))]
public class LowHealthBreathing : MonoBehaviour
{
	public AudioClip BreathingClip;
	[Tooltip("Breathing starts below this fraction of max health")]
	[Range(0f, 1f)] public float Threshold = 0.35f;
	[Tooltip("Volume right at the threshold")]
	public float MinVolume = 0.25f;
	[Tooltip("Volume at (nearly) zero health")]
	public float MaxVolume = 0.8f;
	[Tooltip("How fast the volume follows health, per second")]
	public float FadeSpeed = 0.6f;

	private Health _health;
	private AudioSource _audioSource;

	private void Awake()
	{
		_health = GetComponent<Health>();
		_audioSource = gameObject.AddComponent<AudioSource>();
		_audioSource.playOnAwake = false;
		_audioSource.spatialBlend = 0f;
		_audioSource.loop = true;
		_audioSource.clip = BreathingClip;
		_audioSource.volume = 0f;
	}

	private void OnDisable()
	{
		if (_audioSource != null) _audioSource.Stop();
	}

	private void Update()
	{
		if (BreathingClip == null) return;

		float health = _health.Normalized;
		float target = health < Threshold && !_health.IsDead
			? Mathf.Lerp(MaxVolume, MinVolume, health / Threshold)
			: 0f;

		_audioSource.volume = Mathf.MoveTowards(_audioSource.volume, target, FadeSpeed * Time.deltaTime);

		if (_audioSource.volume > 0f && !_audioSource.isPlaying) _audioSource.Play();
		else if (_audioSource.volume <= 0f && _audioSource.isPlaying) _audioSource.Stop();
	}
}
