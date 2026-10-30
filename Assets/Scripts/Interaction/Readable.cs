using UnityEngine;

// A placard, letter or journal page. Interact shows its text on the HUD until the player presses again,
// looks away or walks off.
public class Readable : Interactable
{
	public string Title;
	[TextArea(4, 12)] public string Body;
	public AudioClip ReadClip;
	[Tooltip("Prompt verb, e.g. \"Read\" or \"Examine\"")]
	public string Verb = "Read";

	public override string Prompt => Verb;

	public override void Interact(PlayerInteractor player)
	{
		player.PlaySound(ReadClip);
		player.StartReading(this);
	}
}
