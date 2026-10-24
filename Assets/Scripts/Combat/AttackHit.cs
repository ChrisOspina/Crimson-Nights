// Raised by the player's weapons after they damage something (for the style meter).
public struct AttackHit
{
	public Health Target;
	// which attack landed ("Revolver", "Jab", "Hook"...); repeating the same one scores less
	public string Action;
	// fists count as one weapon, whichever punch landed
	public string Weapon;
	public bool IsMelee;
	// a combo finisher or heavy punch
	public bool IsFinisher;

	public bool Killed => Target != null && Target.IsDead;

	public AttackHit(Health target, string action, string weapon, bool isMelee, bool isFinisher)
	{
		Target = target;
		Action = action;
		Weapon = weapon;
		IsMelee = isMelee;
		IsFinisher = isFinisher;
	}
}
