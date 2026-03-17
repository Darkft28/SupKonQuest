using Godot;

public partial class GameModeMenu : Control
{
	private Label _titleLabel;
	private Button _soloButton;
	private Button _multiButton;
	private Button _iaButton;
	private Button _backButton;
	private Button _langButton;

	// Popup paramètres
	private ColorRect _overlay;
	private Label _popupTitle;
	private VBoxContainer _difficultySection;
	private bool _pendingIsIA = false;
	private AIController.Difficulty _selectedDifficulty = AIController.Difficulty.Medium;
	private GameState.MapType _selectedMapType = GameState.MapType.Irridium;

	public override void _Ready()
	{
		_titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");
		_soloButton = GetNode<Button>("VBoxContainer/SoloButton");
		_multiButton = GetNode<Button>("VBoxContainer/MultiButton");
		_iaButton = GetNode<Button>("VBoxContainer/IAButton");
		_backButton = GetNode<Button>("VBoxContainer/BackButton");
		_langButton = GetNode<Button>("LangButton");

		_soloButton.Pressed += () => ShowSettingsPopup(true);
		_multiButton.Pressed += OnMultiPressed;
		_iaButton.Visible = false; // fusionné avec Solo
		_backButton.Pressed += OnBackPressed;
		_langButton.Pressed += OnLangPressed;

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;

		CreateSettingsPopup();
		UpdateTexts();
	}

	// ── Popup ────────────────────────────────────────────────────────────────

	private void CreateSettingsPopup()
	{
		// Fond sombre couvrant tout l'écran
		_overlay = new ColorRect();
		_overlay.Color = new Color(0, 0, 0, 0.72f);
		_overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_overlay.Visible = false;
		AddChild(_overlay);

		// Panneau central — PanelContainer s'adapte à la hauteur du contenu
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
		_popupTitle = new Label();
		_popupTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_popupTitle.AddThemeFontSizeOverride("font_size", 22);
		vbox.AddChild(_popupTitle);

		vbox.AddChild(new HSeparator());

		// Sélection de la carte
		var mapLabel = new Label();
		mapLabel.Text = "Carte";
		vbox.AddChild(mapLabel);

		var mapHBox = new HBoxContainer();
		mapHBox.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(mapHBox);

		var mapGroup = new ButtonGroup();
		var mapNames = new[] { "Irridium", "Alabasta" };
		var mapValues = new[] { GameState.MapType.Irridium, GameState.MapType.Alabasta };
		for (int i = 0; i < 2; i++)
		{
			var btn = new Button();
			btn.Text = mapNames[i];
			btn.ToggleMode = true;
			btn.ButtonGroup = mapGroup;
			btn.ButtonPressed = (mapValues[i] == _selectedMapType);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			var captured = mapValues[i];
			btn.Pressed += () => { _selectedMapType = captured; };
			mapHBox.AddChild(btn);
		}

		// Difficulté (mode IA uniquement)
		_difficultySection = new VBoxContainer();
		_difficultySection.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(_difficultySection);

		var diffLabel = new Label();
		diffLabel.Text = "Difficulté de l'IA";
		_difficultySection.AddChild(diffLabel);

		var diffHBox = new HBoxContainer();
		diffHBox.AddThemeConstantOverride("separation", 8);
		_difficultySection.AddChild(diffHBox);

		var diffGroup = new ButtonGroup();
		string[] diffNames = { "Facile", "Moyen", "Difficile" };
		AIController.Difficulty[] diffValues = {
			AIController.Difficulty.Easy,
			AIController.Difficulty.Medium,
			AIController.Difficulty.Hard
		};
		for (int i = 0; i < 3; i++)
		{
			var btn = new Button();
			btn.Text = diffNames[i];
			btn.ToggleMode = true;
			btn.ButtonGroup = diffGroup;
			btn.ButtonPressed = (diffValues[i] == _selectedDifficulty);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			var captured = diffValues[i];
			btn.Pressed += () => _selectedDifficulty = captured;
			diffHBox.AddChild(btn);
		}

		vbox.AddChild(new HSeparator());

		// Boutons action
		var actionsHBox = new HBoxContainer();
		actionsHBox.AddThemeConstantOverride("separation", 12);
		vbox.AddChild(actionsHBox);

		var cancelBtn = new Button();
		cancelBtn.Text = "Annuler";
		cancelBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		cancelBtn.Pressed += () => _overlay.Visible = false;
		actionsHBox.AddChild(cancelBtn);

		var launchBtn = new Button();
		launchBtn.Text = "Lancer";
		launchBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		launchBtn.Pressed += OnLaunchPressed;
		actionsHBox.AddChild(launchBtn);
	}

	private void ShowSettingsPopup(bool isIA)
	{
		_pendingIsIA = isIA;
		_popupTitle.Text = "Paramètres — Solo";
		_difficultySection.Visible = true;
		_overlay.Visible = true;
	}

	private void OnLaunchPressed()
	{
		_overlay.Visible = false;
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState != null)
		{
			gameState.LocalTeamId = 1;
			gameState.IsAIMode = _pendingIsIA;
			gameState.IsFreeForAll = true;
			gameState.AILevel = _selectedDifficulty;
			gameState.SelectedMapType = _selectedMapType;
		}
		GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
	}

	// ── Navigation ───────────────────────────────────────────────────────────

	private void OnMultiPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
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

		_titleLabel.Text = LocalizationManager.Instance.GetText("game_mode");
		_soloButton.Text = LocalizationManager.Instance.GetText("solo");
		_multiButton.Text = LocalizationManager.Instance.GetText("multi");
		_iaButton.Text = LocalizationManager.Instance.GetText("ia");
		_backButton.Text = LocalizationManager.Instance.GetText("back");
		_langButton.Text = LocalizationManager.Instance.GetLanguageCode();
	}

	public override void _ExitTree()
	{
		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
	}
}
