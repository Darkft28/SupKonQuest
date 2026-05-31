using Godot;
using System;

public partial class GameState : Node
{
	public static GameState Instance { get; private set; }

	public enum PlayMode { Offline, Online }
	public enum MapSizePreset { Small, Medium, Large }
	public enum MapType { Irridium, Alabasta, Torskey }

	public bool IsLeavingGame { get; private set; }

	public PlayMode CurrentPlayMode { get; private set; } = PlayMode.Offline;

	// Map seed for identical generation across all peers (network determinism)
	public int MapSeed { get; private set; }

	// Local team: Server=1, Client=2
	public int LocalTeamId { get; set; } = 1;

	// Test mode: time x3 via Engine.TimeScale
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

	public static bool IsOnlineMultiplayer =>
		Instance?.IsOnline == true && NakamaService.Instance?.IsSocketConnected == true;

	/// <summary>Number of human players in the match (1 solo, 2-8 online).</summary>
	public int ActivePlayerCount { get; private set; } = 1;

	/// <summary>Message shown once on Auth screen after redirect from Lobby.</summary>
	public string PendingAuthMessage { get; private set; } = "";

	[Signal] public delegate void GameStartingEventHandler(int seed);
	[Signal] public delegate void PlayerListUpdatedEventHandler();

	private NakamaService _nakamaService;

	public override void _Ready()
	{
		Instance = this;
		_nakamaService = GetNodeOrNull<NakamaService>("/root/NakamaService");
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
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
		IsLeavingGame = false;
		ClearOnlineSessionFields();
		SelectedMapType = mapType;
		FastMode = fastMode;
		MapSeed = 0;
	}

	/// <summary>Solo vs AI mode: do not call ResetOnlineMatchFlags (reserved for multiplayer).</summary>
	public void StartSoloGame(MapType mapType, bool fastMode, AIController.Difficulty aiLevel)
	{
		ClearOnlineSessionFields();
		ActivePlayerCount = 1;
		IsAIMode = true;
		IsFreeForAll = true;
		AILevel = aiLevel;
		SelectedMapType = mapType;
		FastMode = fastMode;
		GenerateSeed();
		LoadGameScene();
	}

	public void ConfigureOnlineLobby(string displayName = "")
	{
		CurrentPlayMode = PlayMode.Online;
		LocalTeamId = 1;
		ActivePlayerCount = 2;
		ResetOnlineMatchFlags();
		FastMode = false;
		MatchId = "";
		MatchmakerTicket = "";
		MapSeed = 0;
		PlayerDisplayName = displayName;
	}

	public static MapType MapTypeFromIndex(int index) => index switch
	{
		1 => MapType.Alabasta,
		2 => MapType.Torskey,
		_ => MapType.Irridium,
	};

	public static string GetMapTypeDisplayName(MapType mapType) => mapType switch
	{
		MapType.Alabasta => "Alabasta",
		MapType.Torskey => "Torskey",
		_ => "Irridium",
	};

	public void ConfigureOnlineMatch(string matchId, int localTeamId, int seed, int activePlayerCount, string nakamaUserId, string displayName, int mapTypeIndex)
	{
		IsLeavingGame = false;
		CurrentPlayMode = PlayMode.Online;
		LocalTeamId = localTeamId;
		ActivePlayerCount = Math.Max(2, Math.Min(8, activePlayerCount));
		ResetOnlineMatchFlags();
		MatchId = matchId;
		MapSeed = NormalizeMapSeed(seed, matchId);
		SelectedMapType = MapTypeFromIndex(mapTypeIndex);
		NakamaUserId = nakamaUserId;
		PlayerDisplayName = displayName;
	}

	public int GetEffectiveMapSeed()
	{
		return NormalizeMapSeed(MapSeed, MatchId);
	}

	public static int DeriveSeedFromMatchId(string matchId)
	{
		if (string.IsNullOrWhiteSpace(matchId))
			return 0;

		unchecked
		{
			int hash = 17;
			foreach (char c in matchId)
				hash = hash * 31 + c;
			return hash & int.MaxValue;
		}
	}

	private static int NormalizeMapSeed(int seed, string matchId)
	{
		if (seed > 0)
			return seed;

		int derived = DeriveSeedFromMatchId(matchId);
		return derived > 0 ? derived : (int)GD.Randi();
	}

	private void ResetOnlineMatchFlags()
	{
		IsAIMode = false;
		IsFreeForAll = false;
	}

	public void SetMatchmakerTicket(string ticket)
	{
		MatchmakerTicket = ticket;
	}

	public void SetPendingAuthMessage(string message)
	{
		PendingAuthMessage = message ?? "";
	}

	public string TakePendingAuthMessage()
	{
		string message = PendingAuthMessage;
		PendingAuthMessage = "";
		return message;
	}

	public void ClearOnlineSession()
	{
		ClearOnlineSessionFields();
		ActivePlayerCount = 1;
		ResetOnlineMatchFlags();
	}

	private void ClearOnlineSessionFields()
	{
		CurrentPlayMode = PlayMode.Offline;
		LocalTeamId = 1;
		MatchId = "";
		MatchmakerTicket = "";
		NakamaUserId = "";
		PlayerDisplayName = "";
	}

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

	public void StartOnlineGameFromMatch(string matchId, int localTeamId, int seed, int activePlayerCount, string nakamaUserId, string displayName, int mapTypeIndex)
	{
		ConfigureOnlineMatch(matchId, localTeamId, seed, activePlayerCount, nakamaUserId, displayName, mapTypeIndex);
		EmitSignal(SignalName.GameStarting, seed);
		CallDeferred(nameof(LoadGameScene));
	}

	private void LoadGameScene()
	{
		IsLeavingGame = false;
		AudioSettings.Instance?.StopMenuMusic();
		GetTree()?.ChangeSceneToFile("res://Scenes/Game.tscn");
	}

	public void ReturnToLobby()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
	}

	public void ReturnToMainMenu()
	{
		IsLeavingGame = true;

		var tree = GetTree();
		if (tree != null)
		{
			tree.Paused = false;
			foreach (var node in tree.GetNodesInGroup("victory_overlay"))
			{
				if (node is Node n && GodotObject.IsInstanceValid(n))
					n.QueueFree();
			}
		}

		_nakamaService?.Disconnect();
		ClearOnlineSession();
		MapSeed = 0;
		FastMode = false;
		SelectedMapType = MapType.Irridium;
		Engine.TimeScale = 1.0;
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}
}
