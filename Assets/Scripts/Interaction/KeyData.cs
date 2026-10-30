using UnityEngine;

// A key item. Doors and key pickups reference the asset itself, so there are no id strings to mistype.
[CreateAssetMenu(menuName = "Crimson Nights/Key", fileName = "Key")]
public class KeyData : ScriptableObject
{
	[Tooltip("Shown in messages, e.g. \"Gallery Key\"")]
	public string DisplayName = "Key";
}
