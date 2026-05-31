using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Node
{
	[Signal] public delegate void OnlinePlayerLeftEventHandler(int teamId);
	[Signal] public delegate void LocalPlayerEliminatedEventHandler();
	[Signal] public delegate void GameWonEventHandler(int winningTeamId);

	public const int MaxGold = 9999;

	private static GameManager _instance;

	public static GameManager Instance
	{
		get { return _instance; }
	}

	private Dictionary<int, int> _teamGold = new Dictionary<int, int>();
	private Dictionary<int, int> _teamGoldVersion = new Dictionary<int, int>();
	private Dictionary<int, int> _homeRegions = new Dictionary<int, int>();

	private const int StartingGold = 100;
	private const int CaptureBonus = 50;
	private const int PassiveGoldPerSecond = 75;
	private const int RegionBonusGold = 30;

	private float _passiveGoldTimer = 0f;

	// Speed bonus per controlled region
	private Dictionary<int, float> _speedMultipliers = new Dictionary<int, float>();
	private const float RegionSpeedBonusPerRegion = 0.20f;
	private readonly Dictionary<string, float> _teamUltimateCooldowns = new();
	private const string HealUltimateId = "heal_ultimate";
	private const string SupportUltimateId = "support_ultimate";
	private const float HealUltimateCooldownSeconds = 20f;
	private const float SupportUltimateCooldownSeconds = 25f;
	private const float TeamHealUltimateRadius = 300f;
	private const float TeamHealUltimateAmount = 70f;
	private const float TeamSupportUltimateRadius = 320f;
	private const float TeamSupportUltimateDefenseBonus = 20f;
	private const float TeamSupportUltimateDuration = 8f;

	private List<CampSimple> _allCamps = new List<CampSimple>();

	// Teams that purchased tier 2
	private HashSet<int> _tier2Unlocked = new HashSet<int>();

	private const int MaxHumanPlayers = 8;

	private VictoryManager _victoryManager;
	private bool _localEliminationNotified;

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
		// Reset gold between matches (GameManager is a persistent autoload)
		_teamGold.Clear();
		_teamGoldVersion.Clear();
		_homeRegions.Clear();
		_allCamps.Clear();
		_tier2Unlocked.Clear();
		_teamUltimateCooldowns.Clear();
		_localEliminationNotified = false;
		_teamUnitCounts.Clear();

		var campNodes = GetTree().GetNodesInGroup("camps");
		foreach (var node in campNodes)
		{
			if (node is CampSimple camp)
				_allCamps.Add(camp);
		}

		AssignCampsToPlayers();
		ResyncAndNotifyTeamUnitCounts();
	}

	private void AssignCampsToPlayers()
	{
		if (_allCamps.Count == 0)
			return;

		// Deterministic shuffle: same result on all peers thanks to shared seed
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int seed = gameState != null ? gameState.GetEffectiveMapSeed() : (int)GD.Randi();

		List<CampSimple> shuffledCamps = new List<CampSimple>(_allCamps);
		ShuffleList(shuffledCamps, seed);

		if (gameState?.IsOnline == true)
		{
			int playerCount = Math.Max(2, Math.Min(MaxHumanPlayers, gameState.ActivePlayerCount));
			playerCount = Math.Min(playerCount, shuffledCamps.Count);
			int campIndex = 0;

			for (int playerId = 1; playerId <= playerCount; playerId++)
			{
				if (campIndex >= shuffledCamps.Count)
					break;

				shuffledCamps[campIndex].SetTeam(playerId, false);
				_homeRegions[playerId] = shuffledCamps[campIndex].RegionId;
				InitializeTeam(playerId);
				campIndex++;
			}

			while (campIndex < shuffledCamps.Count)
			{
				shuffledCamps[campIndex].SetTeam(0, true);
				campIndex++;
			}
		}
		else if (gameState?.IsFreeForAll == true)
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
			int playerCount = 2;
			playerCount = Math.Min(playerCount, shuffledCamps.Count);
			const int campsPerPlayer = 1;
			int campIndex = 0;

			for (int playerId = 1; playerId <= playerCount; playerId++)
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

	private static bool IsMultiplayerActive() => GameState.IsOnlineMultiplayer;

	private int GetLocalTeamId()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		return gameState?.LocalTeamId ?? 1;
	}

	private bool IsLocalTeam(int teamId)
	{
		return teamId > 0 && teamId == GetLocalTeamId();
	}

	private int IncrementGoldVersion(int teamId)
	{
		if (!_teamGoldVersion.ContainsKey(teamId))
			_teamGoldVersion[teamId] = 0;

		_teamGoldVersion[teamId] += 1;
		return _teamGoldVersion[teamId];
	}

	public override void _Process(double delta)
	{
		TickTeamUltimateCooldowns((float)delta);

		_passiveGoldTimer += (float)delta;
		if (_passiveGoldTimer >= 1.0f)
		{
			_passiveGoldTimer = 0f;

			if (IsMultiplayerActive())
			{
				// Multiplayer: each peer manages only its own team gold (no gold sync)
				var gameState = GetNodeOrNull<GameState>("/root/GameState");
				int localTeamId = gameState?.LocalTeamId ?? 1;

				if (_teamGold.ContainsKey(localTeamId) && ShouldAccrueGold(localTeamId))
					_teamGold[localTeamId] = Mathf.Min(MaxGold, _teamGold[localTeamId] + PassiveGoldPerSecond);

				CheckRegionBonuses(localTeamId);
			}
			else
			{
				// Solo / IA : même PassiveGoldPerSecond (75) pour toutes les équipes (joueur + bots)
				foreach (var teamId in new List<int>(_teamGold.Keys))
				{
					if (!ShouldAccrueGold(teamId))
						continue;
					_teamGold[teamId] = Mathf.Min(MaxGold, _teamGold[teamId] + PassiveGoldPerSecond);
				}

				CheckRegionBonuses(-1); // -1 = all teams
				CheckLocalPlayerElimination();
			}

			UpdateSpeedMultipliers();
		}

		_victoryManager.Update(delta);
	}

	private static string BuildTeamUltimateKey(int teamId, string abilityId)
	{
		return $"{teamId}:{abilityId}";
	}

	private static float GetUltimateCooldownDuration(string abilityId)
	{
		return abilityId switch
		{
			HealUltimateId => HealUltimateCooldownSeconds,
			SupportUltimateId => SupportUltimateCooldownSeconds,
			_ => 0f,
		};
	}

	private void TickTeamUltimateCooldowns(float delta)
	{
		if (_teamUltimateCooldowns.Count == 0 || delta <= 0f)
			return;

		var keys = new List<string>(_teamUltimateCooldowns.Keys);
		foreach (string key in keys)
		{
			float next = _teamUltimateCooldowns[key] - delta;
			if (next <= 0f)
				_teamUltimateCooldowns.Remove(key);
			else
				_teamUltimateCooldowns[key] = next;
		}
	}

	public float GetTeamUltimateCooldownRemaining(int teamId, string abilityId)
	{
		if (teamId <= 0 || string.IsNullOrWhiteSpace(abilityId))
			return 0f;

		string key = BuildTeamUltimateKey(teamId, abilityId);
		return _teamUltimateCooldowns.TryGetValue(key, out float remaining) ? remaining : 0f;
	}

	public bool CanUseTeamUltimate(int teamId, string abilityId)
	{
		return teamId > 0
			&& !string.IsNullOrWhiteSpace(abilityId)
			&& GetUltimateCooldownDuration(abilityId) > 0f
			&& GetTeamUltimateCooldownRemaining(teamId, abilityId) <= 0f;
	}

	public bool TryStartTeamUltimateCooldown(int teamId, string abilityId)
	{
		if (!CanUseTeamUltimate(teamId, abilityId))
			return false;

		float duration = GetUltimateCooldownDuration(abilityId);
		if (duration <= 0f)
			return false;

		_teamUltimateCooldowns[BuildTeamUltimateKey(teamId, abilityId)] = duration;
		return true;
	}

	public bool TryCastTeamUltimate(int teamId, string abilityId, Vector2 targetPosition)
	{
		if (!TryStartTeamUltimateCooldown(teamId, abilityId))
			return false;

		ApplyTeamUltimateEffect(teamId, abilityId, targetPosition, emitVfx: true);
		return true;
	}

	public void ApplyRemoteTeamUltimateCast(int teamId, string abilityId, Vector2 targetPosition)
	{
		if (teamId <= 0 || string.IsNullOrWhiteSpace(abilityId))
			return;

		float duration = GetUltimateCooldownDuration(abilityId);
		if (duration > 0f)
		{
			string key = BuildTeamUltimateKey(teamId, abilityId);
			float current = GetTeamUltimateCooldownRemaining(teamId, abilityId);
			_teamUltimateCooldowns[key] = Mathf.Max(current, duration);
		}

		// Remote peers rely on relay cast for gameplay and VFX.
		ApplyTeamUltimateEffect(teamId, abilityId, targetPosition, emitVfx: true);
	}

	private void ApplyTeamUltimateEffect(int teamId, string abilityId, Vector2 targetPosition, bool emitVfx = true)
	{
		if (teamId <= 0 || string.IsNullOrWhiteSpace(abilityId))
			return;

		var allUnits = GetTree().GetNodesInGroup("units");
		Unit firstAffectedUnit = null;

		foreach (var node in allUnits)
		{
			if (node is not Unit ally || ally.GetTeamId() != teamId || ally.GetCurrentHealth() <= 0)
				continue;

			if (abilityId == HealUltimateId)
			{
				if (ally.GlobalPosition.DistanceTo(targetPosition) <= TeamHealUltimateRadius)
				{
					ally.Heal(TeamHealUltimateAmount);
					firstAffectedUnit ??= ally;
				}
			}
			else if (abilityId == SupportUltimateId)
			{
				if (ally.GlobalPosition.DistanceTo(targetPosition) <= TeamSupportUltimateRadius)
				{
					ally.ApplyTeamSupportUltimateBonus(TeamSupportUltimateDefenseBonus, TeamSupportUltimateDuration);
					firstAffectedUnit ??= ally;
				}
			}
		}

		if (emitVfx)
			Unit.EmitUltimateCastVfx(firstAffectedUnit, abilityId, targetPosition);
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
			if (firstTeam <= 0) continue; // neutral

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
			if (shouldGive && _teamGold.ContainsKey(firstTeam) && ShouldAccrueGold(firstTeam))
				_teamGold[firstTeam] = Mathf.Min(MaxGold, _teamGold[firstTeam] + RegionBonusGold);
		}
	}

	private void CheckLocalPlayerElimination()
	{
		if (GameState.IsOnlineMultiplayer || _localEliminationNotified)
			return;

		if (!IsLocalPlayerEliminated())
			return;

		_localEliminationNotified = true;
		EmitSignal(SignalName.LocalPlayerEliminated);
	}

	public void NotifyGameWon(int winningTeamId)
	{
		EmitSignal(SignalName.GameWon, winningTeamId);
	}

	public bool TeamOwnsAnyCamp(int teamId)
	{
		if (teamId <= 0)
			return false;

		foreach (var camp in _allCamps)
		{
			if (camp == null || !IsInstanceValid(camp) || camp.IsNeutralCamp)
				continue;
			if (camp.GetTeamId() == teamId)
				return true;
		}
		return false;
	}

	public bool IsTeamEliminated(int teamId) => teamId > 0 && !TeamOwnsAnyCamp(teamId);

	public bool IsLocalPlayerEliminated() =>
		!GameState.IsOnlineMultiplayer && IsTeamEliminated(GetLocalTeamId());

	public bool ShouldAccrueGold(int teamId)
	{
		if (teamId <= 0)
			return false;
		if (!GameState.IsOnlineMultiplayer && IsTeamEliminated(teamId))
			return false;
		return true;
	}

	public List<CampSimple> GetAllCamps()
	{
		return _allCamps;
	}

	// -- Global per-team unit limit -----------------------------------------------
	// 10 units per controlled camp. All team units count,
	// regardless of which camp produced them.
	public const int MaxUnitsPerCamp = 10;

	public event Action<int, int> OnTeamUnitCountChanged;

	private readonly Dictionary<int, int> _teamUnitCounts = new Dictionary<int, int>();

	public int GetTeamUnitCount(int teamId)
	{
		return _teamUnitCounts.TryGetValue(teamId, out int count) ? count : 0;
	}

	public void RegisterTeamUnit(int teamId)
	{
		if (teamId <= 0)
			return;

		if (!_teamUnitCounts.ContainsKey(teamId))
			_teamUnitCounts[teamId] = 0;

		_teamUnitCounts[teamId]++;
		OnTeamUnitCountChanged?.Invoke(teamId, _teamUnitCounts[teamId]);
	}

	public void UnregisterTeamUnit(int teamId)
	{
		if (teamId <= 0 || !_teamUnitCounts.ContainsKey(teamId))
			return;

		_teamUnitCounts[teamId] = Math.Max(0, _teamUnitCounts[teamId] - 1);
		OnTeamUnitCountChanged?.Invoke(teamId, _teamUnitCounts[teamId]);
	}

	public void ResyncTeamUnitCounts()
	{
		_teamUnitCounts.Clear();
		foreach (var node in GetTree().GetNodesInGroup("units"))
		{
			if (node is Unit unit && unit.GetTeamId() > 0 && unit.GetCurrentHealth() > 0)
			{
				int teamId = unit.GetTeamId();
				if (!_teamUnitCounts.ContainsKey(teamId))
					_teamUnitCounts[teamId] = 0;
				_teamUnitCounts[teamId]++;
			}
		}
	}

	public void ResyncAndNotifyTeamUnitCounts()
	{
		var previous = new Dictionary<int, int>(_teamUnitCounts);
		ResyncTeamUnitCounts();

		foreach (var (teamId, count) in _teamUnitCounts)
		{
			if (!previous.TryGetValue(teamId, out int oldCount) || oldCount != count)
				OnTeamUnitCountChanged?.Invoke(teamId, count);
		}
	}

	public int GetMaxUnitsForTeam(int teamId)
	{
		int camps = 0;
		foreach (var c in _allCamps)
			if (c.GetTeamId() == teamId) camps++;
		return Mathf.Max(1, camps) * MaxUnitsPerCamp;
	}

	public void InitializeTeam(int teamId)
	{
		if (teamId <= 0)
			return;

		if (!_teamGold.ContainsKey(teamId))
			_teamGold[teamId] = Mathf.Min(MaxGold, StartingGold);

		if (!_teamGoldVersion.ContainsKey(teamId))
			_teamGoldVersion[teamId] = 0;
	}

	public int GetGold(int teamId)
	{
		if (!_teamGold.TryGetValue(teamId, out int gold))
			return 0;
		return Mathf.Min(MaxGold, gold);
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
		int version = IncrementGoldVersion(teamId);
		return true;
	}

	public void AddGold(int teamId, int amount) => CreditGold(teamId, amount);

	private void CreditGold(int teamId, int amount)
	{
		if (amount <= 0 || !ShouldAccrueGold(teamId))
			return;

		if (!_teamGold.ContainsKey(teamId))
			_teamGold[teamId] = 0;

		_teamGold[teamId] = Mathf.Min(MaxGold, _teamGold[teamId] + amount);
		IncrementGoldVersion(teamId);
	}

	public void GiveCaptureBonus(int teamId)
	{
		AddGold(teamId, CaptureBonus);
	}

	public float GetSpeedMultiplier(int teamId)
	{
		return _speedMultipliers.TryGetValue(teamId, out float mult) ? mult : 1f;
	}

	private void UpdateSpeedMultipliers()
	{
		_speedMultipliers.Clear();

		// Dynamic detection of present RegionIds (map-dependent: 3 for Irridium, 4 for Alabasta)
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
			if (firstTeam <= 0) continue; // neutral region

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
				if (!_speedMultipliers.ContainsKey(winningTeam))
					_speedMultipliers[winningTeam] = 1f;
				_speedMultipliers[winningTeam] += RegionSpeedBonusPerRegion;
			}
		}
	}

	// -- Tier unlock system --------------------------------------------------------

	public static int GetUnitTier(string unitType) => unitType switch
	{
		"Infantry"or "Support"or "Range"=> 1,
		"Heal"or "AntiArmor"=> 2,
		"Mortar"or "Heavy"or "Tank"=> 3,
		_ => 1
	};

	public static int GetShipTier(string shipType) => shipType switch
	{
		"Transport"=> 3,
		"Fregate"or "Destroyer"=> 3,
		_ => 1
	};

	public int GetHomeRegion(int teamId)
	{
		return _homeRegions.TryGetValue(teamId, out int r) ? r : -1;
	}

	public const int Tier2Cost = 1500;

	/// <summary>
	/// Attempts to purchase tier 2 for a team (costs Tier2Cost gold).
	/// Returns true if purchase succeeds.
	/// </summary>
	public bool UnlockTier2(int teamId)
	{
		if (_tier2Unlocked.Contains(teamId)) return false; // already purchased
		if (!CanAfford(teamId, Tier2Cost)) return false;

		SpendGold(teamId, Tier2Cost);
		_tier2Unlocked.Add(teamId);
		GD.Print($"[TIER] Team {teamId} unlocked tier 2!");
		return true;
	}

	public int GetUnlockedTier(int teamId)
	{
		// Tier 2: manual purchase
		if (!_tier2Unlocked.Contains(teamId)) return 1;

		// Tier 3: controls all camps in home region (count computed dynamically)
		if (!_homeRegions.TryGetValue(teamId, out int homeRegion)) return 2;

		var homeCamps = _allCamps.FindAll(c => c.RegionId == homeRegion);
		if (homeCamps.Count == 0) return 2;

		if (homeCamps.TrueForAll(c => c.GetTeamId() == teamId)) return 3;

		return 2;
	}

	public void ApplyPlayerLeaveCleanup(int leavingTeamId)
	{
		if (leavingTeamId <= 0)
			return;

		foreach (var node in GetTree().GetNodesInGroup("units"))
		{
			if (node is Unit unit && unit.GetTeamId() == leavingTeamId && IsInstanceValid(unit))
				unit.QueueFree();
		}

		foreach (var node in GetTree().GetNodesInGroup("ships"))
		{
			if (node is Ship ship && ship.GetTeamId() == leavingTeamId && IsInstanceValid(ship))
				ship.QueueFree();
		}

		foreach (var camp in _allCamps)
		{
			if (camp == null || !IsInstanceValid(camp) || camp.GetTeamId() != leavingTeamId)
				continue;

			camp.NeutralizeCampAfterPlayerLeave();
			camp.RespawnNeutralDefenders();
		}

		_teamGold.Remove(leavingTeamId);
		_teamGoldVersion.Remove(leavingTeamId);
		_homeRegions.Remove(leavingTeamId);
		_tier2Unlocked.Remove(leavingTeamId);
		_teamUnitCounts.Remove(leavingTeamId);

		if (GameState.IsOnlineMultiplayer)
			EmitSignal(SignalName.OnlinePlayerLeft, leavingTeamId);
	}

}
