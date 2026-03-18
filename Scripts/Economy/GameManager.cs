using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
	private static GameManager _instance;

	public static GameManager Instance
	{
		get { return _instance; }
	}

	private Dictionary<int, int> _teamGold = new Dictionary<int, int>();
	private Dictionary<int, int> _homeRegions = new Dictionary<int, int>();

	private const int StartingGold = 100;
	private const int CaptureBonus = 50;
	private const int PassiveGoldPerSecond = 500;
	private const int RegionBonusGold = 30;

	private float _passiveGoldTimer = 0f;

	// Bonus de vitesse par région contrôlée
	private Dictionary<int, float> _speedMultipliers = new Dictionary<int, float>();
	private const float RegionSpeedBonusPerRegion = 0.20f;

	private List<CampSimple> _allCamps = new List<CampSimple>();

	private const int NumberOfPlayers = 2;

	private VictoryManager _victoryManager;

	public override void _Ready()
	{
		_instance = this;
		_victoryManager = new VictoryManager(this);
	}

	public void OnMapGenerationComplete()
	{
		InitializeCamps();
	}

	private void InitializeCamps()
	{
		// Reset de l'or entre les parties (GameManager est un autoload persistant)
		_teamGold.Clear();
		_homeRegions.Clear();
		_allCamps.Clear();

		var campNodes = GetTree().GetNodesInGroup("camps");
		foreach (var node in campNodes)
		{
			if (node is CampSimple camp)
				_allCamps.Add(camp);
		}

		AssignCampsToPlayers();
	}

	private void AssignCampsToPlayers()
	{
		if (_allCamps.Count == 0)
			return;

		// Shuffle deterministe : meme resultat sur les 2 peers grace a la seed partagee
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int seed = gameState?.MapSeed ?? (int)GD.Randi();

		List<CampSimple> shuffledCamps = new List<CampSimple>(_allCamps);
		ShuffleList(shuffledCamps, seed);

		if (gameState?.IsFreeForAll == true)
		{
			shuffledCamps[0].SetTeam(1, false);
			_homeRegions[1] = shuffledCamps[0].RegionId;
			InitializeTeam(1);

			for (int i = 1; i < shuffledCamps.Count; i++)
			{
				int teamId = i + 1;
				shuffledCamps[i].SetTeam(teamId, false);
				_homeRegions[teamId] = shuffledCamps[i].RegionId;
				InitializeTeam(teamId);
			}
		}
		else
		{
			int campsPerPlayer = shuffledCamps.Count / NumberOfPlayers;
			int campIndex = 0;

			for (int playerId = 1; playerId <= NumberOfPlayers; playerId++)
			{
				bool firstCamp = true;
				for (int i = 0; i < campsPerPlayer; i++)
				{
					if (campIndex < shuffledCamps.Count)
					{
						shuffledCamps[campIndex].SetTeam(playerId, false);
						if (firstCamp) { _homeRegions[playerId] = shuffledCamps[campIndex].RegionId; firstCamp = false; }
						campIndex++;
					}
				}
				InitializeTeam(playerId);
			}

			while (campIndex < shuffledCamps.Count)
			{
				shuffledCamps[campIndex].SetTeam(0, true);
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
		_passiveGoldTimer += (float)delta;
		if (_passiveGoldTimer >= 1.0f)
		{
			_passiveGoldTimer = 0f;

			if (IsMultiplayerActive())
			{
				// Multi : chaque peer gere uniquement l'or de sa propre equipe (pas de sync or)
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
					_teamGold[teamId] += PassiveGoldPerSecond;

				CheckRegionBonuses(-1); // -1 = toutes les equipes
			}

			CheckRegionBonuses();
		}

		_victoryManager.Update(delta);
	}

	private void CheckRegionBonuses(int localTeamId)
	{
		if (_allCamps.Count == 0) return;

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
			if (camps.Count < 2) continue;

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

			bool shouldGive = localTeamId == -1 || firstTeam == localTeamId;
			if (shouldGive && _teamGold.ContainsKey(firstTeam))
				_teamGold[firstTeam] += RegionBonusGold;
		}
	}

	public List<CampSimple> GetAllCamps()
	{
		return _allCamps;
	}

	public void InitializeTeam(int teamId)
	{
		if (teamId <= 0)
			return;

		if (!_teamGold.ContainsKey(teamId))
			_teamGold[teamId] = StartingGold;
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
	}

	public float GetSpeedMultiplier(int teamId)
	{
		return _speedMultipliers.TryGetValue(teamId, out float mult) ? mult : 1f;
	}

	private void CheckRegionBonuses()
	{
		_speedMultipliers.Clear();

		// Détection dynamique des RegionIds présents (variable selon la map : 3 pour Irridium, 4 pour Alabasta)
		var regionCamps = new Dictionary<int, List<CampSimple>>();
		foreach (var camp in _allCamps)
		{
			if (camp == null || !IsInstanceValid(camp)) continue;
			int r = camp.RegionId;
			if (r <= 0) continue;
			if (!regionCamps.ContainsKey(r))
				regionCamps[r] = new List<CampSimple>();
			regionCamps[r].Add(camp);
		}

		foreach (var (r, camps) in regionCamps)
		{
			if (camps.Count == 0) continue;

			int firstTeam = camps[0].TeamId;
			if (firstTeam <= 0) continue; // région neutre

			bool allSameTeam = true;
			foreach (var camp in camps)
			{
				if (camp.IsNeutralCamp || camp.TeamId != firstTeam)
				{
					allSameTeam = false;
					break;
				}
			}

			if (allSameTeam)
			{
				int winningTeam = firstTeam;
				AddGold(winningTeam, 30);
				if (!_speedMultipliers.ContainsKey(winningTeam))
					_speedMultipliers[winningTeam] = 1f;
				_speedMultipliers[winningTeam] += RegionSpeedBonusPerRegion;
			}
		}
	}

	// ── Système de tiers de déverrouillage ───────────────────────────────────

	public static int GetUnitTier(string unitType) => unitType switch
	{
		"Infantry" or "Support" or "Range" => 1,
		"Heal" or "AntiArmor" => 2,
		"Mortar" or "Heavy" or "Tank" => 3,
		_ => 1
	};

	public static int GetShipTier(string shipType) => shipType switch
	{
		"Transport" => 1,
		"Fregate" or "Destroyer" => 3,
		_ => 1
	};

	public int GetHomeRegion(int teamId)
	{
		return _homeRegions.TryGetValue(teamId, out int r) ? r : -1;
	}

	public int GetUnlockedTier(int teamId)
	{
		var ownedCamps = _allCamps.FindAll(c => c.GetTeamId() == teamId);

		if (ownedCamps.Count < 2) return 1;

		// Tier 3 : contrôle tous les camps de sa home region (≥2 camps dans la région)
		if (!_homeRegions.TryGetValue(teamId, out int homeRegion)) return 2;

		var homeCamps = _allCamps.FindAll(c => c.RegionId == homeRegion);
		if (homeCamps.Count < 2) return 2;

		if (homeCamps.TrueForAll(c => c.GetTeamId() == teamId)) return 3;

		return 2;
	}

	// Correction légère de l'or en multijoueur (évite micro-corrections sous 5 or d'écart)
	public void SyncGold(int teamId, int authorativeGold)
	{
		if (!_teamGold.ContainsKey(teamId)) return;

		int diff = Mathf.Abs(_teamGold[teamId] - authorativeGold);
		if (diff > 5)
			_teamGold[teamId] = authorativeGold;
	}
}
