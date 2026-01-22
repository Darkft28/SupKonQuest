using Godot;
using System;
using System.Collections.Generic;

public partial class GameState : Node
{
	// Seed de la map pour génération identique
	public int MapSeed { get; private set; }

	// Signaux
	[Signal] public delegate void GameStartingEventHandler(int seed);
	[Signal] public delegate void PlayerListUpdatedEventHandler();

	// Référence au NetworkManager
	private NetworkManager _networkManager;

	public override void _Ready()
	{
		_networkManager = GetNode<NetworkManager>("/root/NetworkManager");
	}

	/// <summary>
	/// Génère une nouvelle seed aléatoire (côté serveur uniquement)
	/// </summary>
	public int GenerateSeed()
	{
		MapSeed = (int)GD.Randi();
		GD.Print($"Seed générée: {MapSeed}");
		return MapSeed;
	}

	/// <summary>
	/// Définit la seed (utilisé lors de la synchronisation)
	/// </summary>
	public void SetSeed(int seed)
	{
		MapSeed = seed;
		GD.Print($"Seed définie: {MapSeed}");
	}

	/// <summary>
	/// Appelé par l'hôte pour lancer la partie
	/// Envoie la seed à tous les clients puis charge la scène de jeu
	/// </summary>
	public void StartGame()
	{
		if (!_networkManager.IsServer)
		{
			GD.PrintErr("Seul le serveur peut lancer la partie!");
			return;
		}

		// Générer la seed
		GenerateSeed();

		// Envoyer la seed à tous les clients
		Rpc(nameof(RpcReceiveSeedAndStart), MapSeed);

		// Charger la scène localement aussi
		LoadGameScene();
	}

	/// <summary>
	/// RPC reçu par les clients avec la seed et l'ordre de démarrer
	/// </summary>
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveSeedAndStart(int seed)
	{
		GD.Print($"Seed reçue du serveur: {seed}");
		SetSeed(seed);
		EmitSignal(SignalName.GameStarting, seed);
		LoadGameScene();
	}

	/// <summary>
	/// Charge la scène de jeu
	/// </summary>
	private void LoadGameScene()
	{
		GD.Print("Chargement de la scène de jeu...");
		GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
	}

	/// <summary>
	/// Retourne au lobby
	/// </summary>
	public void ReturnToLobby()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
	}

	/// <summary>
	/// Retourne au menu principal et se déconnecte
	/// </summary>
	public void ReturnToMainMenu()
	{
		_networkManager?.Disconnect();
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}
}
