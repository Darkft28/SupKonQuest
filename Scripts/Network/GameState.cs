using Godot;

public partial class GameState : Node
{
	public enum PlayMode { Offline, Online }
	public enum MapSizePreset { Small, Medium, Large }
	public enum MapType { Irridium, Alabasta }

	public PlayMode CurrentPlayMode { get; private set; } = PlayMode.Offline;

	// Seed de la map pour génération identique sur tous les peers (déterminisme réseau)
	public int MapSeed { get; private set; }

	// Equipe locale : Server=1, Client=2
	public int LocalTeamId { get; set; } = 1;

	// Mode test : temps x3 via Engine.TimeScale
	public bool FastMode { get; set; } = false;

	public MapSizePreset MapSize { get; set; } = MapSizePreset.Medium;
	public int MaxCamps { get; set; } = 6;
	public bool IsFreeForAll { get; set; } = false;
	public bool IsAIMode { get; set; } = false;
	public AIController.Difficulty AILevel { get; set; } = AIController.Difficulty.Medium;
	public MapType SelectedMapType { get; set; } = MapType.Irridium;
	public string NakamaUserId { get; private set; } = "";
	public string PlayerDisplayName { get; private set; } = "";
	public string MatchId { get; private set; } = "";
	public string MatchmakerTicket { get; private set; } = "";
	public bool IsOnline => CurrentPlayMode == PlayMode.Online;

	[Signal] public delegate void GameStartingEventHandler(int seed);
	[Signal] public delegate void PlayerListUpdatedEventHandler();

	private NetworkManager _networkManager;
	private NakamaService _nakamaService;

	public override void _Ready()
	{
		_networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		_nakamaService = GetNodeOrNull<NakamaService>("/root/NakamaService");
	}

	public int GenerateSeed()
	{
		MapSeed = (int)GD.Randi();
		return MapSeed;
	}

	public void SetSeed(int seed)
	{
		MapSeed = seed;
	}

	public void ConfigureOfflineGame(MapType mapType, bool fastMode)
	{
		CurrentPlayMode = PlayMode.Offline;
		LocalTeamId = 1;
		SelectedMapType = mapType;
		FastMode = fastMode;
		MapSeed = 0;
		MatchId = "";
		MatchmakerTicket = "";
		NakamaUserId = "";
		PlayerDisplayName = "";
	}

	public void ConfigureOnlineLobby(string displayName = "")
	{
		CurrentPlayMode = PlayMode.Online;
		LocalTeamId = 1;
		SelectedMapType = MapType.Irridium;
		FastMode = false;
		IsFreeForAll = false;
		MatchId = "";
		MatchmakerTicket = "";
		MapSeed = 0;
		PlayerDisplayName = displayName;
	}

	public void ConfigureOnlineMatch(string matchId, int localTeamId, int seed, string nakamaUserId, string displayName)
	{
		CurrentPlayMode = PlayMode.Online;
		LocalTeamId = localTeamId;
		MatchId = matchId;
		MapSeed = seed;
		NakamaUserId = nakamaUserId;
		PlayerDisplayName = displayName;
	}

	public void SetMatchmakerTicket(string ticket)
	{
		MatchmakerTicket = ticket;
	}

	public void ClearOnlineSession()
	{
		CurrentPlayMode = PlayMode.Offline;
		LocalTeamId = 1;
		MatchId = "";
		MatchmakerTicket = "";
		NakamaUserId = "";
		PlayerDisplayName = "";
	}

	/// <summary>
	/// Appelé par l'hôte pour lancer la partie.
	/// Envoie la seed à tous les clients puis charge la scène de jeu.
	/// </summary>
	public void StartGame()
	{
		if (IsOnline && !string.IsNullOrWhiteSpace(MatchId))
		{
			CallDeferred(nameof(LoadGameScene));
			return;
		}

		StartOfflineGame(SelectedMapType, FastMode);
	}

	public void StartOfflineGame(MapType mapType, bool fastMode)
	{
		ConfigureOfflineGame(mapType, fastMode);
		GenerateSeed();
		LoadGameScene();
	}

	public void StartOnlineGameFromMatch(string matchId, int localTeamId, int seed, string nakamaUserId, string displayName)
	{
		ConfigureOnlineMatch(matchId, localTeamId, seed, nakamaUserId, displayName);
		EmitSignal(SignalName.GameStarting, seed);
		CallDeferred(nameof(LoadGameScene));
	}

	/// <summary>
	/// RPC reçu par les clients avec la seed et l'ordre de démarrer.
	/// Authority mode : seul le serveur peut appeler ce RPC.
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveSeedAndStart(int seed, int mapTypeInt)
	{
		LocalTeamId = 2;
		GD.Print($"Seed reçue du serveur: {seed}");
		SetSeed(seed);
		SelectedMapType = (MapType)mapTypeInt;
		EmitSignal(SignalName.GameStarting, seed);
		LoadGameScene();
	}

	private void LoadGameScene()
	{
		GetTree()?.ChangeSceneToFile("res://Scenes/Game.tscn");
	}

	public void ReturnToLobby()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
	}

	public void ReturnToMainMenu()
	{
		_nakamaService?.Disconnect();
		_networkManager?.Disconnect();
		ClearOnlineSession();
		MapSeed = 0;
		IsFreeForAll = false;
		FastMode = false;
		SelectedMapType = MapType.Irridium;
		Engine.TimeScale = 1.0;
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}
}
