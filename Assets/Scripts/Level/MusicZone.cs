using UnityEngine;

// A trigger volume covering a room: walking into it crossfades the MusicDirector to this room's track.
[RequireComponent(typeof(Collider))]
public class MusicZone : MonoBehaviour
{
	public AudioClip Track;

	private void Awake()
	{
		GetComponent<Collider>().isTrigger = true;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!other.CompareTag("Player") || MusicDirector.Instance == null) return;
		MusicDirector.Instance.Play(Track);
	}
}
