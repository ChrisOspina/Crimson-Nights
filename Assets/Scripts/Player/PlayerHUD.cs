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

	private Health _health;
	private Revolver _revolver;
	private MeleeWeapon _melee;
	private PlayerDodge _dodge;

	private RectTransform _healthFill;
	private Text _ammoText;
	private Image _crosshair;
	private Image _damageFlash;
	private Color _flashColor;
	private RectTransform _dodgeCooldownFill;
	private Text _deathText;
	private float _flashAlpha;

	private void Awake()
	{
		_health = GetComponent<Health>();
		_revolver = GetComponent<Revolver>();
		_melee = GetComponent<MeleeWeapon>();
		_dodge = GetComponent<PlayerDodge>();
		BuildUI();
	}

	private void OnEnable()
	{
		_health.Damaged += OnDamaged;
		_health.Healed += RefreshHealth;
		if (_revolver != null) _revolver.Changed += RefreshAmmo;
		if (_dodge != null) _dodge.PerfectDodged += OnPerfectDodged;
	}

	private void OnDisable()
	{
		_health.Damaged -= OnDamaged;
		_health.Healed -= RefreshHealth;
		if (_revolver != null) _revolver.Changed -= RefreshAmmo;
		if (_dodge != null) _dodge.PerfectDodged -= OnPerfectDodged;
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

	private void RefreshAmmo()
	{
		if (_revolver == null)
		{
			_ammoText.text = "";
			return;
		}
		_ammoText.text = _revolver.IsReloading ? "RELOADING" : $"{_revolver.Ammo} / {_revolver.ReserveAmmo}";
		// red when there's nothing left to load
		_ammoText.color = _revolver.ReserveAmmo == 0 && !_revolver.IsReloading ? EmptyAmmoColor : Color.white;
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

		// death message
		_deathText = CreateText("DeathText", root, font, 96, TextAnchor.MiddleCenter);
		_deathText.text = "YOU DIED";
		_deathText.color = new Color(0.8f, 0.05f, 0.05f);
		Stretch(_deathText.rectTransform);
		_deathText.gameObject.SetActive(false);
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
