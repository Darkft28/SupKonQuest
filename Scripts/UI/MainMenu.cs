using Godot;

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

		if (_playButton == null || _optionsButton == null || _quitButton == null)
			GD.PushError("MainMenu: required buttons not found. Check node names under TextureRect/MarginContainer/VBoxContainer.");

		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;
		}

		if (AudioSettings.Instance != null)
		{
			AudioSettings.Instance.AudioSettingsChanged += UpdateTexts;
			AudioSettings.Instance.EnsureMenuMusicPlaying();
		}

		UpdateTexts();
	}

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
		{
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
		}

		if (AudioSettings.Instance != null)
		{
			AudioSettings.Instance.AudioSettingsChanged -= UpdateTexts;
		}
	}
}
