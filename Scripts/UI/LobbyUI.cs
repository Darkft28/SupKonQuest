using Godot;
using System.Threading.Tasks;

public partial class LobbyUI : Control
{
	private Label _titleLabel;
	private Label _codeLabel;
	private LineEdit _codeInput;
	private Label _codeDisplayLabel;
	private Button _hostButton;
	private Button _joinButton;
	private Button _startButton;
	private Button _backButton;
	private Button _logoutButton;
	private Button _langButton;
	private Label _playersLabel;
	private ItemList _playerList;
	private Label _statusLabel;

	private NakamaService _nakamaService;
	private GameState _gameState;
	private bool _isAuthReady;
	private bool _isMatchmaking;
	private bool _inMatchLobby;

	public override void _Ready()
	{
		_nakamaService = GetNode<NakamaService>("/root/NakamaService");
		_gameState = GetNode<GameState>("/root/GameState");

		_titleLabel = GetNode<Label>("VBoxContainer/Title");
		_codeLabel = GetNode<Label>("VBoxContainer/ConnectionPanel/VBoxContainer/CodeContainer/Label");
		_codeInput = GetNode<LineEdit>("VBoxContainer/ConnectionPanel/VBoxContainer/CodeContainer/CodeInput");
		_codeDisplayLabel = GetNode<Label>("VBoxContainer/ConnectionPanel/VBoxContainer/CodeDisplay");
		_hostButton = GetNode<Button>("VBoxContainer/ConnectionPanel/VBoxContainer/ButtonContainer/HostButton");
		_joinButton = GetNode<Button>("VBoxContainer/ConnectionPanel/VBoxContainer/ButtonContainer/JoinButton");
		_startButton = GetNode<Button>("VBoxContainer/LobbyPanel/VBoxContainer/StartButton");
		_backButton = GetNode<Button>("VBoxContainer/BackButton");
		_langButton = GetNode<Button>("LangButton");
		_playersLabel = GetNode<Label>("VBoxContainer/LobbyPanel/VBoxContainer/PlayersLabel");
		_playerList = GetNode<ItemList>("VBoxContainer/LobbyPanel/VBoxContainer/PlayerList");
		_statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

		_logoutButton = new Button();
		_logoutButton.Text = GetText("auth_logout");
		_logoutButton.CustomMinimumSize = new Vector2(200, 48);
		UIStyle.ApplyStone(_logoutButton);
		var bottomHBox = _backButton.GetParent() as HBoxContainer;
		if (bottomHBox != null)
		{
			bottomHBox.AddChild(_logoutButton);
			bottomHBox.MoveChild(_logoutButton, 0);
		}
		else
		{
			AddChild(_logoutButton);
		}

		_hostButton.Pressed += OnSaveNicknamePressed;
		_joinButton.Pressed += OnMatchmakingPressed;
		_backButton.Pressed += OnBackPressed;
		_logoutButton.Pressed += OnLogoutPressed;
		_langButton.Pressed += OnLangPressed;

		_nakamaService.Authenticated += OnAuthenticated;
		_nakamaService.AuthenticationFailed += OnAuthenticationFailed;
		_nakamaService.MatchmakingStarted += OnMatchmakingStarted;
		_nakamaService.MatchmakingFailed += OnMatchmakingFailed;
		_nakamaService.MatchLobbyEntered += OnMatchLobbyEntered;
		_nakamaService.MatchLobbyTick += OnMatchLobbyTick;
		_nakamaService.MatchStarting += OnMatchStarting;
		_nakamaService.Disconnected += OnDisconnected;

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;

		AudioSettings.Instance?.EnsureMenuMusicPlaying();

		_startButton.Visible = false;
		_codeInput.Editable = true;
		_codeInput.Text = "";
		_codeInput.PlaceholderText = "Guest-01";
		_codeDisplayLabel.Text = "";

		UpdateTexts();
		SetButtonsEnabled(false);

		if (_nakamaService == null || !_nakamaService.IsAuthenticated)
		{
			RedirectToAuth(GetText("auth_not_authenticated"));
			return;
		}

		_isAuthReady = true;
		_hostButton.Visible = _nakamaService.IsGuestAccount;
		_codeInput.Editable = _nakamaService.IsGuestAccount;
		_codeLabel.Visible = _nakamaService.IsGuestAccount;
		OnAuthenticated(_nakamaService.UserId, _nakamaService.DisplayName);
	}

	public override void _ExitTree()
	{
		if (_nakamaService != null)
		{
			_nakamaService.Authenticated -= OnAuthenticated;
			_nakamaService.AuthenticationFailed -= OnAuthenticationFailed;
			_nakamaService.MatchmakingStarted -= OnMatchmakingStarted;
			_nakamaService.MatchmakingFailed -= OnMatchmakingFailed;
			_nakamaService.MatchLobbyEntered -= OnMatchLobbyEntered;
			_nakamaService.MatchLobbyTick -= OnMatchLobbyTick;
			_nakamaService.MatchStarting -= OnMatchStarting;
			_nakamaService.Disconnected -= OnDisconnected;
		}

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
	}

	private void RedirectToAuth(string message)
	{
		AuthSessionStore.Clear();
		_nakamaService?.Disconnect();
		if (!string.IsNullOrWhiteSpace(message))
			_gameState?.SetPendingAuthMessage(message);
		GetTree().ChangeSceneToFile("res://Scenes/Auth.tscn");
	}

	private void UpdateTexts()
	{
		if (LocalizationManager.Instance == null)
			return;

		_titleLabel.Text = LocalizationManager.Instance.GetText("lobby_title");
		_codeLabel.Text = LocalizationManager.Instance.GetText("nickname");
		_hostButton.Text = LocalizationManager.Instance.GetText("save_nickname");
		_joinButton.Text = LocalizationManager.Instance.GetText("find_match");
		_playersLabel.Text = LocalizationManager.Instance.GetText("players_connected");
		_backButton.Text = LocalizationManager.Instance.GetText("back");
		_logoutButton.Text = LocalizationManager.Instance.GetText("auth_logout");
		_langButton.Text = LocalizationManager.Instance.GetLanguageCode();
		_startButton.Visible = false;
	}

	private string GetText(string key)
	{
		return LocalizationManager.Instance?.GetText(key) ?? key;
	}

	private void SetButtonsEnabled(bool enabled)
	{
		_hostButton.Disabled = !enabled || _inMatchLobby || !_nakamaService.IsGuestAccount;
		_joinButton.Disabled = !enabled || _isMatchmaking || !_isAuthReady || _inMatchLobby;
		_logoutButton.Disabled = !enabled || _inMatchLobby;
	}

	private void OnLangPressed()
	{
		LocalizationManager.Instance?.CycleLanguage();
	}

	private async void OnSaveNicknamePressed()
	{
		if (_nakamaService == null || !_nakamaService.IsGuestAccount)
			return;

		string desiredName = _codeInput.Text.Trim();
		if (string.IsNullOrWhiteSpace(desiredName))
		{
			UpdateStatus(GetText("error_enter_code"));
			return;
		}

		SetButtonsEnabled(false);
		UpdateStatus(GetText("authenticating"));
		bool updated = await _nakamaService.UpdateUniqueUsernameAsync(desiredName);
		if (updated)
		{
			_codeInput.Text = _nakamaService.DisplayName;
			UpdatePlayerList();
			UpdateStatus($"{GetText("guest_connected")} : {_nakamaService.DisplayName}");
			SetButtonsEnabled(true);
		}
		else
		{
			SetButtonsEnabled(true);
		}
	}

	private async void OnMatchmakingPressed()
	{
		if (_nakamaService == null)
			return;

		if (!_nakamaService.IsAuthenticated)
		{
			RedirectToAuth(GetText("auth_not_authenticated"));
			return;
		}

		_isMatchmaking = true;
		SetButtonsEnabled(false);
		UpdateStatus(GetText("matchmaking_started"));
		await _nakamaService.StartMatchmakingAsync();
	}

	private async void OnBackPressed()
	{
		if (_nakamaService != null)
		{
			await _nakamaService.CancelMatchmakingAsync();
			_nakamaService.Disconnect();
		}

		_inMatchLobby = false;
		_gameState?.ClearOnlineSession();
		GetTree().ChangeSceneToFile("res://Scenes/GameModeMenu.tscn");
	}

	private async void OnLogoutPressed()
	{
		if (_nakamaService == null)
			return;

		await _nakamaService.CancelMatchmakingAsync();
		await _nakamaService.LogoutAsync();
		_gameState?.ClearOnlineSession();
		GetTree().ChangeSceneToFile("res://Scenes/Auth.tscn");
	}

	private void OnAuthenticated(string userId, string displayName)
	{
		_isAuthReady = true;
		string shortId = userId.Length > 8 ? userId.Substring(0, 8) : userId;
		_codeDisplayLabel.Text = $"{displayName} • {shortId}";
		UpdatePlayerList();
		SetButtonsEnabled(true);
		string prefix = _nakamaService.IsGuestAccount ? GetText("guest_connected") : GetText("connected_to_server");
		UpdateStatus($"{prefix} : {displayName}");
	}

	private void OnAuthenticationFailed(string reason)
	{
		_isAuthReady = false;
		_isMatchmaking = false;
		_inMatchLobby = false;
		SetButtonsEnabled(true);

		if (reason == "auth_session_expired" || reason == "auth_not_authenticated")
		{
			RedirectToAuth(GetText(reason));
			return;
		}

		string hint = "";
		string lower = reason?.ToLowerInvariant() ?? "";
		if (lower.Contains("connection") || lower.Contains("refused") || lower.Contains("timeout") || lower.Contains("host"))
			hint = " | Verifie nakama/host dans project.godot et que le serveur est joignable.";

		string display = reason.StartsWith("auth_") ? GetText(reason) : reason;
		UpdateStatus($"{GetText("connection_failed")} : {display}{hint}");
	}

	private void OnMatchmakingStarted(string ticket)
	{
		_isMatchmaking = true;
		UpdateStatus($"{GetText("matchmaking_started")} #{ticket}");
	}

	private void OnMatchmakingFailed(string reason)
	{
		_isMatchmaking = false;
		_inMatchLobby = false;
		SetButtonsEnabled(true);
		UpdateStatus($"{GetText("connection_failed")} : {reason}");
	}

	private void OnMatchLobbyEntered(string matchId, int pendingSeed)
	{
		_isMatchmaking = false;
		_inMatchLobby = true;
		SetButtonsEnabled(false);
		UpdatePlayerList();
		UpdateStatus($"{GetText("match_found")} — {GetText("waiting_players")}");
		GD.Print($"[LOBBY] In-match lobby matchId={matchId} seed={pendingSeed}");
	}

	private void OnMatchLobbyTick(int secondsRemaining, int playerCount)
	{
		UpdatePlayerList();
		string countdown = secondsRemaining < 0
			? GetText("waiting_server")
			: secondsRemaining > 0
				? $"{GetText("starting_in")} {secondsRemaining}s"
				: GetText("starting_soon");
		UpdateStatus($"{playerCount}/{NakamaService.MaxMatchPlayers} {GetText("players_connected").ToLower()} — {countdown}");
	}

	private void OnMatchStarting(string matchId, int localTeamId, int seed, int playerCount)
	{
		_isMatchmaking = false;
		_inMatchLobby = false;
		SetButtonsEnabled(true);
		UpdateStatus(GetText("starting_soon"));
		_gameState?.StartOnlineGameFromMatch(
			matchId,
			localTeamId,
			seed,
			playerCount,
			_nakamaService.UserId,
			_nakamaService.DisplayName);
	}

	private void OnDisconnected()
	{
		_isAuthReady = false;
		_isMatchmaking = false;
		_inMatchLobby = false;
		_codeDisplayLabel.Text = "";
		_playerList.Clear();
		SetButtonsEnabled(true);
		UpdateStatus(GetText("disconnected_from_server"));
	}

	private void UpdatePlayerList()
	{
		_playerList.Clear();

		if (_nakamaService == null || _nakamaService.MatchPlayers.Count == 0)
		{
			_playerList.AddItem(_nakamaService?.DisplayName ?? "Guest");
			return;
		}

		foreach (var player in _nakamaService.MatchPlayers)
		{
			string suffix = player.Key == _nakamaService.UserId ? " (Vous)" : "";
			_playerList.AddItem($"{player.Value}{suffix}");
		}
	}

	private void UpdateStatus(string message)
	{
		_statusLabel.Text = message;
	}
}
