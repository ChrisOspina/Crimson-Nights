using UnityEngine;

// A container the player can search once (a coffin, a drawer): plays a sound, shows a line, and can reveal
// something hidden inside (a key pickup) or wake whatever is in there (an Encounter with no trigger of its own).
public class Searchable : Interactable
{
	[Tooltip("Shown in the prompt, e.g. \"the coffin of Elias Morvayne\"")]
	public string DisplayName = "coffin";
	[TextArea(2, 6)] public string FoundMessage;
	public AudioClip SearchClip;

	[Header("Contents")]
	[Tooltip("Inactive until searched")]
	public GameObject Reveal;
	[Tooltip("Started when searched")]
	public Encounter Ambush;

	public bool Searched { get; private set; }

	public override string Prompt => "Search " + DisplayName;

	public override bool CanInteract => base.CanInteract && !Searched;

	private void Awake()
	{
		if (Reveal != null) Reveal.SetActive(false);
	}

	public override void Interact(PlayerInteractor player)
	{
		Searched = true;
		player.PlaySound(SearchClip);
		player.ShowMessage(FoundMessage);
		if (Reveal != null) Reveal.SetActive(true);
		if (Ambush != null) Ambush.Begin(player);
	}
}
