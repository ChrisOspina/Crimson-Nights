using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Escape (or a gamepad's Start) pauses the game: shows Panel, stops time through SlowMotion.Paused, pauses sound
// effects (the music sources ignore the listener pause), takes the player's input away and frees the cursor.
// Pressing it again resumes. The panel's buttons call Resume, QuitToMenu and QuitGame from their OnClick.
// Put this on the Canvas (not on Panel: an inactive object never runs its Update). There's no pausing once the
// player is frozen by death or the end of the level.
public class PauseMenu : MonoBehaviour
{
	[Tooltip("The panel shown while paused; hidden on start")]
	public GameObject Panel;
	[Tooltip("Optional: the button selected when the menu opens, so a gamepad can navigate it")]
	public GameObject FirstSelected;
	[Tooltip("Loaded by QuitToMenu; must be in Build Settings")]
	public string MenuSceneName = "MainMenu";
	[Tooltip("This canvas's sorting order; the HUD is 10 and the end screen 100")]
	public int SortingOrder = 50;

	public static bool IsPaused { get; private set; }

	private InputAction _pauseAction;
	private PlayerInput _playerInput;
	private StarterAssetsInputs _inputs;

	private void Awake()
	{
		_pauseAction = new InputAction("Pause", InputActionType.Button);
		_pauseAction.AddBinding("<Keyboard>/escape");
		_pauseAction.AddBinding("<Gamepad>/start");

		// the HUD builds its own canvas at sorting order 10, which would cover this one
		Canvas canvas = GetComponentInParent<Canvas>();
		if (canvas != null) canvas.rootCanvas.sortingOrder = SortingOrder;

		if (Panel == gameObject) Debug.LogError("[PauseMenu] Put PauseMenu on the Canvas, not on the Panel it hides.", this);
		else if (Panel != null) Panel.SetActive(false);
	}

	// coming in from the main menu: a scene load doesn't refocus the window, so Starter Assets won't lock the cursor
	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	private void OnEnable() => _pauseAction.Enable();

	private void OnDisable() => _pauseAction.Disable();

	private void OnDestroy()
	{
		_pauseAction.Dispose();
		// leaving the scene while paused (Quit to Menu): don't carry the pause into the next one
		if (IsPaused) ReleaseGlobals();
	}

	private void Update()
	{
		if (!_pauseAction.WasPressedThisFrame()) return;
		if (IsPaused) Resume();
		else Pause();
	}

	public void Pause()
	{
		if (IsPaused) return;
		if (_inputs == null)
		{
			_inputs = FindAnyObjectByType<StarterAssetsInputs>();
			if (_inputs != null) _playerInput = _inputs.GetComponent<PlayerInput>();
		}
		// frozen by death or the level exit (PlayerControl.Freeze turns PlayerInput off)
		if (_playerInput == null || !_playerInput.enabled) return;

		IsPaused = true;
		SlowMotion.Paused = true;
		AudioListener.pause = true;

		// otherwise the mouse still turns the camera and clicking a button fires on resume
		_playerInput.DeactivateInput();
		ClearInputs();

		_inputs.cursorLocked = false;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		if (Panel != null) Panel.SetActive(true);
		if (FirstSelected != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(FirstSelected);
	}

	public void Resume()
	{
		if (!IsPaused) return;
		ReleaseGlobals();

		if (Panel != null) Panel.SetActive(false);
		if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

		ClearInputs();
		if (_playerInput != null) _playerInput.ActivateInput();

		if (_inputs != null) _inputs.cursorLocked = true;
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	public void QuitToMenu()
	{
		ReleaseGlobals();
		SceneManager.LoadScene(MenuSceneName);
	}

	public void QuitGame()
	{
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	private static void ReleaseGlobals()
	{
		IsPaused = false;
		SlowMotion.Paused = false;
		AudioListener.pause = false;
	}

	// drop held and latched input so nothing carries across the pause
	private void ClearInputs()
	{
		if (_inputs == null) return;
		_inputs.move = Vector2.zero;
		_inputs.look = Vector2.zero;
		_inputs.sprint = false;
		_inputs.fire = false;
		_inputs.melee = false;
		_inputs.reload = false;
		_inputs.dodge = false;
		_inputs.switchWeapon = false;
		_inputs.interact = false;
	}
}
