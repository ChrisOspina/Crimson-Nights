using UnityEngine;

// Exploration music: one looping track at a time, crossfaded when the player walks into a MusicZone. It fades
// out while any Encounter is being fought (so ambush sounds and the Countess's own music have the room) and
// comes back once every started fight is cleared. Fades run on unscaled time, so slow motion doesn't stall them.
public class MusicDirector : MonoBehaviour
{
	public static MusicDirector Instance { get; private set; }

	[Tooltip("Playing from the start (the corridor and gallery)")]
	public AudioClip StartTrack;
	[Range(0f, 1f)] public float Volume = 0.3f;
	[Tooltip("Seconds to crossfade between tracks")]
	public float CrossfadeTime = 4f;
	[Tooltip("Seconds to fade out when a fight starts, and back in after it")]
	public float CombatFadeTime = 1.5f;
	[Tooltip("Volume multiplier while a fight is on (0 = silent)")]
	[Range(0f, 1f)] public float CombatVolume = 0f;

	private AudioSource _current;
	private AudioSource _previous;
	private float _blend = 1f; // 0..1 from _previous to _current
	private float _combatScale = 1f;
	private int _activeFights;

	private void Awake()
	{
		Instance = this;
		_current = CreateSource();
		_previous = CreateSource();
	}

	private void OnEnable()
	{
		Encounter.AnyStarted += OnFightStarted;
		Encounter.AnyCleared += OnFightCleared;
	}

	private void OnDisable()
	{
		Encounter.AnyStarted -= OnFightStarted;
		Encounter.AnyCleared -= OnFightCleared;
		if (Instance == this) Instance = null;
	}

	private void Start()
	{
		Play(StartTrack);
	}

	// crossfades to `clip`; nothing happens if it's already playing
	public void Play(AudioClip clip)
	{
		if (clip == null || (_current.clip == clip && _current.isPlaying)) return;

		AudioSource old = _previous;
		_previous = _current;
		_current = old;
		_current.Stop();
		_current.clip = clip;
		_current.volume = 0f;
		_current.Play();
		_blend = 0f;
	}

	private void Update()
	{
		float dt = Time.unscaledDeltaTime;
		_blend = CrossfadeTime > 0f ? Mathf.MoveTowards(_blend, 1f, dt / CrossfadeTime) : 1f;
		float combatTarget = _activeFights > 0 ? CombatVolume : 1f;
		_combatScale = CombatFadeTime > 0f ? Mathf.MoveTowards(_combatScale, combatTarget, dt / CombatFadeTime) : combatTarget;

		float level = Volume * _combatScale;
		_current.volume = level * _blend;
		_previous.volume = level * (1f - _blend);
		if (_blend >= 1f && _previous.isPlaying) _previous.Stop();
	}

	private void OnFightStarted(Encounter encounter) => _activeFights++;

	private void OnFightCleared(Encounter encounter) => _activeFights = Mathf.Max(0, _activeFights - 1);

	private AudioSource CreateSource()
	{
		var source = gameObject.AddComponent<AudioSource>();
		source.playOnAwake = false;
		source.loop = true;
		source.spatialBlend = 0f;
		source.volume = 0f;
		return source;
	}
}
