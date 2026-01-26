using Godot;

public partial class LobbyUI : Control
{
	// Références aux éléments UI
	private Label _titleLabel;
	private Label _ipLabel;
	private Label _portLabel;
	private LineEdit _ipInput;
	private LineEdit _portInput;
	private Button _hostButton;
	private Button _joinButton;
	private Button _startButton;
	private Button _backButton;
	private Button _langButton;
	private Label _playersLabel;
	private ItemList _playerList;
	private Label _statusLabel;

	// Références aux managers
	private NetworkManager _networkManager;
	private GameState _gameState;

	public override void _Ready()
	{
		// Récupérer les managers
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");
		_gameState = GetNode<GameState>("/root/GameState");

		// Récupérer les éléments UI
		_titleLabel = GetNode<Label>("VBoxContainer/Title");
		_ipLabel = GetNode<Label>("VBoxContainer/ConnectionPanel/VBoxContainer/IPContainer/Label");
		_portLabel = GetNode<Label>("VBoxContainer/ConnectionPanel/VBoxContainer/PortContainer/Label");
		_ipInput = GetNode<LineEdit>("VBoxContainer/ConnectionPanel/VBoxContainer/IPContainer/IPInput");
		_portInput = GetNode<LineEdit>("VBoxContainer/ConnectionPanel/VBoxContainer/PortContainer/PortInput");
		_hostButton = GetNode<Button>("VBoxContainer/ConnectionPanel/VBoxContainer/ButtonContainer/HostButton");
		_joinButton = GetNode<Button>("VBoxContainer/ConnectionPanel/VBoxContainer/ButtonContainer/JoinButton");
		_startButton = GetNode<Button>("VBoxContainer/LobbyPanel/VBoxContainer/StartButton");
		_backButton = GetNode<Button>("VBoxContainer/BackButton");
		_langButton = GetNode<Button>("LangButton");
		_playersLabel = GetNode<Label>("VBoxContainer/LobbyPanel/VBoxContainer/PlayersLabel");
		_playerList = GetNode<ItemList>("VBoxContainer/LobbyPanel/VBoxContainer/PlayerList");
		_statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

		// Connecter les signaux des boutons
		_hostButton.Pressed += OnHostPressed;
		_joinButton.Pressed += OnJoinPressed;
		_startButton.Pressed += OnStartPressed;
		_backButton.Pressed += OnBackPressed;
		_langButton.Pressed += OnLangPressed;

		// Connecter les signaux du NetworkManager
		_networkManager.PlayerConnected += OnPlayerConnected;
		_networkManager.PlayerDisconnected += OnPlayerDisconnected;
		_networkManager.ConnectionSucceeded += OnConnectionSucceeded;
		_networkManager.ConnectionFailed += OnConnectionFailed;
		_networkManager.ServerDisconnected += OnServerDisconnected;

		// Connecter le signal de changement de langue
		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.LanguageChanged += UpdateTexts;
		}

		// État initial
		_portInput.Text = NetworkManager.DefaultPort.ToString();
		_ipInput.Text = "127.0.0.1";
		_startButton.Visible = false;

		UpdateTexts();
	}

	private void UpdateTexts()
	{
		if (LocalizationManager.Instance == null) return;

		_titleLabel.Text = LocalizationManager.Instance.GetText("lobby_title");
		_ipLabel.Text = LocalizationManager.Instance.GetText("ip_address");
		_portLabel.Text = LocalizationManager.Instance.GetText("port");
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
		// Déconnecter les signaux
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
		int port = int.TryParse(_portInput.Text, out int p) ? p : NetworkManager.DefaultPort;

		var error = _networkManager.HostGame(port);
		if (error == Error.Ok)
		{
			UpdateStatus($"{LocalizationManager.Instance.GetText("server_started")} {port}");
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
		string ip = _ipInput.Text;
		int port = int.TryParse(_portInput.Text, out int p) ? p : NetworkManager.DefaultPort;

		if (string.IsNullOrWhiteSpace(ip))
		{
			UpdateStatus(LocalizationManager.Instance.GetText("error_enter_ip"));
			return;
		}

		var error = _networkManager.JoinGame(ip, port);
		if (error == Error.Ok)
		{
			UpdateStatus($"{LocalizationManager.Instance.GetText("connecting_to")} {ip}:{port}...");
			_hostButton.Disabled = true;
			_joinButton.Disabled = true;
		}
		else
		{
			UpdateStatus($"{LocalizationManager.Instance.GetText("error_connect")} ({error})");
		}
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

	// --- Callbacks réseau ---

	private void OnPlayerConnected(long id)
	{
		UpdatePlayerList();
		UpdateStatus($"{LocalizationManager.Instance.GetText("player_connected")} {id}");

		// Si on est le serveur, envoyer la liste des joueurs
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
		UpdateStatus(LocalizationManager.Instance.GetText("connection_failed"));
		_hostButton.Disabled = false;
		_joinButton.Disabled = false;
	}

	private void OnServerDisconnected()
	{
		UpdateStatus(LocalizationManager.Instance.GetText("disconnected_from_server"));
		_hostButton.Disabled = false;
		_joinButton.Disabled = false;
		_startButton.Visible = false;
		_playerList.Clear();
	}

	// --- Helpers ---

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
		GD.Print($"[Lobby] {message}");
	}
}
