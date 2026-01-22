using Godot;
using System;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
	public const int DefaultPort = 7777;
	public const int MaxPlayers = 4;

	private ENetMultiplayerPeer _peer;

	// Signaux pour les événements réseau
	[Signal] public delegate void PlayerConnectedEventHandler(long id);
	[Signal] public delegate void PlayerDisconnectedEventHandler(long id);
	[Signal] public delegate void ConnectionFailedEventHandler();
	[Signal] public delegate void ConnectionSucceededEventHandler();
	[Signal] public delegate void ServerDisconnectedEventHandler();

	// Liste des joueurs connectés (ID -> Nom)
	public Dictionary<long, string> Players { get; private set; } = new();

	// Est-ce qu'on est le serveur ?
	public bool IsServer => Multiplayer.IsServer();

	// Est-ce qu'on est connecté ?
	public bool IsConnected => _peer != null && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

	public override void _Ready()
	{
		// Connexion aux signaux du Multiplayer
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	public override void _ExitTree()
	{
		// Déconnexion des signaux
		Multiplayer.PeerConnected -= OnPeerConnected;
		Multiplayer.PeerDisconnected -= OnPeerDisconnected;
		Multiplayer.ConnectedToServer -= OnConnectedToServer;
		Multiplayer.ConnectionFailed -= OnConnectionFailed;
		Multiplayer.ServerDisconnected -= OnServerDisconnected;
	}

	/// <summary>
	/// Crée un serveur et héberge une partie
	/// </summary>
	public Error HostGame(int port = DefaultPort)
	{
		_peer = new ENetMultiplayerPeer();
		var error = _peer.CreateServer(port, MaxPlayers);

		if (error != Error.Ok)
		{
			GD.PrintErr($"Erreur lors de la création du serveur: {error}");
			_peer = null;
			return error;
		}

		Multiplayer.MultiplayerPeer = _peer;

		// L'hôte s'ajoute lui-même à la liste des joueurs
		Players[1] = "Hôte";

		GD.Print($"Serveur démarré sur le port {port}");
		return Error.Ok;
	}

	/// <summary>
	/// Rejoint une partie via IP
	/// </summary>
	public Error JoinGame(string ip, int port = DefaultPort)
	{
		_peer = new ENetMultiplayerPeer();
		var error = _peer.CreateClient(ip, port);

		if (error != Error.Ok)
		{
			GD.PrintErr($"Erreur lors de la connexion: {error}");
			_peer = null;
			return error;
		}

		Multiplayer.MultiplayerPeer = _peer;

		GD.Print($"Tentative de connexion à {ip}:{port}...");
		return Error.Ok;
	}

	/// <summary>
	/// Se déconnecter du réseau
	/// </summary>
	public void Disconnect()
	{
		if (_peer != null)
		{
			_peer.Close();
			_peer = null;
		}

		Multiplayer.MultiplayerPeer = null;
		Players.Clear();

		GD.Print("Déconnecté du réseau");
	}

	// --- Callbacks des événements réseau ---

	private void OnPeerConnected(long id)
	{
		GD.Print($"Joueur {id} connecté");
		Players[id] = $"Joueur {id}";
		EmitSignal(SignalName.PlayerConnected, id);
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"Joueur {id} déconnecté");
		Players.Remove(id);
		EmitSignal(SignalName.PlayerDisconnected, id);
	}

	private void OnConnectedToServer()
	{
		GD.Print("Connecté au serveur!");
		// Ajouter notre propre ID
		Players[Multiplayer.GetUniqueId()] = $"Joueur {Multiplayer.GetUniqueId()}";
		EmitSignal(SignalName.ConnectionSucceeded);
	}

	private void OnConnectionFailed()
	{
		GD.PrintErr("Échec de la connexion au serveur");
		_peer = null;
		Multiplayer.MultiplayerPeer = null;
		EmitSignal(SignalName.ConnectionFailed);
	}

	private void OnServerDisconnected()
	{
		GD.Print("Déconnecté du serveur");
		_peer = null;
		Multiplayer.MultiplayerPeer = null;
		Players.Clear();
		EmitSignal(SignalName.ServerDisconnected);
	}

	// --- RPCs ---

	/// <summary>
	/// Appelé par le serveur pour envoyer la liste des joueurs à un nouveau client
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcSyncPlayerList(string[] playerIds, string[] playerNames)
	{
		Players.Clear();
		for (int i = 0; i < playerIds.Length; i++)
		{
			Players[long.Parse(playerIds[i])] = playerNames[i];
		}
		GD.Print($"Liste des joueurs synchronisée: {Players.Count} joueurs");
	}

	/// <summary>
	/// Envoie la liste des joueurs à tous les clients
	/// </summary>
	public void BroadcastPlayerList()
	{
		if (!IsServer) return;

		var ids = new List<string>();
		var names = new List<string>();

		foreach (var kvp in Players)
		{
			ids.Add(kvp.Key.ToString());
			names.Add(kvp.Value);
		}

		Rpc(nameof(RpcSyncPlayerList), ids.ToArray(), names.ToArray());
	}
}
