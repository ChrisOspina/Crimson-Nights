using UnityEngine;
using UnityEngine.UI;

// Prototype HUD. Builds its own overlay canvas at runtime so it needs no scene wiring.
[RequireComponent(typeof(Health))]
public class PlayerHUD : MonoBehaviour
{
	public Color HealthColor = new Color(0.75f, 0.08f, 0.1f);
	public Color CrosshairColor = new Color(1f, 1f, 1f, 0.85f);
	public Color HeavyReadyColor = new Color(1f, 0.6f, 0.2f, 1f);
	public Color EmptyAmmoColor = new Color(0.9f, 0.25f, 0.2f, 1f);
	public Color CounterColor = new Color(0.55f, 0.85f, 1f, 1f);
	public Color PerfectDodgeFlashColor = new Color(0.75f, 0.9f, 1f, 1f);

	[Header("Style Meter")]
	[Tooltip("Rank colour at D, blending towards StyleHighColor at A")]
	public Color StyleLowColor = new Color(0.7f, 0.7f, 0.72f, 1f);
	public Color StyleHighColor = new Color(0.85f, 0.08f, 0.12f, 1f);
	[Tooltip("Rank colour from S upwards")]
	public Color StyleTopColor = new Color(1f, 0.78f, 0.3f, 1f);
	[Tooltip("Seconds a callout (\"FINISHER\", \"DOUBLE KILL\"...) stays on screen")]
	public float CalloutTime = 1.2f;

	private Health _health;
	private Firearm[] _firearms;
	private WeaponSwitcher _switcher;
	private MeleeWeapon _melee;
	private PlayerDodge _dodge;
	private StyleMeter _style;

	private RectTransform _healthFill;
	private Text _ammoText;
	private Text _weaponText;
	private Image _crosshair;
	private Image _damageFlash;
	private Color _flashColor;
	private RectTransform _dodgeCooldownFill;
	private Text _deathText;
	private float _flashAlpha;

	private GameObject _styleRoot;
	private Text _styleLetter;
	private Text _styleName;
	private RectTransform _styleFill;
	private Image _styleFillImage;
	private Text _styleCallout;
	private float _calloutEndTime;
	private float _rankPunch; // 1 right after a rank change, decays to 0

	private void Awake()
	{
		_health = GetComponent<Health>();
		_firearms = GetComponents<Firearm>();
		_switcher = GetComponent<WeaponSwitcher>();
		_melee = GetComponent<MeleeWeapon>();
		_dodge = GetComponent<PlayerDodge>();
		_style = GetComponent<StyleMeter>();
		BuildUI();
	}

	private void OnEnable()
	{
		_health.Damaged += OnDamaged;
		_health.Healed += RefreshHealth;
		foreach (Firearm firearm in _firearms) firearm.Changed += RefreshAmmo;
		if (_switcher != null) _switcher.Switched += OnSwitched;
		if (_dodge != null) _dodge.PerfectDodged += OnPerfectDodged;
		if (_style != null)
		{
			_style.RankChanged += OnRankChanged;
			_style.Callout += OnStyleCallout;
		}
	}

	private void OnDisable()
	{
		_health.Damaged -= OnDamaged;
		_health.Healed -= RefreshHealth;
		foreach (Firearm firearm in _firearms) firearm.Changed -= RefreshAmmo;
		if (_switcher != null) _switcher.Switched -= OnSwitched;
		if (_dodge != null) _dodge.PerfectDodged -= OnPerfectDodged;
		if (_style != null)
		{
			_style.RankChanged -= OnRankChanged;
			_style.Callout -= OnStyleCallout;
		}
	}

	private void Start()
	{
		RefreshHealth();
		RefreshAmmo();
	}

	private void Update()
	{
		if (_flashAlpha > 0f)
		{
			_flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, Time.unscaledDeltaTime * 1.5f);
			_damageFlash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAlpha);
		}

		// dodge cooldown: a thin bar under the crosshair that empties as the dodge recharges
		float cooldown = _dodge != null && _dodge.enabled ? _dodge.CooldownRemaining01 : 0f;
		_dodgeCooldownFill.parent.gameObject.SetActive(cooldown > 0f);
		_dodgeCooldownFill.anchorMax = new Vector2(cooldown, 1f);

		bool heavyReady = _melee != null && _melee.enabled && _melee.IsHeavyReady;
		bool counter = _dodge != null && _dodge.IsCounterWindow;
		_crosshair.color = heavyReady ? HeavyReadyColor : counter ? CounterColor : CrosshairColor;
		float size = heavyReady ? 12f : 6f;
		_crosshair.rectTransform.sizeDelta = Vector2.Lerp(_crosshair.rectTransform.sizeDelta, new Vector2(size, size), Time.deltaTime * 20f);

		UpdateStyle();
	}

	private void UpdateStyle()
	{
		bool visible = _style != null && _style.Rank >= 0;
		_styleRoot.SetActive(visible);
		if (!visible) return;

		_styleFill.anchorMax = new Vector2(_style.RankProgress, 1f);

		// the letter pops when the rank changes
		_rankPunch = Mathf.MoveTowards(_rankPunch, 0f, Time.unscaledDeltaTime * 4f);
		_styleLetter.rectTransform.localScale = Vector3.one * (1f + 0.5f * _rankPunch * _rankPunch);

		float calloutLeft = _calloutEndTime - Time.unscaledTime;
		_styleCallout.gameObject.SetActive(calloutLeft > 0f);
		Color calloutColor = _styleCallout.color;
		calloutColor.a = Mathf.Clamp01(calloutLeft / 0.3f);
		_styleCallout.color = calloutColor;
	}

	private void OnRankChanged(int rank)
	{
		if (rank < 0) return;

		_styleLetter.text = _style.RankLetter;
		_styleName.text = _style.RankName.ToUpperInvariant();

		Color color = RankColor(rank);
		_styleLetter.color = color;
		_styleName.color = color;
		_styleFillImage.color = color;
		_rankPunch = 1f;
	}

	private Color RankColor(int rank)
	{
		// S and above share the top colour; D to A blend from low to high
		const int topRank = 4;
		if (rank >= topRank) return StyleTopColor;
		return Color.Lerp(StyleLowColor, StyleHighColor, rank / (float)(topRank - 1));
	}

	private void OnStyleCallout(string text)
	{
		_styleCallout.text = text;
		_calloutEndTime = Time.unscaledTime + CalloutTime;
	}

	public void ShowDeath()
	{
		_deathText.gameObject.SetActive(true);
		_crosshair.gameObject.SetActive(false);
	}

	private void OnDamaged(DamageInfo info)
	{
		_flashAlpha = 0.45f;
		_flashColor = new Color(0.6f, 0f, 0f);
		RefreshHealth();
	}

	private void OnPerfectDodged(EnemyController attacker)
	{
		_flashAlpha = 0.35f;
		_flashColor = PerfectDodgeFlashColor;
	}

	private void RefreshHealth()
	{
		_healthFill.anchorMax = new Vector2(_health.Normalized, 1f);
	}

	private void OnSwitched(Firearm firearm) => RefreshAmmo();

	// shows the equipped firearm (or the one being drawn)
	private void RefreshAmmo()
	{
		Firearm firearm = _switcher != null ? _switcher.Current : _firearms.Length > 0 ? _firearms[0] : null;
		if (firearm == null)
		{
			_ammoText.text = "";
			_weaponText.text = "";
			return;
		}
		_weaponText.text = firearm.Data.DisplayName.ToUpperInvariant();
		_ammoText.text = firearm.IsReloading ? "RELOADING" : $"{firearm.Ammo} / {firearm.ReserveAmmo}";
		// red when there's nothing left to load
		_ammoText.color = firearm.ReserveAmmo == 0 && !firearm.IsReloading ? EmptyAmmoColor : Color.white;
	}

	private void BuildUI()
	{
		var canvasGO = new GameObject("PlayerHUD", typeof(Canvas), typeof(CanvasScaler));
		var canvas = canvasGO.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 10;
		var scaler = canvasGO.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		RectTransform root = canvasGO.GetComponent<RectTransform>();

		Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

		// damage flash (full screen, behind everything else)
		_damageFlash = CreateImage("DamageFlash", root, new Color(0.6f, 0f, 0f, 0f));
		Stretch(_damageFlash.rectTransform);
		_damageFlash.raycastTarget = false;

		// crosshair
		_crosshair = CreateImage("Crosshair", root, CrosshairColor);
		_crosshair.rectTransform.sizeDelta = new Vector2(6f, 6f);

		// dodge cooldown bar, just under the crosshair
		Image cooldownBack = CreateImage("DodgeCooldown", root, new Color(0f, 0f, 0f, 0.5f));
		cooldownBack.rectTransform.anchoredPosition = new Vector2(0f, -22f);
		cooldownBack.rectTransform.sizeDelta = new Vector2(40f, 4f);
		Image cooldownFill = CreateImage("Fill", cooldownBack.rectTransform, new Color(1f, 1f, 1f, 0.8f));
		_dodgeCooldownFill = cooldownFill.rectTransform;
		Stretch(_dodgeCooldownFill);
		cooldownBack.gameObject.SetActive(false);

		// health bar, bottom left
		Image healthBack = CreateImage("HealthBar", root, new Color(0f, 0f, 0f, 0.6f));
		RectTransform backRect = healthBack.rectTransform;
		backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 0f);
		backRect.pivot = new Vector2(0f, 0f);
		backRect.anchoredPosition = new Vector2(40f, 40f);
		backRect.sizeDelta = new Vector2(360f, 24f);

		Image healthFill = CreateImage("Fill", backRect, HealthColor);
		_healthFill = healthFill.rectTransform;
		Stretch(_healthFill);
		_healthFill.offsetMin = new Vector2(3f, 3f);
		_healthFill.offsetMax = new Vector2(-3f, -3f);

		// ammo, bottom right
		_ammoText = CreateText("Ammo", root, font, 40, TextAnchor.LowerRight);
		RectTransform ammoRect = _ammoText.rectTransform;
		ammoRect.anchorMin = ammoRect.anchorMax = new Vector2(1f, 0f);
		ammoRect.pivot = new Vector2(1f, 0f);
		ammoRect.anchoredPosition = new Vector2(-40f, 32f);
		ammoRect.sizeDelta = new Vector2(400f, 60f);

		// equipped weapon name, just above the ammo count
		_weaponText = CreateText("Weapon", root, font, 22, TextAnchor.LowerRight);
		RectTransform weaponRect = _weaponText.rectTransform;
		weaponRect.anchorMin = weaponRect.anchorMax = new Vector2(1f, 0f);
		weaponRect.pivot = new Vector2(1f, 0f);
		weaponRect.anchoredPosition = new Vector2(-40f, 92f);
		weaponRect.sizeDelta = new Vector2(400f, 30f);
		_weaponText.color = new Color(1f, 1f, 1f, 0.7f);

		BuildStyleMeter(root, font);

		// death message
		_deathText = CreateText("DeathText", root, font, 96, TextAnchor.MiddleCenter);
		_deathText.text = "YOU DIED";
		_deathText.color = new Color(0.8f, 0.05f, 0.05f);
		Stretch(_deathText.rectTransform);
		_deathText.gameObject.SetActive(false);
	}

	// style rank, right edge of the screen: big letter, rank name, progress bar, callout
	private void BuildStyleMeter(RectTransform root, Font font)
	{
		var styleGO = new GameObject("StyleMeter", typeof(RectTransform));
		styleGO.transform.SetParent(root, false);
		RectTransform styleRect = styleGO.GetComponent<RectTransform>();
		styleRect.anchorMin = styleRect.anchorMax = new Vector2(1f, 0.5f);
		styleRect.pivot = new Vector2(1f, 0.5f);
		styleRect.anchoredPosition = new Vector2(-40f, 80f);
		styleRect.sizeDelta = new Vector2(260f, 200f);
		_styleRoot = styleGO;

		_styleLetter = CreateText("Rank", styleRect, font, 96, TextAnchor.LowerRight);
		PlaceInStyle(_styleLetter.rectTransform, 0f, 110f);
		// scale from the right edge so the pop doesn't shift the letter sideways
		_styleLetter.rectTransform.pivot = new Vector2(1f, 0.5f);
		_styleLetter.rectTransform.anchoredPosition = new Vector2(0f, -55f);
		_styleLetter.fontStyle = FontStyle.Bold;
		_styleLetter.verticalOverflow = VerticalWrapMode.Overflow;

		_styleName = CreateText("RankName", styleRect, font, 22, TextAnchor.UpperRight);
		PlaceInStyle(_styleName.rectTransform, -110f, 30f);

		Image barBack = CreateImage("Bar", styleRect, new Color(0f, 0f, 0f, 0.5f));
		RectTransform barRect = barBack.rectTransform;
		barRect.anchorMin = barRect.anchorMax = new Vector2(1f, 1f);
		barRect.pivot = new Vector2(1f, 1f);
		barRect.anchoredPosition = new Vector2(0f, -144f);
		barRect.sizeDelta = new Vector2(180f, 6f);
		_styleFillImage = CreateImage("Fill", barBack.rectTransform, StyleLowColor);
		_styleFill = _styleFillImage.rectTransform;
		Stretch(_styleFill);

		_styleCallout = CreateText("Callout", styleRect, font, 24, TextAnchor.UpperRight);
		PlaceInStyle(_styleCallout.rectTransform, -158f, 32f);
		_styleCallout.fontStyle = FontStyle.Italic;
		_styleCallout.gameObject.SetActive(false);

		styleGO.SetActive(false);
	}

	// a full-width row of the style block, its top edge y pixels below the block's top
	private static void PlaceInStyle(RectTransform rect, float y, float height)
	{
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(1f, 1f);
		rect.anchoredPosition = new Vector2(0f, y);
		rect.sizeDelta = new Vector2(0f, height);
	}

	private static Image CreateImage(string name, RectTransform parent, Color color)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, false);
		var image = go.GetComponent<Image>();
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	private static Text CreateText(string name, RectTransform parent, Font font, int size, TextAnchor anchor)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		var text = go.GetComponent<Text>();
		text.font = font;
		text.fontSize = size;
		text.alignment = anchor;
		text.color = Color.white;
		text.raycastTarget = false;
		return text;
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}
}
