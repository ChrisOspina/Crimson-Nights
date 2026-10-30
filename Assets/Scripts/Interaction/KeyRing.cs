using System;
using System.Collections.Generic;
using UnityEngine;

// The keys the player is carrying. Keys are kept once found (doors don't use them up).
public class KeyRing : MonoBehaviour
{
	public event Action<KeyData> KeyAdded;

	private readonly HashSet<KeyData> _keys = new HashSet<KeyData>();

	public bool Has(KeyData key) => key != null && _keys.Contains(key);

	public void Add(KeyData key)
	{
		if (key != null && _keys.Add(key)) KeyAdded?.Invoke(key);
	}
}
