using Godot;

public partial class GameModeMenu : Control
{
	private Label _titleLabel;
	private Button _soloButton;
	private Button _multiButton;
	private Button _iaButton;
	private Button _backButton;
	private Button _langButton;

	public override void _Ready()
	{
		_titleLabel = GetNode<Label>("VBoxContainer/TitleLabel");
		_soloButton = GetNode<Button>("VBoxContainer/SoloButton");
		_multiButton = GetNode<Button>("VBoxContainer/MultiButton");
		_iaButton = GetNode<Button>("VBoxContainer/IAButton");
		_backButton = GetNode<Button>("VBoxContainer/BackButton");
		_langButton = GetNode<Button>("LangButton");

		_soloButton.Pressed += OnSoloPressed;
		_multiButton.Pressed += OnMultiPressed;
		_iaButton.Pressed += OnIAPressed;
		_backButton.Pressed += OnBackPressed;
		_langButton.Pressed += OnLangPressed;

		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;
		}

		UpdateTexts();
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

	private void OnSoloPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
	}

	private void OnMultiPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
	}

	private void OnIAPressed()
	{
		// TODO: Implémenter le mode IA
		GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
	}

	private void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	private void OnLangPressed()
	{
		LocalizationManager.Instance?.CycleLanguage();
	}

	public override void _ExitTree()
	{
		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
		}
	}
}
