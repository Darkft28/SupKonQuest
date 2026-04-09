using Godot;
using System;
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
	private Button _langButton;
	private Label _playersLabel;
	private ItemList _playerList;
	private Label _statusLabel;

	private NakamaService _nakamaService;
	private GameState _gameState;
	private bool _isAuthReady;
	private bool _isMatchmaking;

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

		_hostButton.Pressed += OnSaveNicknamePressed;
		_joinButton.Pressed += OnMatchmakingPressed;
		_backButton.Pressed += OnBackPressed;
		_langButton.Pressed += OnLangPressed;

		_nakamaService.Authenticated += OnAuthenticated;
		_nakamaService.AuthenticationFailed += OnAuthenticationFailed;
		_nakamaService.MatchmakingStarted += OnMatchmakingStarted;
		_nakamaService.MatchmakingFailed += OnMatchmakingFailed;
		_nakamaService.MatchJoined += OnMatchJoined;
		_nakamaService.Disconnected += OnDisconnected;

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;

		_startButton.Visible = false;
		_codeInput.Editable = true;
		_codeInput.Text = "";
		_codeInput.PlaceholderText = "Guest-01";
		_codeDisplayLabel.Text = "";

		UpdateTexts();
		SetButtonsEnabled(false);
		UpdateStatus(GetText("authenticating"));
		_ = AuthenticateGuestAsync();
	}

	public override void _ExitTree()
	{
		if (_nakamaService != null)
		{
			_nakamaService.Authenticated -= OnAuthenticated;
			_nakamaService.AuthenticationFailed -= OnAuthenticationFailed;
			_nakamaService.MatchmakingStarted -= OnMatchmakingStarted;
			_nakamaService.MatchmakingFailed -= OnMatchmakingFailed;
			_nakamaService.MatchJoined -= OnMatchJoined;
			_nakamaService.Disconnected -= OnDisconnected;
		}

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
	}

	private async Task AuthenticateGuestAsync()
	{
		if (_nakamaService == null)
			return;

		await _nakamaService.AuthenticateGuestAsync();
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
		_langButton.Text = LocalizationManager.Instance.GetLanguageCode();
		_startButton.Visible = false;
	}

	private string GetText(string key)
	{
		return LocalizationManager.Instance?.GetText(key) ?? key;
	}

	private void SetButtonsEnabled(bool enabled)
	{
		_hostButton.Disabled = !enabled;
		_joinButton.Disabled = !enabled || _isMatchmaking || !_isAuthReady;
	}

	private void OnLangPressed()
	{
		LocalizationManager.Instance?.CycleLanguage();
	}

	private async void OnSaveNicknamePressed()
	{
		if (_nakamaService == null)
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
	}

	private async void OnMatchmakingPressed()
	{
		if (_nakamaService == null)
			return;

		if (!_nakamaService.IsAuthenticated)
		{
			UpdateStatus(GetText("authenticating"));
			await _nakamaService.AuthenticateGuestAsync();
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

		_gameState?.ClearOnlineSession();
		GetTree().ChangeSceneToFile("res://Scenes/GameModeMenu.tscn");
	}

	private void OnAuthenticated(string userId, string displayName)
	{
		_isAuthReady = true;
		string shortId = userId.Length > 8 ? userId.Substring(0, 8) : userId;
		_codeDisplayLabel.Text = $"{displayName} • {shortId}";
		UpdatePlayerList();
		SetButtonsEnabled(true);
		UpdateStatus($"{GetText("guest_connected")} : {displayName}");
	}

	private void OnAuthenticationFailed(string reason)
	{
		_isAuthReady = false;
		_isMatchmaking = false;
		SetButtonsEnabled(true);

		string hint = "";
		string lower = reason?.ToLowerInvariant() ?? "";
		if (lower.Contains("connection") || lower.Contains("refused") || lower.Contains("timeout") || lower.Contains("host"))
			hint = " | Lance Nakama local (Docker) puis reessaie.";

		UpdateStatus($"{GetText("connection_failed")} : {reason}{hint}");
	}

	private void OnMatchmakingStarted(string ticket)
	{
		_isMatchmaking = true;
		UpdateStatus($"{GetText("matchmaking_started")} #{ticket}");
	}

	private void OnMatchmakingFailed(string reason)
	{
		_isMatchmaking = false;
		SetButtonsEnabled(true);
		UpdateStatus($"{GetText("connection_failed")} : {reason}");
	}

	private void OnMatchJoined(string matchId, int localTeamId, int seed)
	{
		_isMatchmaking = false;
		SetButtonsEnabled(true);
		UpdateStatus(GetText("match_found"));
		_gameState?.StartOnlineGameFromMatch(matchId, localTeamId, seed, _nakamaService.UserId, _nakamaService.DisplayName);
	}

	private void OnDisconnected()
	{
		_isAuthReady = false;
		_isMatchmaking = false;
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
