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
	private const int PassiveGoldPerSecond = 500;
	private const int RegionBonusGold = 30; // Bonus si on controle toute une region

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

		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int seed = gameState?.MapSeed ?? (int)GD.Randi();

		List<CampSimple> shuffledCamps = new List<CampSimple>(_allCamps);
		ShuffleList(shuffledCamps, seed);

		if (gameState?.IsFreeForAll == true)
		{
			// Joueur = camp 0 (team 1), chaque autre camp = sa propre team bot
			shuffledCamps[0].SetTeam(1, false);
			InitializeTeam(1);
			GD.Print($"Camp #{shuffledCamps[0].GetCampId()} attribue au Joueur (Team 1)");

			for (int i = 1; i < shuffledCamps.Count; i++)
			{
				int teamId = i + 1;
				shuffledCamps[i].SetTeam(teamId, false);
				InitializeTeam(teamId);
				GD.Print($"Camp #{shuffledCamps[i].GetCampId()} attribue au Bot Team {teamId}");
			}
		}
		else
		{
			// Mode original : 2 teams + camps neutres
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
		}

		foreach (var camp in _allCamps)
			camp.UpdateDefendersAuthority();

		BroadcastCampAssignments();
	}

	public List<int> GetBotTeamIds()
	{
		var botTeams = new List<int>();
		foreach (var camp in _allCamps)
		{
			int teamId = camp.GetTeamId();
			if (teamId > 1 && !botTeams.Contains(teamId))
				botTeams.Add(teamId);
		}
		return botTeams;
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
		return NetworkSync.Instance != null
			&& GodotObject.IsInstanceValid(NetworkSync.Instance)
			&& NetworkSync.Instance.IsMultiplayer();
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

				CheckRegionBonuses(localTeamId);
			}
			else
			{
				// Solo / IA : toutes les equipes recoivent l'or passif
				foreach (var teamId in new List<int>(_teamGold.Keys))
				{
					_teamGold[teamId] += PassiveGoldPerSecond;
				}
				CheckRegionBonuses(-1); // -1 = toutes les equipes
			}
		}

		// Vérification périodique de victoire
		_victoryManager.Update(delta);
	}

	// Verifie si une equipe controle toute une region et lui donne un bonus d'or
	// localTeamId = -1 pour verifier toutes les equipes (mode solo/IA)
	private void CheckRegionBonuses(int localTeamId)
	{
		if (_allCamps.Count == 0) return;

		// Grouper les camps par regionId
		var campsByRegion = new Dictionary<int, List<CampSimple>>();
		foreach (var camp in _allCamps)
		{
			int regionId = camp.RegionId;
			if (regionId <= 0) continue;
			if (!campsByRegion.ContainsKey(regionId))
				campsByRegion[regionId] = new List<CampSimple>();
			campsByRegion[regionId].Add(camp);
		}

		foreach (var (regionId, camps) in campsByRegion)
		{
			if (camps.Count < 2) continue; // une region d'un seul camp ne compte pas

			// Verifier si tous les camps de la region appartiennent a la meme equipe
			int firstTeam = camps[0].GetTeamId();
			if (firstTeam <= 0) continue; // neutre

			bool allSameTeam = true;
			foreach (var camp in camps)
			{
				if (camp.GetTeamId() != firstTeam || camp.IsNeutralCamp)
				{
					allSameTeam = false;
					break;
				}
			}

			if (!allSameTeam) continue;

			// Donner le bonus si ce peer gere cette equipe
			bool shouldGive = localTeamId == -1 || firstTeam == localTeamId;
			if (shouldGive && _teamGold.ContainsKey(firstTeam))
			{
				_teamGold[firstTeam] += RegionBonusGold;
				GD.Print($"[REGION] Equipe {firstTeam} controle la region {regionId} -> +{RegionBonusGold} or");
			}
		}
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
