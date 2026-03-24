using Godot;

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

		private NetworkManager _networkManager;
	private GameState _gameState;

	public override void _Ready()
	{
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");
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

		_hostButton.Pressed += OnHostPressed;
		_joinButton.Pressed += OnJoinPressed;
		_startButton.Pressed += OnStartPressed;
		_backButton.Pressed += OnBackPressed;
		_langButton.Pressed += OnLangPressed;

		_networkManager.PlayerConnected += OnPlayerConnected;
		_networkManager.PlayerDisconnected += OnPlayerDisconnected;
		_networkManager.ConnectionSucceeded += OnConnectionSucceeded;
		_networkManager.ConnectionFailed += OnConnectionFailed;
		_networkManager.ServerDisconnected += OnServerDisconnected;

		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;
		}

		_startButton.Visible = false;
		_codeDisplayLabel.Text = "";

		UpdateTexts();
	}

	private void UpdateTexts()
	{
		if (LocalizationManager.Instance == null) return;

		_titleLabel.Text = LocalizationManager.Instance.GetText("lobby_title");
		_codeLabel.Text = LocalizationManager.Instance.GetText("room_code");
		_hostButton.Text = LocalizationManager.Instance.GetText("host");
		_joinButton.Text = LocalizationManager.Instance.GetText("join");
		_playersLabel.Text = LocalizationManager.Instance.GetText("players_connected");
		_startButton.Text = LocalizationManager.Instance.GetText("start_game");
		_backButton.Text = LocalizationManager.Instance.GetText("back");
		_langButton.Text = LocalizationManager.Instance.GetLanguageCode();
		_statusLabel.Text = LocalizationManager.Instance.GetText("waiting");
	}

	private void OnLangPressed()
	{
		LocalizationManager.Instance?.CycleLanguage();
	}

	public override void _ExitTree()
	{
		if (_networkManager != null)
		{
			_networkManager.PlayerConnected -= OnPlayerConnected;
			_networkManager.PlayerDisconnected -= OnPlayerDisconnected;
			_networkManager.ConnectionSucceeded -= OnConnectionSucceeded;
			_networkManager.ConnectionFailed -= OnConnectionFailed;
			_networkManager.ServerDisconnected -= OnServerDisconnected;
		}

		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.LanguageChanged -= UpdateTexts;
		}
	}

	private void OnHostPressed()
	{
		var error = _networkManager.HostGame();
		if (error == Error.Ok)
		{
				_codeDisplayLabel.Text = _networkManager.RoomCode;
			_codeInput.Editable = false;
			_codeInput.Text = _networkManager.RoomCode;

			UpdateStatus($"{LocalizationManager.Instance.GetText("room_created")} {_networkManager.RoomCode}");
			_hostButton.Disabled = true;
			_joinButton.Disabled = true;
			_startButton.Visible = true;
			UpdatePlayerList();
		}
		else
		{
			UpdateStatus($"{LocalizationManager.Instance.GetText("error_start_server")} ({error})");
		}
	}

	private void OnJoinPressed()
	{
		string code = _codeInput.Text.ToUpper().Trim();

		if (string.IsNullOrWhiteSpace(code) || code.Length < 6)
		{
			UpdateStatus(LocalizationManager.Instance.GetText("error_enter_code"));
			return;
		}

		UpdateStatus($"{LocalizationManager.Instance.GetText("searching_room")} {code}...");
		_hostButton.Disabled = true;
		_joinButton.Disabled = true;

		_networkManager.JoinWithCode(code);
	}

	private void OnStartPressed()
	{
		if (!_networkManager.IsServer)
		{
			UpdateStatus(LocalizationManager.Instance.GetText("only_host_start"));
			return;
		}

		UpdateStatus(LocalizationManager.Instance.GetText("starting_game"));
		_gameState.StartGame();
	}

	private void OnBackPressed()
	{
		_networkManager.Disconnect();
		GetTree().ChangeSceneToFile("res://Scenes/GameModeMenu.tscn");
	}

		private void OnPlayerConnected(long id)
	{
		UpdatePlayerList();
		UpdateStatus($"{LocalizationManager.Instance.GetText("player_connected")} {id}");

		if (_networkManager.IsServer)
		{
			_networkManager.BroadcastPlayerList();
		}
	}

	private void OnPlayerDisconnected(long id)
	{
		UpdatePlayerList();
		UpdateStatus($"{LocalizationManager.Instance.GetText("player_disconnected")} {id}");
	}

	private void OnConnectionSucceeded()
	{
		UpdateStatus(LocalizationManager.Instance.GetText("connected_to_server"));
		UpdatePlayerList();
	}

	private void OnConnectionFailed()
	{
		UpdateStatus(LocalizationManager.Instance.GetText("room_not_found"));
		_hostButton.Disabled = false;
		_joinButton.Disabled = false;
	}

	private void OnServerDisconnected()
	{
		UpdateStatus(LocalizationManager.Instance.GetText("disconnected_from_server"));
		_hostButton.Disabled = false;
		_joinButton.Disabled = false;
		_startButton.Visible = false;
		_codeDisplayLabel.Text = "";
		_codeInput.Editable = true;
		_codeInput.Text = "";
		_playerList.Clear();
	}

	private void UpdatePlayerList()
	{
		_playerList.Clear();
		foreach (var player in _networkManager.Players)
		{
			string suffix = player.Key == 1 ? LocalizationManager.Instance.GetText("host_suffix") : "";
			_playerList.AddItem($"{player.Value}{suffix}");
		}
	}

	private void UpdateStatus(string message)
	{
		_statusLabel.Text = message;
	}
}
