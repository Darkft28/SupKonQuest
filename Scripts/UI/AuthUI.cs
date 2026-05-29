using Godot;
using System.Threading.Tasks;

public partial class AuthUI : Control
{
	private Label _titleLabel;
	private Label _statusLabel;
	private TabContainer _tabs;
	private Label _loginEmailLabel;
	private LineEdit _loginEmail;
	private Label _loginPasswordLabel;
	private LineEdit _loginPassword;
	private Button _loginButton;
	private Label _registerEmailLabel;
	private LineEdit _registerEmail;
	private Label _registerUsernameLabel;
	private LineEdit _registerUsername;
	private Label _registerPasswordLabel;
	private LineEdit _registerPassword;
	private Label _registerConfirmPasswordLabel;
	private LineEdit _registerConfirmPassword;
	private Button _registerButton;
	private Button _guestButton;
	private Button _backButton;
	private Button _langButton;

	private NakamaService _nakamaService;
	private bool _isBusy;

	public override void _Ready()
	{
		_nakamaService = GetNode<NakamaService>("/root/NakamaService");
		BuildUi();

		_loginButton.Pressed += OnLoginPressed;
		_registerButton.Pressed += OnRegisterPressed;
		_guestButton.Pressed += OnGuestPressed;
		_backButton.Pressed += OnBackPressed;
		_langButton.Pressed += OnLangPressed;

		_nakamaService.Authenticated += OnAuthenticated;
		_nakamaService.AuthenticationFailed += OnAuthenticationFailed;

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;

		AudioSettings.Instance?.EnsureMenuMusicPlaying();
		UpdateTexts();

		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		string pending = gameState?.TakePendingAuthMessage() ?? "";
		if (!string.IsNullOrWhiteSpace(pending))
			SetStatus(pending);

		SetBusy(true, GetText("auth_restoring"));
		_ = TryRestoreAndContinueAsync();
	}

	public override void _ExitTree()
	{
		if (_nakamaService != null)
		{
			_nakamaService.Authenticated -= OnAuthenticated;
			_nakamaService.AuthenticationFailed -= OnAuthenticationFailed;
		}

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
	}

	private void BuildUi()
	{
		var bg = new TextureRect();
		bg.Texture = GD.Load<Texture2D>("res://Assets/Menu/Texture/Menu_bg.png");
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(520, 0);
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.08f, 0.07f, 0.06f, 0.88f);
		panelStyle.CornerRadiusTopLeft = 12;
		panelStyle.CornerRadiusTopRight = 12;
		panelStyle.CornerRadiusBottomLeft = 12;
		panelStyle.CornerRadiusBottomRight = 12;
		panelStyle.ContentMarginLeft = 24;
		panelStyle.ContentMarginRight = 24;
		panelStyle.ContentMarginTop = 20;
		panelStyle.ContentMarginBottom = 20;
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(panel);

		var rootVBox = new VBoxContainer();
		rootVBox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(rootVBox);

		_titleLabel = new Label();
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeFontSizeOverride("font_size", 28);
		rootVBox.AddChild(_titleLabel);

		_statusLabel = new Label();
		_statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_statusLabel.Modulate = new Color(0.9f, 0.45f, 0.45f);
		rootVBox.AddChild(_statusLabel);

		_tabs = new TabContainer();
		_tabs.CustomMinimumSize = new Vector2(0, 280);
		_tabs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		rootVBox.AddChild(_tabs);

		var loginPanel = new VBoxContainer();
		loginPanel.Name = "Login";
		loginPanel.AddThemeConstantOverride("separation", 10);
		_tabs.AddChild(loginPanel);

		(_loginEmail, _loginEmailLabel) = CreateLabeledLineEdit(loginPanel, secret: false);
		(_loginPassword, _loginPasswordLabel) = CreateLabeledLineEdit(loginPanel, secret: true);
		_loginButton = CreateStoneButton(loginPanel, "auth_login");
		_loginButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

		var registerPanel = new VBoxContainer();
		registerPanel.Name = "Register";
		registerPanel.AddThemeConstantOverride("separation", 10);
		_tabs.AddChild(registerPanel);

		(_registerEmail, _registerEmailLabel) = CreateLabeledLineEdit(registerPanel, secret: false);
		(_registerUsername, _registerUsernameLabel) = CreateLabeledLineEdit(registerPanel, secret: false);
		(_registerPassword, _registerPasswordLabel) = CreateLabeledLineEdit(registerPanel, secret: true);
		(_registerConfirmPassword, _registerConfirmPasswordLabel) = CreateLabeledLineEdit(registerPanel, secret: true);
		_registerButton = CreateStoneButton(registerPanel, "auth_register");
		_registerButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

		rootVBox.AddChild(new HSeparator());

		_guestButton = CreateStoneButton(rootVBox, "auth_guest");
		_guestButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

		var bottomRow = new HBoxContainer();
		bottomRow.AddThemeConstantOverride("separation", 12);
		bottomRow.Alignment = BoxContainer.AlignmentMode.Center;
		rootVBox.AddChild(bottomRow);

		_backButton = CreateStoneButton(bottomRow, "back");
		_langButton = CreateStoneButton(bottomRow, "language");
		_langButton.CustomMinimumSize = new Vector2(72, 48);
	}

	private static (LineEdit Edit, Label Label) CreateLabeledLineEdit(VBoxContainer parent, bool secret = false)
	{
		var label = new Label();
		parent.AddChild(label);

		var edit = new LineEdit();
		edit.CustomMinimumSize = new Vector2(0, 40);
		edit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (secret)
			edit.Secret = true;
		parent.AddChild(edit);
		return (edit, label);
	}

	private static Button CreateStoneButton(Node parent, string textKey)
	{
		var btn = new Button();
		btn.Text = textKey;
		btn.CustomMinimumSize = new Vector2(280, 52);
		btn.AddThemeFontSizeOverride("font_size", 18);
		UIStyle.ApplyStone(btn);
		parent.AddChild(btn);
		return btn;
	}

	private async Task TryRestoreAndContinueAsync()
	{
		if (_nakamaService == null)
		{
			SetBusy(false, GetText("connection_failed"));
			return;
		}

		if (!AuthSessionStore.HasStoredSession)
		{
			SetBusy(false, "");
			return;
		}

		bool restored = await _nakamaService.TryRestoreSessionAsync();
		if (restored)
			GoToLobby();
		else
			SetBusy(false, GetText("auth_session_expired"));
	}

	private async void OnLoginPressed()
	{
		if (_isBusy || _nakamaService == null)
			return;

		SetBusy(true, GetText("authenticating"));
		bool ok = await _nakamaService.LoginWithEmailAsync(
			_loginEmail.Text.Trim(),
			_loginPassword.Text);
		if (!ok)
			SetBusy(false, "");
	}

	private async void OnRegisterPressed()
	{
		if (_isBusy || _nakamaService == null)
			return;

		if (_registerPassword.Text != _registerConfirmPassword.Text)
		{
			SetStatus(GetText("auth_error_password_mismatch"));
			return;
		}

		SetBusy(true, GetText("authenticating"));
		bool ok = await _nakamaService.RegisterWithEmailAsync(
			_registerEmail.Text.Trim(),
			_registerUsername.Text.Trim(),
			_registerPassword.Text);
		if (!ok)
			SetBusy(false, "");
	}

	private async void OnGuestPressed()
	{
		if (_isBusy || _nakamaService == null)
			return;

		SetBusy(true, GetText("authenticating"));
		await _nakamaService.AuthenticateGuestAsync();
	}

	private void OnBackPressed()
	{
		if (_isBusy)
			return;

		GetTree().ChangeSceneToFile("res://Scenes/GameModeMenu.tscn");
	}

	private void OnLangPressed()
	{
		LocalizationManager.Instance?.CycleLanguage();
	}

	private void OnAuthenticated(string userId, string displayName)
	{
		GoToLobby();
	}

	private void OnAuthenticationFailed(string reason)
	{
		SetBusy(false, ResolveStatusMessage(reason));
	}

	private void GoToLobby()
	{
		SetBusy(false, "");
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
	}

	private void SetBusy(bool busy, string statusMessage)
	{
		_isBusy = busy;
		_loginButton.Disabled = busy;
		_registerButton.Disabled = busy;
		_guestButton.Disabled = busy;
		_backButton.Disabled = busy;
		_loginEmail.Editable = !busy;
		_loginPassword.Editable = !busy;
		_registerEmail.Editable = !busy;
		_registerUsername.Editable = !busy;
		_registerPassword.Editable = !busy;
		_registerConfirmPassword.Editable = !busy;
		SetStatus(statusMessage);
	}

	private void SetStatus(string message)
	{
		_statusLabel.Text = message ?? "";
		_statusLabel.Visible = !string.IsNullOrWhiteSpace(_statusLabel.Text);
	}

	private string ResolveStatusMessage(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			return GetText("connection_failed");

		if (reason.StartsWith("auth_") || reason == "connection_failed" || reason == "auth_not_authenticated")
			return GetText(reason);

		return reason;
	}

	private string GetText(string key)
	{
		return LocalizationManager.Instance?.GetText(key) ?? key;
	}

	private void UpdateTexts()
	{
		if (LocalizationManager.Instance == null)
			return;

		_titleLabel.Text = LocalizationManager.Instance.GetText("auth_title");
		_tabs.SetTabTitle(0, LocalizationManager.Instance.GetText("auth_login_tab"));
		_tabs.SetTabTitle(1, LocalizationManager.Instance.GetText("auth_register_tab"));
		_loginButton.Text = LocalizationManager.Instance.GetText("auth_login");
		_registerButton.Text = LocalizationManager.Instance.GetText("auth_register");
		_guestButton.Text = LocalizationManager.Instance.GetText("auth_guest");
		_backButton.Text = LocalizationManager.Instance.GetText("back");
		_langButton.Text = LocalizationManager.Instance.GetLanguageCode();

		_loginEmailLabel.Text = LocalizationManager.Instance.GetText("auth_email");
		_loginPasswordLabel.Text = LocalizationManager.Instance.GetText("auth_password");
		_registerEmailLabel.Text = LocalizationManager.Instance.GetText("auth_email");
		_registerUsernameLabel.Text = LocalizationManager.Instance.GetText("auth_username");
		_registerPasswordLabel.Text = LocalizationManager.Instance.GetText("auth_password");
		_registerConfirmPasswordLabel.Text = LocalizationManager.Instance.GetText("auth_confirm_password");
	}
}
