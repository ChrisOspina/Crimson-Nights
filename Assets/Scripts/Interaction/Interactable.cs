using UnityEngine;

// Something the player can use by looking at it and pressing Interact. PlayerInteractor finds it with
// GetComponentInParent on whatever collider its look ray hits, so the collider can be on a child.
public abstract class Interactable : MonoBehaviour
{
	// verb shown after the button hint, e.g. "Open", "Read", "Take Gallery Key"
	public abstract string Prompt { get; }

	// false hides the prompt and ignores presses (an open door, a key already taken)
	public virtual bool CanInteract => isActiveAndEnabled;

	public abstract void Interact(PlayerInteractor player);
}
