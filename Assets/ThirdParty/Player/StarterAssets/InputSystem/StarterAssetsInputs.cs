using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool sprint;

		[Header("Combat Input Values")]
		[Tooltip("Set on press; cleared by the weapon that consumes it")]
		public bool fire;
		[Tooltip("Held state (true while the melee button is down)")]
		public bool melee;
		[Tooltip("Set on press; cleared by the weapon that consumes it")]
		public bool reload;
		[Tooltip("Set on press; cleared by PlayerDodge when it consumes it")]
		public bool dodge;
		[Tooltip("Set on press; cleared by WeaponSwitcher when it consumes it")]
		public bool switchWeapon;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnFire(InputValue value)
		{
			FireInput(value.isPressed);
		}

		public void OnMelee(InputValue value)
		{
			MeleeInput(value.isPressed);
		}

		public void OnReload(InputValue value)
		{
			ReloadInput(value.isPressed);
		}

		public void OnDodge(InputValue value)
		{
			DodgeInput(value.isPressed);
		}

		public void OnSwitchWeapon(InputValue value)
		{
			SwitchWeaponInput(value.isPressed);
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		public void FireInput(bool newFireState)
		{
			// only latch presses; the weapon clears the flag when it consumes it
			if (newFireState) fire = true;
		}

		public void MeleeInput(bool newMeleeState)
		{
			melee = newMeleeState;
		}

		public void ReloadInput(bool newReloadState)
		{
			if (newReloadState) reload = true;
		}

		public void DodgeInput(bool newDodgeState)
		{
			if (newDodgeState) dodge = true;
		}

		public void SwitchWeaponInput(bool newSwitchWeaponState)
		{
			if (newSwitchWeaponState) switchWeapon = true;
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}