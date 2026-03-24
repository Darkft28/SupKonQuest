using Godot;

public partial class MainMenu : Control
{
	private Button _playButton;
	private Button _optionsButton;
	private Button _quitButton;
	private Button _langButton;

	public override void _Ready()
	{
		var vbox = GetNode<VBoxContainer>("TextureRect/MarginContainer/VBoxContainer");
		_playButton = vbox.GetNode<Button>("Button");
		_optionsButton = vbox.GetNode<Button>("Button2");
		_quitButton = vbox.GetNode<Button>("Button3");
		_langButton = GetNode<Button>("LangButton");

		_langButton.Pressed += OnLangPressed;
		_optionsButton.Pressed += OnLangPressed;

		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;
		}

		UpdateTexts();
	}

	private void UpdateTexts()
	{
		if (LocalizationManager.Instance == null) return;

		_playButton.Text = LocalizationManager.Instance.GetText("play");
		_optionsButton.Text = LocalizationManager.Instance.GetText("options");
		_quitButton.Text = LocalizationManager.Instance.GetText("quit");
		_langButton.Text = LocalizationManager.Instance.GetLanguageCode();
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
