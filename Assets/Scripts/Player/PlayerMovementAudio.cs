using StarterAssets;
using UnityEngine;

// Plays footstep and land sounds for the FirstPersonController.
[RequireComponent(typeof(FirstPersonController))]
public class PlayerMovementAudio : MonoBehaviour
{
	[Header("Clips")]
	public AudioClip[] FootstepClips;
	public AudioClip LandClip;

	[Header("Footsteps")]
	[Tooltip("Distance in metres travelled between footsteps")]
	public float StrideLength = 2.0f;
	[Tooltip("Horizontal speed below which no footsteps play")]
	public float MinStepSpeed = 0.5f;
	[Range(0f, 1f)] public float FootstepVolume = 0.5f;

	[Header("Land")]
	[Range(0f, 1f)] public float LandVolume = 0.6f;
	[Tooltip("Minimum time airborne before a land sound plays, e.g. stepping off a ledge")]
	public float MinAirTimeForLand = 0.25f;

	[Header("Variation")]
	public float PitchVariation = 0.1f;

	private FirstPersonController _controller;
	private CharacterController _characterController;
	private AudioSource _audioSource;

	private float _stepDistance;
	private float _airTime;
	private int _lastFootstepIndex = -1;

	private void Awake()
	{
		_controller = GetComponent<FirstPersonController>();
		_characterController = GetComponent<CharacterController>();

		_audioSource = GetComponent<AudioSource>();
		if (_audioSource == null)
		{
			_audioSource = gameObject.AddComponent<AudioSource>();
			_audioSource.playOnAwake = false;
			_audioSource.spatialBlend = 0f; // 2D, it's the player's own sounds
		}
	}

	// LateUpdate so we read the controller's state after its Update has run
	private void LateUpdate()
	{
		bool grounded = _controller.Grounded;

		if (!grounded)
		{
			_airTime += Time.deltaTime;
			return;
		}

		// landed this frame
		if (_airTime > 0f)
		{
			if (_airTime >= MinAirTimeForLand)
			{
				Play(LandClip, LandVolume);
				_stepDistance = 0f;
			}
			_airTime = 0f;
		}

		UpdateFootsteps();
	}

	private void UpdateFootsteps()
	{
		Vector3 velocity = _characterController.velocity;
		float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

		if (horizontalSpeed < MinStepSpeed)
		{
			// prime it so the first step comes quickly when we start moving again
			_stepDistance = StrideLength * 0.6f;
			return;
		}

		_stepDistance += horizontalSpeed * Time.deltaTime;
		if (_stepDistance >= StrideLength)
		{
			_stepDistance = 0f;
			PlayFootstep();
		}
	}

	private void PlayFootstep()
	{
		if (FootstepClips == null || FootstepClips.Length == 0) return;

		// pick a random clip, avoiding an immediate repeat
		int index = Random.Range(0, FootstepClips.Length);
		if (FootstepClips.Length > 1 && index == _lastFootstepIndex)
		{
			index = (index + 1) % FootstepClips.Length;
		}
		_lastFootstepIndex = index;

		Play(FootstepClips[index], FootstepVolume);
	}

	private void Play(AudioClip clip, float volume)
	{
		if (clip == null) return;
		_audioSource.pitch = 1f + Random.Range(-PitchVariation, PitchVariation);
		_audioSource.PlayOneShot(clip, volume);
	}
}
