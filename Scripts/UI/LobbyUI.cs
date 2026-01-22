using Godot;
using System;

public partial class LobbyUI : Control
{
	// Références aux éléments UI
	private LineEdit _ipInput;
	private LineEdit _portInput;
	private Button _hostButton;
	private Button _joinButton;
	private Button _startButton;
	private Button _backButton;
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
		_ipInput = GetNode<LineEdit>("VBoxContainer/ConnectionPanel/VBoxContainer/IPContainer/IPInput");
		_portInput = GetNode<LineEdit>("VBoxContainer/ConnectionPanel/VBoxContainer/PortContainer/PortInput");
		_hostButton = GetNode<Button>("VBoxContainer/ConnectionPanel/VBoxContainer/ButtonContainer/HostButton");
		_joinButton = GetNode<Button>("VBoxContainer/ConnectionPanel/VBoxContainer/ButtonContainer/JoinButton");
		_startButton = GetNode<Button>("VBoxContainer/LobbyPanel/VBoxContainer/StartButton");
		_backButton = GetNode<Button>("VBoxContainer/BackButton");
		_playerList = GetNode<ItemList>("VBoxContainer/LobbyPanel/VBoxContainer/PlayerList");
		_statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

		// Connecter les signaux des boutons
		_hostButton.Pressed += OnHostPressed;
		_joinButton.Pressed += OnJoinPressed;
		_startButton.Pressed += OnStartPressed;
		_backButton.Pressed += OnBackPressed;

		// Connecter les signaux du NetworkManager
		_networkManager.PlayerConnected += OnPlayerConnected;
		_networkManager.PlayerDisconnected += OnPlayerDisconnected;
		_networkManager.ConnectionSucceeded += OnConnectionSucceeded;
		_networkManager.ConnectionFailed += OnConnectionFailed;
		_networkManager.ServerDisconnected += OnServerDisconnected;

		// État initial
		_portInput.Text = NetworkManager.DefaultPort.ToString();
		_ipInput.Text = "127.0.0.1";
		_startButton.Visible = false;

		UpdateStatus("En attente...");
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
	}

	private void OnHostPressed()
	{
		int port = int.TryParse(_portInput.Text, out int p) ? p : NetworkManager.DefaultPort;

		var error = _networkManager.HostGame(port);
		if (error == Error.Ok)
		{
			UpdateStatus($"Serveur démarré sur le port {port}");
			_hostButton.Disabled = true;
			_joinButton.Disabled = true;
			_startButton.Visible = true;
			UpdatePlayerList();
		}
		else
		{
			UpdateStatus($"Erreur: impossible de démarrer le serveur ({error})");
		}
	}

	private void OnJoinPressed()
	{
		string ip = _ipInput.Text;
		int port = int.TryParse(_portInput.Text, out int p) ? p : NetworkManager.DefaultPort;

		if (string.IsNullOrWhiteSpace(ip))
		{
			UpdateStatus("Erreur: entrez une adresse IP");
			return;
		}

		var error = _networkManager.JoinGame(ip, port);
		if (error == Error.Ok)
		{
			UpdateStatus($"Connexion à {ip}:{port}...");
			_hostButton.Disabled = true;
			_joinButton.Disabled = true;
		}
		else
		{
			UpdateStatus($"Erreur: impossible de se connecter ({error})");
		}
	}

	private void OnStartPressed()
	{
		if (!_networkManager.IsServer)
		{
			UpdateStatus("Seul l'hôte peut lancer la partie");
			return;
		}

		UpdateStatus("Lancement de la partie...");
		_gameState.StartGame();
	}

	private void OnBackPressed()
	{
		_networkManager.Disconnect();
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	// --- Callbacks réseau ---

	private void OnPlayerConnected(long id)
	{
		UpdatePlayerList();
		UpdateStatus($"Joueur {id} connecté");

		// Si on est le serveur, envoyer la liste des joueurs
		if (_networkManager.IsServer)
		{
			_networkManager.BroadcastPlayerList();
		}
	}

	private void OnPlayerDisconnected(long id)
	{
		UpdatePlayerList();
		UpdateStatus($"Joueur {id} déconnecté");
	}

	private void OnConnectionSucceeded()
	{
		UpdateStatus("Connecté au serveur!");
		UpdatePlayerList();
	}

	private void OnConnectionFailed()
	{
		UpdateStatus("Échec de la connexion");
		_hostButton.Disabled = false;
		_joinButton.Disabled = false;
	}

	private void OnServerDisconnected()
	{
		UpdateStatus("Déconnecté du serveur");
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
			string suffix = player.Key == 1 ? " (Hôte)" : "";
			_playerList.AddItem($"{player.Value}{suffix}");
		}
	}

	private void UpdateStatus(string message)
	{
		_statusLabel.Text = message;
		GD.Print($"[Lobby] {message}");
	}
}
