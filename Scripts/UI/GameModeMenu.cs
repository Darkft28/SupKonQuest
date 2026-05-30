using Godot;

public partial class GameModeMenu : Control
{
	private Button _soloButton;
	private Button _multiButton;
	private Button _backButton;
	private Button _langButton;

	// Popup paramètres
	private ColorRect _overlay;
	private GameState.MapType _selectedMapType = GameState.MapType.Irridium;
	private CheckBox _fastModeCheckBox;
	private AIController.Difficulty _selectedDifficulty = AIController.Difficulty.Medium;
	private Label _soloPopupTitle;
	private Label _mapLabel;
	private Label _diffLabel;
	private Button[] _mapButtons;
	private Button[] _diffButtons;
	private Button _soloCancelBtn;
	private Button _soloLaunchBtn;

	private static string L(string key) =>
		LocalizationManager.Instance?.GetText(key) ?? key;

	public override void _Ready()
	{
		_soloButton = GetNode<Button>("Background/MarginContainer/VBoxContainer/SoloButton");
		_multiButton = GetNode<Button>("Background/MarginContainer/VBoxContainer/MultiButton");
		_backButton = GetNode<Button>("Background/MarginContainer/VBoxContainer/BackButton");
		_langButton = GetNode<Button>("LangButton");

		_soloButton.Pressed += ShowSettingsPopup;
		_multiButton.Pressed += OnMultiPressed;
		_backButton.Pressed += OnBackPressed;
		_langButton.Pressed += OnLangPressed;

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;

		AudioSettings.Instance?.EnsureMenuMusicPlaying();

		CreateSettingsPopup();
		UpdateTexts();
	}

	// Popup

	private void CreateSettingsPopup()
	{
		// Fond sombre couvrant tout l'écran
		_overlay = new ColorRect();
		_overlay.Color = new Color(0, 0, 0, 0.72f);
		_overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_overlay.Visible = false;
		AddChild(_overlay);

		// Panneau central - PanelContainer s'adapte à la hauteur du contenu
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;
		_overlay.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 28);
		margin.AddThemeConstantOverride("margin_right", 28);
		margin.AddThemeConstantOverride("margin_top", 22);
		margin.AddThemeConstantOverride("margin_bottom", 22);
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.CustomMinimumSize = new Vector2(420, 0);
		vbox.AddThemeConstantOverride("separation", 14);
		margin.AddChild(vbox);

		// Titre
		_soloPopupTitle = new Label();
		_soloPopupTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_soloPopupTitle.AddThemeFontSizeOverride("font_size", 22);
		vbox.AddChild(_soloPopupTitle);

		vbox.AddChild(new HSeparator());

		// Sélection de la carte
		_mapLabel = new Label();
		vbox.AddChild(_mapLabel);

		var mapHBox = new HBoxContainer();
		mapHBox.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(mapHBox);

		var mapGroup = new ButtonGroup();
		var mapKeys = new[] { "map_irridium", "map_alabasta", "map_torskey" };
		var mapValues = new[] { GameState.MapType.Irridium, GameState.MapType.Alabasta, GameState.MapType.Torskey };
		_mapButtons = new Button[3];
		for (int i = 0; i < 3; i++)
		{
			var btn = new Button();
			btn.ToggleMode = true;
			btn.ButtonGroup = mapGroup;
			btn.ButtonPressed = (mapValues[i] == _selectedMapType);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			UIStyle.ApplyStone(btn);
			var captured = mapValues[i];
			btn.Pressed += () => { _selectedMapType = captured; };
			mapHBox.AddChild(btn);
			_mapButtons[i] = btn;
		}

		vbox.AddChild(new HSeparator());

		// Difficulté IA
		_diffLabel = new Label();
		vbox.AddChild(_diffLabel);

		var diffHBox = new HBoxContainer();
		diffHBox.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(diffHBox);

		var diffGroup = new ButtonGroup();
		var diffKeys = new[] { "ai_easy", "ai_medium", "ai_hard" };
		var diffValues = new[] { AIController.Difficulty.Easy, AIController.Difficulty.Medium, AIController.Difficulty.Hard };
		_diffButtons = new Button[3];
		for (int i = 0; i < 3; i++)
		{
			var btn = new Button();
			btn.ToggleMode = true;
			btn.ButtonGroup = diffGroup;
			btn.ButtonPressed = (diffValues[i] == _selectedDifficulty);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			UIStyle.ApplyStone(btn);
			var captured = diffValues[i];
			btn.Pressed += () => { _selectedDifficulty = captured; };
			diffHBox.AddChild(btn);
			_diffButtons[i] = btn;
		}

		vbox.AddChild(new HSeparator());

		// Mode test : vitesse x3
		_fastModeCheckBox = new CheckBox();
		vbox.AddChild(_fastModeCheckBox);

		vbox.AddChild(new HSeparator());

		// Boutons action
		var actionsHBox = new HBoxContainer();
		actionsHBox.AddThemeConstantOverride("separation", 12);
		vbox.AddChild(actionsHBox);

		_soloCancelBtn = new Button();
		_soloCancelBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		UIStyle.ApplyStone(_soloCancelBtn);
		_soloCancelBtn.Pressed += () => _overlay.Visible = false;
		actionsHBox.AddChild(_soloCancelBtn);

		_soloLaunchBtn = new Button();
		_soloLaunchBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		UIStyle.ApplyStone(_soloLaunchBtn);
		_soloLaunchBtn.Pressed += OnLaunchPressed;
		actionsHBox.AddChild(_soloLaunchBtn);
	}

	private void ShowSettingsPopup()
	{
		_overlay.Visible = true;
	}

	private void OnLaunchPressed()
	{
		_overlay.Visible = false;
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		Engine.TimeScale = _fastModeCheckBox.ButtonPressed ? 3.0 : 1.0;
		gameState?.StartSoloGame(_selectedMapType, _fastModeCheckBox.ButtonPressed, _selectedDifficulty);
	}

	// Navigation

	private void OnMultiPressed()
	{
		NavigateToLobby();
	}

	private void NavigateToLobby()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		gameState?.ConfigureOnlineLobby();
		GetTree().ChangeSceneToFile("res://Scenes/Auth.tscn");
	}

	private void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	private void OnLangPressed()
	{
		LocalizationManager.Instance?.CycleLanguage();
	}

	private void UpdateTexts()
	{
		if (LocalizationManager.Instance == null) return;

		_soloButton.Text = LocalizationManager.Instance.GetText("solo");
		_multiButton.Text = LocalizationManager.Instance.GetText("multi");
		_backButton.Text = LocalizationManager.Instance.GetText("back");
		_langButton.Text = LocalizationManager.Instance.GetLanguageCode();

		if (_soloPopupTitle != null)
			_soloPopupTitle.Text = L("solo_settings_title");
		if (_mapLabel != null)
			_mapLabel.Text = L("solo_map_label");
		if (_diffLabel != null)
			_diffLabel.Text = L("solo_ai_difficulty");
		if (_mapButtons != null)
		{
			string[] mapKeys = { "map_irridium", "map_alabasta", "map_torskey" };
			for (int i = 0; i < _mapButtons.Length && i < mapKeys.Length; i++)
				_mapButtons[i].Text = L(mapKeys[i]);
		}
		if (_diffButtons != null)
		{
			string[] diffKeys = { "ai_easy", "ai_medium", "ai_hard" };
			for (int i = 0; i < _diffButtons.Length && i < diffKeys.Length; i++)
				_diffButtons[i].Text = L(diffKeys[i]);
		}
		if (_fastModeCheckBox != null)
			_fastModeCheckBox.Text = L("solo_fast_mode");
		if (_soloCancelBtn != null)
			_soloCancelBtn.Text = L("cancel");
		if (_soloLaunchBtn != null)
			_soloLaunchBtn.Text = L("launch");
	}

	public override void _ExitTree()
	{
		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
	}
}
