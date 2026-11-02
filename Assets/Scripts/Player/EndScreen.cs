using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The end-of-level card: fades to black (after a beat, so the exit's last line can be read), then shows the
// title, a line, the run's stats (RunStats) and Play Again / Quit. Builds its own canvas and, if the scene has
// none, an EventSystem, so it needs no scene wiring. Works with the mouse or a gamepad.
public class EndScreen : MonoBehaviour
{
	private const float HoldTime = 2.5f;
	private const float FadeTime = 1.5f;
	private const float CardFadeTime = 1f;

	private static readonly Color Blood = new Color(0.75f, 0.06f, 0.08f);
	private static readonly Color Bone = new Color(0.9f, 0.86f, 0.8f);

	private string _title, _subtitle, _footer;
	private Image _black;
	private CanvasGroup _card;
	private Button _playAgain;

	public static void Show(string title, string subtitle, string footer)
	{
		if (FindAnyObjectByType<EndScreen>() != null) return;
		var screen = new GameObject("EndScreen").AddComponent<EndScreen>();
		screen._title = title;
		screen._subtitle = subtitle;
		screen._footer = footer;
	}

	private void Start()
	{
		Build();
		StartCoroutine(Run());
	}

	private IEnumerator Run()
	{
		yield return new WaitForSecondsRealtime(HoldTime);
		for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
		{
			_black.color = new Color(0f, 0f, 0f, t / FadeTime);
			yield return null;
		}
		_black.color = Color.black;

		// hand the cursor back; the player's input is already off
		StarterAssetsInputs inputs = FindAnyObjectByType<StarterAssetsInputs>();
		if (inputs != null) inputs.cursorLocked = false;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		_card.gameObject.SetActive(true);
		for (float t = 0f; t < CardFadeTime; t += Time.unscaledDeltaTime)
		{
			_card.alpha = t / CardFadeTime;
			yield return null;
		}
		_card.alpha = 1f;
		_card.interactable = true;
		EventSystem.current.SetSelectedGameObject(_playAgain.gameObject);
	}

	private void PlayAgain()
	{
		RunStats.Begin();
		// a scene load doesn't refocus the window, so Starter Assets won't lock the cursor again by itself
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}

	private void Quit()
	{
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	// ---------------------------------------------------------------- UI

	private void Build()
	{
		if (EventSystem.current == null)
		{
			var events = new GameObject("EventSystem", typeof(EventSystem));
			events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
		}

		var canvasGO = new GameObject("EndScreenCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		canvasGO.transform.SetParent(transform, false);
		var canvas = canvasGO.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 100; // over the HUD
		var scaler = canvasGO.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		var root = canvasGO.GetComponent<RectTransform>();
		Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

		_black = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		_black.transform.SetParent(root, false);
		_black.color = Color.clear;
		Stretch(_black.rectTransform);

		var cardGO = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
		cardGO.transform.SetParent(root, false);
		Stretch(cardGO.GetComponent<RectTransform>());
		_card = cardGO.GetComponent<CanvasGroup>();
		_card.alpha = 0f;
		_card.interactable = false;
		var card = cardGO.GetComponent<RectTransform>();

		Text title = AddText(card, font, _title, 84, Blood, 230f, 110f);
		title.fontStyle = FontStyle.Bold;
		AddText(card, font, _subtitle, 30, Bone, 140f, 50f).fontStyle = FontStyle.Italic;
		AddText(card, font, _footer, 26, new Color(Bone.r, Bone.g, Bone.b, 0.6f), 85f, 40f);

		string style = RunStats.BestStyleRank >= 0 ? StyleRankText(RunStats.BestStyleRank) : "-";
		string stats = $"Time   {FormatTime(RunStats.Elapsed)}\nDeaths   {RunStats.Deaths}\nBest style   {style}";
		Text statsText = AddText(card, font, stats, 30, Bone, -20f, 140f);
		statsText.lineSpacing = 1.3f;

		_playAgain = AddButton(card, font, "Play Again", -170f, PlayAgain);
		Button quit = AddButton(card, font, "Quit", -240f, Quit);
		// explicit up/down, so a pad moves between the two
		SetVerticalNavigation(_playAgain, quit);

		cardGO.SetActive(false);
	}

	private static string StyleRankText(int rank)
	{
		StyleMeter meter = FindAnyObjectByType<StyleMeter>();
		if (meter == null || meter.Data == null || rank >= meter.Data.RankLetters.Length) return (rank + 1).ToString();
		string letter = meter.Data.RankLetters[rank];
		return rank < meter.Data.RankNames.Length ? $"{letter}  ({meter.Data.RankNames[rank]})" : letter;
	}

	private static string FormatTime(float seconds)
	{
		int total = Mathf.FloorToInt(seconds);
		return $"{total / 60}:{total % 60:00}";
	}

	private static Text AddText(RectTransform parent, Font font, string content, int size, Color color, float y, float height)
	{
		var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
		text.transform.SetParent(parent, false);
		text.font = font;
		text.fontSize = size;
		text.color = color;
		text.alignment = TextAnchor.MiddleCenter;
		text.text = content;
		text.raycastTarget = false;
		RectTransform rect = text.rectTransform;
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = new Vector2(0f, y);
		rect.sizeDelta = new Vector2(1400f, height);
		return text;
	}

	private static Button AddButton(RectTransform parent, Font font, string label, float y, UnityEngine.Events.UnityAction onClick)
	{
		var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = new Vector2(0f, y);
		rect.sizeDelta = new Vector2(320f, 54f);

		var image = go.GetComponent<Image>();
		image.color = Color.white;
		var button = go.GetComponent<Button>();
		ColorBlock colors = button.colors;
		colors.normalColor = new Color(0.12f, 0.02f, 0.03f, 0.9f);
		colors.highlightedColor = new Color(0.45f, 0.04f, 0.06f, 1f);
		colors.selectedColor = colors.highlightedColor;
		colors.pressedColor = Blood;
		button.colors = colors;
		button.onClick.AddListener(onClick);

		Text text = AddText(rect, font, label, 28, Bone, 0f, 54f);
		text.rectTransform.sizeDelta = new Vector2(320f, 54f);
		return button;
	}

	private static void SetVerticalNavigation(Button top, Button bottom)
	{
		var navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = bottom };
		top.navigation = navigation;
		bottom.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = top };
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}
}
