using UnityEngine;

// Plays a looping ambient sound (wind, room tone, fire) from a random point in the clip, so several copies
// of the same loop don't play in step.
[RequireComponent(typeof(AudioSource))]
public class AmbientLoop : MonoBehaviour
{
	private void Start()
	{
		var source = GetComponent<AudioSource>();
		if (source.clip == null) return;
		source.loop = true;
		source.time = Random.Range(0f, source.clip.length);
		source.Play();
	}
}
