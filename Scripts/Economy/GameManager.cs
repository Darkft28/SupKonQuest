using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
	private static GameManager _instance;

	public static GameManager Instance
	{
		get { return _instance; }
	}

	// Or par équipe
	private Dictionary<int, int> _teamGold = new Dictionary<int, int>();

	// Or de départ et bonus de capture
	private const int StartingGold = 100;
	private const int CaptureBonus = 50;
	private const int PassiveGoldPerSecond = 5;

	private float _passiveGoldTimer = 0f;

	// Liste des camps de la scène
	private List<CampSimple> _allCamps = new List<CampSimple>();

	// Nombre de joueurs
	private const int NumberOfPlayers = 2;

	// Gestionnaire de victoire
	private VictoryManager _victoryManager;

	public override void _Ready()
	{
		_instance = this;
		_victoryManager = new VictoryManager(this);
		// L'initialisation des camps sera faite après la génération de la map
		// via OnMapGenerationComplete()
	}

	// Méthode publique appelée par MapGenerator après la génération des camps
	public void OnMapGenerationComplete()
	{
		InitializeCamps();
	}

	private void InitializeCamps()
	{
		// Reset de l'or entre les parties (GameManager est un autoload persistant)
		_teamGold.Clear();

		// Récupérer tous les camps de la scène
		_allCamps.Clear();
		var campNodes = GetTree().GetNodesInGroup("camps");

		foreach (var node in campNodes)
		{
			if (node is CampSimple camp)
			{
				_allCamps.Add(camp);
			}
		}

		GD.Print($"Nombre de camps trouves: {_allCamps.Count}");

		// Attribuer les camps aux joueurs
		AssignCampsToPlayers();
	}

	private void AssignCampsToPlayers()
	{
		if (_allCamps.Count == 0)
		{
			GD.Print("Aucun camp a attribuer");
			return;
		}

		// Utiliser une seed deterministe pour le shuffle (meme resultat sur les 2 peers)
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int seed = gameState?.MapSeed ?? (int)GD.Randi();

		List<CampSimple> shuffledCamps = new List<CampSimple>(_allCamps);
		ShuffleList(shuffledCamps, seed);

		int campsPerPlayer = shuffledCamps.Count / NumberOfPlayers;
		int campIndex = 0;

		for (int playerId = 1; playerId <= NumberOfPlayers; playerId++)
		{
			for (int i = 0; i < campsPerPlayer; i++)
			{
				if (campIndex < shuffledCamps.Count)
				{
					CampSimple camp = shuffledCamps[campIndex];
					camp.SetTeam(playerId, false);
					GD.Print($"Camp #{camp.GetCampId()} attribue au Joueur {playerId}");
					campIndex++;
				}
			}

			InitializeTeam(playerId);
		}

		while (campIndex < shuffledCamps.Count)
		{
			CampSimple camp = shuffledCamps[campIndex];
			camp.SetTeam(0, true);
			GD.Print($"Camp #{camp.GetCampId()} est Neutre");
			campIndex++;
		}

		// Mettre a jour l'autorite des defenseurs
		foreach (var camp in _allCamps)
		{
			camp.UpdateDefendersAuthority();
		}

		// En multijoueur, le serveur broadcast les assignations
		BroadcastCampAssignments();
	}

	private void BroadcastCampAssignments()
	{
		if (NetworkSync.Instance == null || !NetworkSync.Instance.IsMultiplayer()) return;
		if (!NetworkSync.Instance.IsServer()) return;

		var campIds = new List<int>();
		var teamIds = new List<int>();
		var isNeutral = new List<bool>();

		foreach (var camp in _allCamps)
		{
			campIds.Add(camp.GetCampId());
			teamIds.Add(camp.GetTeamId());
			isNeutral.Add(camp.IsNeutralCamp);
		}

		NetworkSync.Instance.SendSyncCampAssignments(campIds.ToArray(), teamIds.ToArray(), isNeutral.ToArray());
		GD.Print($"[NET] Camp assignments broadcast: {campIds.Count} camps");
	}

	private void ShuffleList(List<CampSimple> list, int seed)
	{
		var rng = new System.Random(seed);

		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.Next(0, i + 1);
			CampSimple temp = list[i];
			list[i] = list[j];
			list[j] = temp;
		}
	}

	private bool IsMultiplayerActive()
	{
		return NetworkSync.Instance != null && NetworkSync.Instance.IsMultiplayer();
	}

	public override void _Process(double delta)
	{
		// Or passif chaque seconde
		_passiveGoldTimer += (float)delta;
		if (_passiveGoldTimer >= 1.0f)
		{
			_passiveGoldTimer = 0f;

			if (IsMultiplayerActive())
			{
				// Multi : chaque peer gere uniquement l'or de sa propre equipe
				var gameState = GetNodeOrNull<GameState>("/root/GameState");
				int localTeamId = gameState?.LocalTeamId ?? 1;

				if (_teamGold.ContainsKey(localTeamId))
					_teamGold[localTeamId] += PassiveGoldPerSecond;
			}
			else
			{
				// Solo : toutes les equipes recoivent l'or passif
				foreach (var teamId in _teamGold.Keys)
				{
					_teamGold[teamId] += PassiveGoldPerSecond;
				}
			}
		}

		// Vérification périodique de victoire
		_victoryManager.Update(delta);
	}

	public List<CampSimple> GetAllCamps()
	{
		return _allCamps;
	}

	public void InitializeTeam(int teamId)
	{
		// Ne pas initialiser les camps neutres (teamId <= 0)
		if (teamId <= 0)
			return;

		if (!_teamGold.ContainsKey(teamId))
		{
			_teamGold[teamId] = StartingGold;
			GD.Print($"Equipe {teamId} initialisee avec {StartingGold} or");
		}
	}

	public int GetGold(int teamId)
	{
		return _teamGold.TryGetValue(teamId, out int gold) ? gold : 0;
	}

	public bool CanAfford(int teamId, int cost)
	{
		return GetGold(teamId) >= cost;
	}

	public bool SpendGold(int teamId, int amount)
	{
		if (!CanAfford(teamId, amount))
			return false;

		_teamGold[teamId] -= amount;
		return true;
	}

	public void AddGold(int teamId, int amount)
	{
		if (!_teamGold.ContainsKey(teamId))
		{
			_teamGold[teamId] = 0;
		}
		_teamGold[teamId] += amount;
	}

	public void GiveCaptureBonus(int teamId)
	{
		AddGold(teamId, CaptureBonus);
		GD.Print($"Equipe {teamId} recoit {CaptureBonus} or pour la capture!");
	}
}
