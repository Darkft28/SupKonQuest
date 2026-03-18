using Godot;

public partial class GameState : Node
{
	public enum MapSizePreset { Small, Medium, Large }
	public enum MapType { Irridium, Alabasta }

	// Seed de la map pour génération identique sur tous les peers (déterminisme réseau)
	public int MapSeed { get; private set; }

	// Equipe locale : Server=1, Client=2
	public int LocalTeamId { get; set; } = 1;

	// Mode test : temps x3 via Engine.TimeScale
	public bool FastMode { get; set; } = false;

	public MapSizePreset MapSize { get; set; } = MapSizePreset.Medium;
	public int MaxCamps { get; set; } = 6;
	public bool IsFreeForAll { get; set; } = false;
	public MapType SelectedMapType { get; set; } = MapType.Irridium;

	[Signal] public delegate void GameStartingEventHandler(int seed);
	[Signal] public delegate void PlayerListUpdatedEventHandler();

	private NetworkManager _networkManager;

	public override void _Ready()
	{
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");
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

	/// <summary>
	/// Appelé par l'hôte pour lancer la partie.
	/// Envoie la seed à tous les clients puis charge la scène de jeu.
	/// </summary>
	public void StartGame()
	{
		if (!_networkManager.IsServer)
		{
			GD.PrintErr("Seul le serveur peut lancer la partie!");
			return;
		}

		LocalTeamId = 1;
		GenerateSeed();
		Rpc(nameof(RpcReceiveSeedAndStart), MapSeed, (int)SelectedMapType);
		LoadGameScene();
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
		GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
	}

	public void ReturnToLobby()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
	}

	public void ReturnToMainMenu()
	{
		_networkManager?.Disconnect();
		LocalTeamId = 1;
		MapSeed = 0;
		IsFreeForAll = false;
		FastMode = false;
		Engine.TimeScale = 1.0;
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}
}
