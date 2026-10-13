using System;
using UnityEngine;

// Shared hit points for the player and enemies. Find it on a hit collider with GetComponentInParent<Health>().
public class Health : MonoBehaviour
{
	public float MaxHealth = 100f;
	[Tooltip("While true, incoming damage is ignored (dodge i-frames)")]
	public bool Invulnerable;

	public float Current { get; private set; }
	public bool IsDead { get; private set; }
	public float Normalized => MaxHealth > 0f ? Current / MaxHealth : 0f;

	public event Action<DamageInfo> Damaged;
	public event Action Healed;
	public event Action Died;

	private void Awake()
	{
		Current = MaxHealth;
	}

	public void TakeDamage(DamageInfo info)
	{
		if (IsDead || Invulnerable || info.Amount <= 0f) return;

		Current = Mathf.Max(0f, Current - info.Amount);
		Damaged?.Invoke(info);

		if (Current <= 0f)
		{
			IsDead = true;
			Died?.Invoke();
		}
	}

	public void Heal(float amount)
	{
		if (IsDead || amount <= 0f) return;

		Current = Mathf.Min(MaxHealth, Current + amount);
		Healed?.Invoke();
	}

	// used by spawners/data assets to set health before the first hit
	public void ResetHealth(float maxHealth)
	{
		MaxHealth = maxHealth;
		Current = maxHealth;
		IsDead = false;
	}
}
