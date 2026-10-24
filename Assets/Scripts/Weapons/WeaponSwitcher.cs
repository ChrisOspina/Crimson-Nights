using System;
using System.Collections;
using StarterAssets;
using UnityEngine;

// Cycles between the player's firearms (Q / scroll / gamepad Y). Only the equipped Firearm is enabled
// and visible; switching lowers the old viewmodel, then raises the new one, and nothing fires meanwhile.
// Punches have their own button and isn't part of this.
[RequireComponent(typeof(StarterAssetsInputs))]
public class WeaponSwitcher : MonoBehaviour
{
	[Tooltip("Firearms in switch order; the first is equipped at start")]
	public Firearm[] Firearms;
	[Tooltip("Seconds to lower the old weapon")]
	public float LowerTime = 0.12f;
	[Tooltip("Seconds to raise the new weapon")]
	public float RaiseTime = 0.18f;
	[Tooltip("How far viewmodels drop out of view while switching, in metres")]
	public float LowerDistance = 0.35f;

	public Firearm Current => Firearms.Length > 0 ? Firearms[_index] : null;
	public bool IsSwitching { get; private set; }

	// raised as soon as a switch starts, with the incoming weapon (for the HUD)
	public event Action<Firearm> Switched;

	private StarterAssetsInputs _input;
	private Vector3[] _restPositions;
	private int _index;

	private void Awake()
	{
		_input = GetComponent<StarterAssetsInputs>();

		_restPositions = new Vector3[Firearms.Length];
		for (int i = 0; i < Firearms.Length; i++)
		{
			Transform viewmodel = Firearms[i].Viewmodel;
			if (viewmodel != null) _restPositions[i] = viewmodel.localPosition;
			SetEquipped(i, i == 0);
		}
	}

	private void OnDisable()
	{
		StopAllCoroutines();
		IsSwitching = false;
	}

	private void Update()
	{
		if (IsSwitching)
		{
			// presses made mid-switch would otherwise fire or reload the moment the new weapon is up
			_input.fire = false;
			_input.reload = false;
		}

		if (_input.switchWeapon)
		{
			_input.switchWeapon = false;
			if (!IsSwitching && Firearms.Length > 1) StartCoroutine(SwitchTo((_index + 1) % Firearms.Length));
		}
	}

	private IEnumerator SwitchTo(int next)
	{
		IsSwitching = true;
		int previous = _index;

		Firearms[previous].enabled = false;
		_index = next;
		Switched?.Invoke(Firearms[next]);

		yield return MoveViewmodel(previous, 0f, 1f, LowerTime);
		SetViewmodelVisible(previous, false);

		SetViewmodelVisible(next, true);
		yield return MoveViewmodel(next, 1f, 0f, RaiseTime);

		Firearms[next].enabled = true;
		IsSwitching = false;
	}

	// lowered: 0 = at rest, 1 = fully out of view
	private IEnumerator MoveViewmodel(int index, float fromLowered, float toLowered, float duration)
	{
		Transform viewmodel = Firearms[index].Viewmodel;
		if (viewmodel == null) yield break;

		Vector3 start = viewmodel.localPosition;
		Vector3 rest = _restPositions[index];
		Vector3 target = rest + Vector3.down * (LowerDistance * toLowered);
		if (fromLowered > 0f) start = rest + Vector3.down * (LowerDistance * fromLowered);

		for (float t = 0f; t < duration; t += Time.deltaTime)
		{
			viewmodel.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
			yield return null;
		}
		viewmodel.localPosition = target;
	}

	private void SetEquipped(int index, bool equipped)
	{
		Firearms[index].enabled = equipped;
		SetViewmodelVisible(index, equipped);
	}

	private void SetViewmodelVisible(int index, bool visible)
	{
		Transform viewmodel = Firearms[index].Viewmodel;
		if (viewmodel != null) viewmodel.gameObject.SetActive(visible);
	}
}
