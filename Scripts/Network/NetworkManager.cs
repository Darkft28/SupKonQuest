using Godot;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
	public const int DefaultPort = 7777;
	public const int DiscoveryPort = 7778;
	public const int MaxPlayers = 2;

	private ENetMultiplayerPeer _peer;

	[Signal] public delegate void PlayerConnectedEventHandler(long id);
	[Signal] public delegate void PlayerDisconnectedEventHandler(long id);
	[Signal] public delegate void ConnectionFailedEventHandler();
	[Signal] public delegate void ConnectionSucceededEventHandler();
	[Signal] public delegate void ServerDisconnectedEventHandler();

	public Dictionary<long, string> Players { get; private set; } = new();
	public bool IsServer => Multiplayer.IsServer();
	public new bool IsConnected => _peer != null && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
	public string RoomCode { get; private set; } = "";

	private PacketPeerUdp _discoveryPeer;
	private bool _isSearching = false;
	private string _searchCode = "";
	private float _searchTimer = 0f;
	private float _broadcastTimer = 0f;
	private const float SearchTimeout = 5f;
	private const float BroadcastInterval = 0.5f;
	private const string DiscoveryPrefix = "SUPKONQUEST_DISCOVER:";
	private const string FoundPrefix = "SUPKONQUEST_FOUND:";

	public override void _Ready()
	{
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	public override void _ExitTree()
	{
		Multiplayer.PeerConnected -= OnPeerConnected;
		Multiplayer.PeerDisconnected -= OnPeerDisconnected;
		Multiplayer.ConnectedToServer -= OnConnectedToServer;
		Multiplayer.ConnectionFailed -= OnConnectionFailed;
		Multiplayer.ServerDisconnected -= OnServerDisconnected;
	}

	public override void _Process(double delta)
	{
		ProcessDiscovery(delta);
	}

	private string GenerateRoomCode()
	{
		// Pas de I, O, 0, 1 pour eviter les confusions
		const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		string code = "";
		for (int i = 0; i < 6; i++)
		{
			code += chars[(int)(rng.Randi() % chars.Length)];
		}
		return code;
	}

	public Error HostGame(int port = DefaultPort)
	{
		RoomCode = GenerateRoomCode();

		_peer = new ENetMultiplayerPeer();
		var error = _peer.CreateServer(port, MaxPlayers);

		if (error != Error.Ok)
		{
			GD.PrintErr($"Erreur lors de la création du serveur: {error}");
			_peer = null;
			RoomCode = "";
			return error;
		}

		Multiplayer.MultiplayerPeer = _peer;
		Players[1] = "Hôte";
		StartDiscoveryListener();

		GD.Print($"Serveur demarre sur le port {port} - Code salon: {RoomCode}");
		return Error.Ok;
	}

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
	/// Recherche un salon par code sur le reseau local via UDP broadcast
	/// </summary>
	public void JoinWithCode(string code)
	{
		_searchCode = code.ToUpper().Trim();
		_isSearching = true;
		_searchTimer = 0f;
		_broadcastTimer = 0f;

		_discoveryPeer = new PacketPeerUdp();
		_discoveryPeer.Bind(0);
		_discoveryPeer.SetBroadcastEnabled(true);

		SendDiscoveryBroadcast();
		GD.Print($"[DISCOVERY] Recherche du salon {_searchCode} sur le reseau local...");
	}

	public void Disconnect()
	{
		StopDiscovery();

		if (_peer != null)
		{
			_peer.Close();
			_peer = null;
		}

		Multiplayer.MultiplayerPeer = null;
		Players.Clear();
		RoomCode = "";
		_isSearching = false;

		GD.Print("Déconnecté du réseau");
	}

	private void StartDiscoveryListener()
	{
		_discoveryPeer = new PacketPeerUdp();
		var error = _discoveryPeer.Bind(DiscoveryPort);
		if (error != Error.Ok)
		{
			GD.PrintErr($"Erreur bind UDP discovery port {DiscoveryPort}: {error}");
			_discoveryPeer = null;
		}
		else
		{
			GD.Print($"[DISCOVERY] Ecoute UDP sur le port {DiscoveryPort}");
		}
	}

	private void SendDiscoveryBroadcast()
	{
		if (_discoveryPeer == null) return;

		string message = $"{DiscoveryPrefix}{_searchCode}";
		byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
		_discoveryPeer.SetDestAddress("255.255.255.255", DiscoveryPort);
		_discoveryPeer.PutPacket(data);
	}

	private void ProcessDiscovery(double delta)
	{
		if (_discoveryPeer == null) return;

		// Host : repondre aux requetes de decouverte
		if (_peer != null && IsServer && !string.IsNullOrEmpty(RoomCode))
		{
			while (_discoveryPeer.GetAvailablePacketCount() > 0)
			{
				byte[] packet = _discoveryPeer.GetPacket();
				string message = System.Text.Encoding.UTF8.GetString(packet);

				string expectedRequest = $"{DiscoveryPrefix}{RoomCode}";
				if (message == expectedRequest)
				{
					string senderIp = (string)_discoveryPeer.Call("get_packet_ip");
					int senderPort = (int)_discoveryPeer.Call("get_packet_port");

					string response = $"{FoundPrefix}{RoomCode}:{DefaultPort}";
					byte[] responseData = System.Text.Encoding.UTF8.GetBytes(response);

					_discoveryPeer.SetDestAddress(senderIp, senderPort);
					_discoveryPeer.PutPacket(responseData);

					GD.Print($"[DISCOVERY] Repondu a {senderIp}:{senderPort} pour le salon {RoomCode}");
				}
			}
		}

		if (_isSearching)
		{
			_searchTimer += (float)delta;
			_broadcastTimer += (float)delta;

			if (_broadcastTimer >= BroadcastInterval)
			{
				_broadcastTimer = 0f;
				SendDiscoveryBroadcast();
			}

			while (_discoveryPeer.GetAvailablePacketCount() > 0)
			{
				byte[] packet = _discoveryPeer.GetPacket();
				string message = System.Text.Encoding.UTF8.GetString(packet);
				string hostIp = (string)_discoveryPeer.Call("get_packet_ip");

				string expectedPrefix = $"{FoundPrefix}{_searchCode}:";
				if (message.StartsWith(expectedPrefix))
				{
					string portStr = message.Substring(expectedPrefix.Length);
					int port = int.TryParse(portStr, out int p) ? p : DefaultPort;

					_isSearching = false;
					RoomCode = _searchCode;

					_discoveryPeer.Close();
					_discoveryPeer = null;

					GD.Print($"[DISCOVERY] Salon {_searchCode} trouve a {hostIp}:{port}!");
					JoinGame(hostIp, port);
					return;
				}
			}

			if (_searchTimer >= SearchTimeout)
			{
				_isSearching = false;
				StopDiscovery();
				EmitSignal(SignalName.ConnectionFailed);
				GD.Print("[DISCOVERY] Timeout: salon non trouve sur le reseau local");
			}
		}
	}

	private void StopDiscovery()
	{
		if (_discoveryPeer != null)
		{
			_discoveryPeer.Close();
			_discoveryPeer = null;
		}
	}

	private void OnPeerConnected(long id)
	{
		GD.Print($"[NET] Joueur {id} connecté");
		Players[id] = $"Joueur {id}";
		EmitSignal(SignalName.PlayerConnected, id);
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"[NET] Joueur {id} déconnecté");
		Players.Remove(id);
		EmitSignal(SignalName.PlayerDisconnected, id);

		// Si on est en jeu, planifier le retour au menu après 5s
		if (GetTree().CurrentScene?.Name == "Game")
		{
			GetTree().CreateTimer(5.0).Timeout += () =>
			{
				if (IsInstanceValid(this))
					GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
			};
		}
	}

	private void OnConnectedToServer()
	{
		GD.Print("[NET] Connecté au serveur!");
		Players[Multiplayer.GetUniqueId()] = $"Joueur {Multiplayer.GetUniqueId()}";
		EmitSignal(SignalName.ConnectionSucceeded);
	}

	private void OnConnectionFailed()
	{
		GD.PrintErr("[NET] Échec de la connexion au serveur");
		_peer = null;
		Multiplayer.MultiplayerPeer = null;
		EmitSignal(SignalName.ConnectionFailed);
	}

	private void OnServerDisconnected()
	{
		GD.Print("[NET] Déconnecté du serveur");
		_peer = null;
		Multiplayer.MultiplayerPeer = null;
		Players.Clear();
		EmitSignal(SignalName.ServerDisconnected);

		// Si on est en jeu, planifier le retour au menu après 5s
		if (GetTree().CurrentScene?.Name == "Game")
		{
			// Réutiliser PlayerDisconnected avec id=-1 pour signaler une déco serveur
			EmitSignal(SignalName.PlayerDisconnected, (long)-1);
			GetTree().CreateTimer(5.0).Timeout += () =>
			{
				if (IsInstanceValid(this))
					GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
			};
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcSyncPlayerList(string[] playerIds, string[] playerNames)
	{
		Players.Clear();
		for (int i = 0; i < playerIds.Length; i++)
			Players[long.Parse(playerIds[i])] = playerNames[i];

		GD.Print($"[NET] Liste des joueurs synchronisée: {Players.Count} joueurs");
	}

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
