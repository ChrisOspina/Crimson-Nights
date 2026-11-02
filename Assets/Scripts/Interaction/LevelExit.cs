using UnityEngine;

// The way out of a level (the Countess's empty portrait). Using it shows a last line, takes the controls away
// and fades to EndScreen's card with the run's stats. The game has at most three levels; until level 2
// exists this is the end of the demo.
public class LevelExit : Interactable
{
	[Tooltip("Prompt verb, e.g. \"Climb through the frame\"")]
	public string Verb = "Go on";
	[TextArea(2, 4)] public string ExitMessage;
	public AudioClip ExitClip;

	[Header("End card")]
	public string Title = "THE CRIMSON MUSEUM";
	public string Subtitle;
	public string Footer = "End of the demo";

	private bool _used;

	public override string Prompt => Verb;

	public override bool CanInteract => base.CanInteract && !_used;

	public override void Interact(PlayerInteractor player)
	{
		_used = true;
		player.PlaySound(ExitClip);
		player.ShowMessage(ExitMessage);
		PlayerControl.Freeze(player.gameObject);
		EndScreen.Show(Title, Subtitle, Footer);
	}
}
