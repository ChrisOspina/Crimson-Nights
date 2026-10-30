using System.Collections;
using UnityEngine;

// A hinged door. The root sits at the bottom centre of the doorway facing out of the wall; Hinge sits on one
// edge and turns the leaf, which carries the collider. A locked door opens for RequiredKey; with no
// RequiredKey it stays shut until something calls Unlock (an Encounter barring the exit, a door that's
// only scenery for now). Doors swing away from the player and stay open.
[RequireComponent(typeof(AudioSource))]
public class Door : Interactable
{
	public Transform Hinge;
	public float OpenAngle = 100f;
	public float OpenTime = 1.1f;
	public float SlamTime = 0.2f;
	public bool StartOpen;

	[Header("Lock")]
	public bool Locked;
	[Tooltip("Opens the lock if the player carries it. Leave empty for a door only a script can unlock")]
	public KeyData RequiredKey;
	[Tooltip("Shown when the player tries the locked door; defaults to naming the key")]
	public string LockedMessage;

	[Header("Audio")]
	public AudioClip OpenClip;
	public AudioClip SlamClip;
	public AudioClip LockedClip;

	public bool IsOpen { get; private set; }

	public override string Prompt => "Open";
	public override bool CanInteract => base.CanInteract && !IsOpen && _swing == null;

	private AudioSource _audio;
	private Coroutine _swing;
	private float _lockedClipEndTime;

	private void Awake()
	{
		_audio = GetComponent<AudioSource>();
		if (StartOpen)
		{
			IsOpen = true;
			Hinge.localRotation = Quaternion.Euler(0f, OpenAngle, 0f);
		}
	}

	public override void Interact(PlayerInteractor player)
	{
		if (Locked)
		{
			if (RequiredKey != null && player.Keys != null && player.Keys.Has(RequiredKey))
			{
				Locked = false;
				player.ShowMessage("Unlocked with the " + RequiredKey.DisplayName);
			}
			else
			{
				// don't restart the rattle on every press
				if (Time.time >= _lockedClipEndTime && LockedClip != null)
				{
					_audio.PlayOneShot(LockedClip);
					_lockedClipEndTime = Time.time + Mathf.Min(LockedClip.length, 1.5f);
				}
				player.ShowMessage(!string.IsNullOrEmpty(LockedMessage) ? LockedMessage
					: RequiredKey != null ? "Locked. It needs the " + RequiredKey.DisplayName + "." : "Locked.");
				return;
			}
		}

		Open(player.transform.position);
	}

	// swings away from `from`
	public void Open(Vector3 from)
	{
		if (IsOpen) return;
		IsOpen = true;

		// a positive turn swings the leaf towards -forward, away from someone standing in front
		float side = Vector3.Dot(from - transform.position, transform.forward) >= 0f ? 1f : -1f;
		if (OpenClip != null) _audio.PlayOneShot(OpenClip);
		Swing(OpenAngle * side, OpenTime);
	}

	// slams shut, optionally locking it with a message for anyone who tries it
	public void Close(bool lockIt, string lockedMessage = null)
	{
		if (lockIt)
		{
			Locked = true;
			LockedMessage = lockedMessage;
		}
		if (!IsOpen) return;
		IsOpen = false;

		if (SlamClip != null) _audio.PlayOneShot(SlamClip);
		Swing(0f, SlamTime);
	}

	public void Unlock()
	{
		Locked = false;
	}

	private void Swing(float angle, float time)
	{
		if (_swing != null) StopCoroutine(_swing);
		_swing = StartCoroutine(SwingTo(angle, time));
	}

	private IEnumerator SwingTo(float angle, float time)
	{
		Quaternion start = Hinge.localRotation;
		Quaternion end = Quaternion.Euler(0f, angle, 0f);
		for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(time, 0.01f))
		{
			// eases out when opening (a heavy door), linear when slamming
			float eased = angle != 0f ? 1f - (1f - t) * (1f - t) : t;
			Hinge.localRotation = Quaternion.Slerp(start, end, eased);
			yield return null;
		}
		Hinge.localRotation = end;
		_swing = null;
	}
}
