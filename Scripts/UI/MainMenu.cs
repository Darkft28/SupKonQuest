using Godot;
using System.Collections.Generic;

public partial class MainMenu : Control
{
	private Button _playButton;
	private Button _optionsButton;
	private Button _quitButton;
	private Button _langButton;
	private ColorRect _optionsOverlay;
	private Label _optionsTitleLabel;
	private Button _popupLangButton;
	private Button _musicButton;
	private Button _sfxButton;
	private Button _closeOptionsButton;

	// Panneau remapping
	private Button _controlsButton;
	private ColorRect _keybindingsPanel;
	private Dictionary<string, Button> _keyButtons; // clé = nom d'action complet
	private string _listeningAction;

	public override void _Ready()
	{
		var vbox = GetNode<VBoxContainer>("TextureRect/MarginContainer/VBoxContainer");
		_playButton = FindButton(vbox, "PlayButton", "Button");
		_optionsButton = FindButton(vbox, "OptionButton", "OptionsButton", "Button2");
		_quitButton = FindButton(vbox, "ExitButton", "QuitButton", "Button3");
		_langButton = GetNodeOrNull<Button>("LangButton");
		_optionsOverlay = GetNodeOrNull<ColorRect>("OptionsOverlay");
		_optionsTitleLabel = GetNodeOrNull<Label>("OptionsOverlay/PanelContainer/MarginContainer/VBoxContainer/TitleLabel");
		_popupLangButton = GetNodeOrNull<Button>("OptionsOverlay/PanelContainer/MarginContainer/VBoxContainer/LanguageButton");
		_musicButton = GetNodeOrNull<Button>("OptionsOverlay/PanelContainer/MarginContainer/VBoxContainer/MusicButton");
		_sfxButton = GetNodeOrNull<Button>("OptionsOverlay/PanelContainer/MarginContainer/VBoxContainer/SfxButton");
		_closeOptionsButton = GetNodeOrNull<Button>("OptionsOverlay/PanelContainer/MarginContainer/VBoxContainer/CloseButton");

		if (_langButton != null)
			_langButton.Pressed += OnLangPressed;
		if (_optionsButton != null)
			_optionsButton.Pressed += ShowOptionsPopup;
		if (_popupLangButton != null)
			_popupLangButton.Pressed += OnLangPressed;
		if (_musicButton != null)
			_musicButton.Pressed += OnMusicPressed;
		if (_sfxButton != null)
			_sfxButton.Pressed += OnSfxPressed;
		if (_closeOptionsButton != null)
			_closeOptionsButton.Pressed += HideOptionsPopup;

		// Bouton "Contrôles" inséré avant CloseButton dans l'overlay Options
		var optionsVBox = GetNodeOrNull<VBoxContainer>("OptionsOverlay/PanelContainer/MarginContainer/VBoxContainer");
		if (optionsVBox != null)
		{
			_controlsButton = new Button();
			_controlsButton.Text = "Contrôles";
			_controlsButton.CustomMinimumSize = new Vector2(304, 56);
			_controlsButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			_controlsButton.AddThemeFontSizeOverride("font_size", 20);
			UIStyle.ApplyStone(_controlsButton);
			_controlsButton.Pressed += ShowKeybindingsPanel;
			optionsVBox.AddChild(_controlsButton);
			optionsVBox.MoveChild(_controlsButton, optionsVBox.GetChildCount() - 2);
		}

		BuildKeybindingsPanel();

		if (_playButton == null || _optionsButton == null || _quitButton == null)
			GD.PushError("MainMenu: required buttons not found. Check node names under TextureRect/MarginContainer/VBoxContainer.");

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;

		if (AudioSettings.Instance != null)
		{
			AudioSettings.Instance.AudioSettingsChanged += UpdateTexts;
			AudioSettings.Instance.EnsureMenuMusicPlaying();
		}

		UpdateTexts();
	}

	// ─── Panneau de remapping ───────────────────────────────────────────────

	private void BuildKeybindingsPanel()
	{
		_keybindingsPanel = new ColorRect();
		_keybindingsPanel.Color = new Color(0f, 0f, 0f, 0.75f);
		_keybindingsPanel.AnchorRight = 1.0f;
		_keybindingsPanel.AnchorBottom = 1.0f;
		_keybindingsPanel.Visible = false;
		AddChild(_keybindingsPanel);

		var center = new CenterContainer();
		center.AnchorRight = 1.0f;
		center.AnchorBottom = 1.0f;
		_keybindingsPanel.AddChild(center);

		var panelContainer = new PanelContainer();
		panelContainer.CustomMinimumSize = new Vector2(460, 0);
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.12f, 0.10f, 0.08f, 0.96f);
		panelStyle.BorderColor = new Color(0.6f, 0.45f, 0.15f, 1f);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.CornerRadiusTopLeft = panelStyle.CornerRadiusTopRight =
			panelStyle.CornerRadiusBottomLeft = panelStyle.CornerRadiusBottomRight = 6;
		panelContainer.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(panelContainer);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		panelContainer.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		margin.AddChild(vbox);

		var title = new Label();
		title.Text = "Contrôles — Sélection par type";
		title.AddThemeFontSizeOverride("font_size", 18);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.42f, 1f));
		vbox.AddChild(title);

		vbox.AddChild(new HSeparator());

		// Zone scrollable pour les lignes
		var scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(0, 350);
		scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		vbox.AddChild(scroll);

		var innerVbox = new VBoxContainer();
		innerVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		innerVbox.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(innerVbox);

		_keyButtons = new Dictionary<string, Button>();

		AddSectionLabel(innerVbox, "Unités");
		foreach (string unitType in KeybindingsManager.UnitTypes)
			AddBindingRow(innerVbox, $"unit_macro_{unitType}", GetUnitLabel(unitType));

		innerVbox.AddChild(new HSeparator());

		AddSectionLabel(innerVbox, "Navires");
		foreach (string shipType in KeybindingsManager.ShipTypes)
			AddBindingRow(innerVbox, $"ship_macro_{shipType}", GetShipLabel(shipType));

		vbox.AddChild(new HSeparator());

		var closeBtn = new Button();
		closeBtn.Text = "Fermer";
		UIStyle.ApplyStone(closeBtn);
		closeBtn.Pressed += HideKeybindingsPanel;
		vbox.AddChild(closeBtn);
	}

	private void AddSectionLabel(VBoxContainer parent, string text)
	{
		var lbl = new Label();
		lbl.Text = text;
		lbl.AddThemeFontSizeOverride("font_size", 14);
		lbl.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.5f, 1f));
		parent.AddChild(lbl);
	}

	private void AddBindingRow(VBoxContainer parent, string action, string displayName)
	{
		var hbox = new HBoxContainer();
		parent.AddChild(hbox);

		var label = new Label();
		label.Text = displayName;
		label.CustomMinimumSize = new Vector2(140, 0);
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.75f, 1f));
		hbox.AddChild(label);

		var spacer = new Control();
		spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(spacer);

		var keyBtn = new Button();
		keyBtn.CustomMinimumSize = new Vector2(80, 0);
		Key currentKey = KeybindingsManager.Instance?.GetBindingForAction(action) ?? Key.None;
		keyBtn.Text = KeybindingsManager.KeyDisplayName(currentKey);
		UIStyle.ApplyStone(keyBtn);
		keyBtn.Pressed += () => StartListening(action);
		hbox.AddChild(keyBtn);
		_keyButtons[action] = keyBtn;
	}

	private static string GetUnitLabel(string unitType) => unitType switch
	{
		"Infantry"  => "Infanterie",
		"Support"   => "Support",
		"Range"     => "Tireurs",
		"Heal"      => "Soigneurs",
		"AntiArmor" => "Anti-Armure",
		"Mortar"    => "Mortier",
		"Heavy"     => "Lourd",
		"Tank"      => "Tank",
		_ => unitType,
	};

	private static string GetShipLabel(string shipType) => shipType switch
	{
		"Transport"  => "Transport",
		"Fregate"    => "Frégate",
		"Destroyer"  => "Destroyer",
		_ => shipType,
	};

	private void ShowKeybindingsPanel()
	{
		if (_keyButtons != null && KeybindingsManager.Instance != null)
			foreach (var (action, btn) in _keyButtons)
				btn.Text = KeybindingsManager.KeyDisplayName(
					KeybindingsManager.Instance.GetBindingForAction(action));

		_listeningAction = null;
		if (_keybindingsPanel != null)
			_keybindingsPanel.Visible = true;
	}

	private void HideKeybindingsPanel()
	{
		_listeningAction = null;
		if (_keybindingsPanel != null)
			_keybindingsPanel.Visible = false;
	}

	private void StartListening(string action)
	{
		if (_listeningAction != null && _keyButtons.TryGetValue(_listeningAction, out var oldBtn))
			oldBtn.Text = KeybindingsManager.KeyDisplayName(
				KeybindingsManager.Instance?.GetBindingForAction(_listeningAction) ?? Key.None);

		_listeningAction = action;
		_keyButtons[action].Text = "...";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_listeningAction == null) return;
		if (@event is not InputEventKey keyEv || !keyEv.Pressed || keyEv.Echo) return;

		if (keyEv.Keycode == Key.Escape)
		{
			if (_keyButtons.TryGetValue(_listeningAction, out var btn))
				btn.Text = KeybindingsManager.KeyDisplayName(
					KeybindingsManager.Instance?.GetBindingForAction(_listeningAction) ?? Key.None);
		}
		else
		{
			KeybindingsManager.Instance?.SetBindingForAction(_listeningAction, keyEv.Keycode);
			if (_keyButtons.TryGetValue(_listeningAction, out var btn))
				btn.Text = KeybindingsManager.KeyDisplayName(keyEv.Keycode);
		}

		_listeningAction = null;
		GetViewport().SetInputAsHandled();
	}

	// ─── Menu principal ─────────────────────────────────────────────────────

	public void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/GameModeMenu.tscn");
	}

	public void OnQuitPressed()
	{
		GetTree().Quit();
	}

	private void ShowOptionsPopup()
	{
		if (_optionsOverlay != null)
			_optionsOverlay.Visible = true;
	}

	private void HideOptionsPopup()
	{
		if (_optionsOverlay != null)
			_optionsOverlay.Visible = false;
	}

	private void OnMusicPressed()
	{
		AudioSettings.Instance?.ToggleMusic();
		UpdateTexts();
	}

	private void OnSfxPressed()
	{
		AudioSettings.Instance?.ToggleSfx();
		UpdateTexts();
	}

	private void UpdateTexts()
	{
		if (LocalizationManager.Instance == null) return;

		if (_playButton != null)
			_playButton.Text = LocalizationManager.Instance.GetText("play");
		if (_optionsButton != null)
			_optionsButton.Text = LocalizationManager.Instance.GetText("options");
		if (_quitButton != null)
			_quitButton.Text = LocalizationManager.Instance.GetText("quit");
		if (_langButton != null)
			_langButton.Text = LocalizationManager.Instance.GetLanguageCode();
		if (_optionsTitleLabel != null)
			_optionsTitleLabel.Text = LocalizationManager.Instance.GetText("options");

		if (_optionsOverlay != null)
		{
			if (_popupLangButton != null)
				_popupLangButton.Text = $"{LocalizationManager.Instance.GetText("language")}: {LocalizationManager.Instance.GetLanguageCode()}";
			if (_musicButton != null)
				_musicButton.Text = BuildToggleText("music", AudioSettings.Instance?.MusicEnabled ?? true);
			if (_sfxButton != null)
				_sfxButton.Text = BuildToggleText("sfx", AudioSettings.Instance?.SfxEnabled ?? true);
			if (_closeOptionsButton != null)
				_closeOptionsButton.Text = LocalizationManager.Instance.GetText("close");
		}
	}

	private string BuildToggleText(string key, bool enabled)
	{
		string stateKey = enabled ? "on" : "off";
		return $"{LocalizationManager.Instance.GetText(key)}: {LocalizationManager.Instance.GetText(stateKey)}";
	}

	private static Button FindButton(Node parent, params string[] candidateNames)
	{
		foreach (string name in candidateNames)
		{
			Button node = parent.GetNodeOrNull<Button>(name);
			if (node != null)
				return node;
		}
		return null;
	}

	private void OnLangPressed()
	{
		LocalizationManager.Instance?.CycleLanguage();
	}

	public override void _ExitTree()
	{
		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
		if (AudioSettings.Instance != null)
			AudioSettings.Instance.AudioSettingsChanged -= UpdateTexts;
	}
}
