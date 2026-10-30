using System;
using StarterAssets;
using UnityEngine;

// Looks for an Interactable under the crosshair and uses it when Interact is pressed. Also owns the
// reading state for Readables: pressing Interact again, looking away or walking off closes the text.
[RequireComponent(typeof(StarterAssetsInputs))]
public class PlayerInteractor : MonoBehaviour
{
	public float Range = 2.5f;
	[Tooltip("Thickness of the look ray, so small objects are easy to target")]
	public float Radius = 0.12f;
	[Tooltip("Everything but the player; triggers count, so an interactable can use a trigger collider")]
	public LayerMask Mask = ~(1 << 8);
	[Tooltip("Readables close when the player moves this far from where they started reading")]
	public float ReadWalkAwayDistance = 1.5f;

	// the interactable under the crosshair, or null
	public Interactable Current { get; private set; }
	public Readable Reading { get; private set; }

	public event Action<Readable> ReadingChanged;

	public KeyRing Keys { get; private set; }
	public PlayerHUD Hud { get; private set; }

	private StarterAssetsInputs _input;
	private Camera _camera;
	private AudioSource _audio;
	private Vector3 _readStartPosition;
	private readonly RaycastHit[] _hits = new RaycastHit[16];

	private void Awake()
	{
		_input = GetComponent<StarterAssetsInputs>();
		Keys = GetComponent<KeyRing>();
		Hud = GetComponent<PlayerHUD>();

		// 2D source for interface sounds (page turns, key pickups)
		_audio = gameObject.AddComponent<AudioSource>();
		_audio.playOnAwake = false;
		_audio.spatialBlend = 0f;
	}

	private void OnDisable()
	{
		Current = null;
		StopReading();
	}

	private void Update()
	{
		if (_camera == null) _camera = Camera.main;
		Current = FindTarget();

		if (Reading != null && (Current != Reading || Vector3.Distance(transform.position, _readStartPosition) > ReadWalkAwayDistance))
		{
			StopReading();
		}

		if (!_input.interact) return;
		_input.interact = false;

		if (Reading != null) StopReading();
		else if (Current != null) Current.Interact(this);
	}

	private Interactable FindTarget()
	{
		if (_camera == null) return null;

		// the nearest solid hit blocks; triggers only count if they belong to an interactable, so encounter
		// volumes and pickups don't get in the way
		Transform view = _camera.transform;
		int count = Physics.SphereCastNonAlloc(view.position, Radius, view.forward, _hits, Range, Mask, QueryTriggerInteraction.Collide);
		Interactable best = null;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < count; i++)
		{
			RaycastHit hit = _hits[i];
			if (hit.distance >= bestDistance) continue;

			Interactable target = hit.collider.GetComponentInParent<Interactable>();
			if (hit.collider.isTrigger && target == null) continue;

			best = target;
			bestDistance = hit.distance;
		}
		return best != null && best.CanInteract ? best : null;
	}

	public void StartReading(Readable readable)
	{
		Reading = readable;
		_readStartPosition = transform.position;
		ReadingChanged?.Invoke(readable);
	}

	private void StopReading()
	{
		if (Reading == null) return;
		Reading = null;
		ReadingChanged?.Invoke(null);
	}

	public void PlaySound(AudioClip clip)
	{
		if (clip != null) _audio.PlayOneShot(clip);
	}

	public void ShowMessage(string text)
	{
		if (Hud != null && !string.IsNullOrEmpty(text)) Hud.ShowMessage(text);
	}
}
