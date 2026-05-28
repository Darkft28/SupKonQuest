using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controls a bot team in solo vs AI mode.
/// Utility AI architecture: scores 4 modes each tick and picks the best one.
/// Three difficulty levels: Easy / Medium / Hard.
/// The AI uses unlocked tiers and adapts its strategy accordingly.
/// </summary>
public partial class AIController : Node
{
	public enum Difficulty { Easy, Medium, Hard }

	private int _teamId = 2;

	// -- Per-difficulty parameters ------------------------------------------------

	// Interval between decision ticks
	private static readonly float[] TickInterval    = { 6f,  3.5f, 2f   };
	// Max simultaneous unit count
	private static readonly int[]   MaxUnits         = { 8,   16,   28   };
	// Delay before the first attack (allows buildup time)
	private static readonly float[] FirstAttackDelay = { 20f, 12f,  5f   };
	// Reaction delay before executing an order (simulates human latency)
	private static readonly float[] ReactionDelay    = { 5f,  1.5f, 0.3f };
	// Chance to choose a suboptimal target (simulates human mistakes)
	private static readonly float[] ErrorRate        = { 0.4f,0.15f, 0f  };
	// If no attack for N seconds, force an attack
	private static readonly float[] ForcedAttackDelay= { 60f, 40f, 25f };
	// Gold threshold to start saving for tier 2
	private static readonly int[]   Tier2SaveThreshold = { 0, 900, 700 };
	// % of units kept for defense (from total available)
	private static readonly float[] DefenseRatio     = { 0f,  0.15f, 0.20f };
	// % chance to skip a full tick (simulates inattention)
	private static readonly float[] SkipTickChance   = { 0.35f, 0.10f, 0f  };
	// Minimum units at rally point before attacking
	private static readonly int[]   MinRallyUnits    = { 1,   4,    6   };
	// Radius used to consider a unit "arrived" at rally point
	private static readonly float[] RallyArrivalRadius = { 0f, 600f, 500f };
	// Hard naval offense before full home region control: tick chance + min cooldown between waves
	private static readonly float[] NavalEarlyAttackChance = { 0f,  0f,   0.20f };
	private static readonly float[] NavalAttackCooldown    = { 0f,  0f,   30f  };

	// -- Target army composition (ratio per type) --------------------------------
	// Easy: infantry spam, no support units
	// Medium: balanced composition
	// Hard: adaptive composition (see PickUnitToBuy)

	private static readonly Dictionary<string, float>[] TargetComposition =
	{
		// Easy
		new Dictionary<string, float>
		{
			{ "Infantry", 1.0f }
		},
		// Medium - tier 1 only in the base target (Heal/AntiArmor added at tier 2)
		new Dictionary<string, float>
		{
			{ "Infantry", 0.45f },
			{ "Range",    0.35f },
			{ "Support",  0.20f },
			// Heal and AntiArmor are added dynamically when tier 2 is unlocked (see PickUnitToBuy)
		},
		// Hard
		new Dictionary<string, float>
		{
			{ "Infantry", 0.30f },
			{ "Range",    0.30f },
			{ "Support",  0.20f },
			{ "Mortar",   0.10f },
			{ "Heavy",    0.10f }
			// Heal, AntiArmor, and Tank are added dynamically by tier
		}
	};

	// Known boss teams (for visual display in CampSimple)
	public static readonly HashSet<int> BossTeamIds = new HashSet<int>();

	// -- Internal state ------------------------------------------------------------

	private Difficulty _difficulty;
	private int _diffIdx;

	private float _tickTimer;
	private float _gameTimer;
	private float _lastAttackTimer;
	private float _lastNavalAttackTimer;

	private bool  _reactionPending;
	private float _reactionTimer;
	private CampSimple _pendingTarget;
	private List<Unit> _pendingAttackers;

	private readonly Random _rng = new Random();

	// ── Initialisation ────────────────────────────────────────────────────────

	public void Initialize(Difficulty difficulty, int teamId = 2)
	{
		_teamId     = teamId;
		_difficulty = difficulty;
		_diffIdx    = (int)difficulty;
		_tickTimer  = TickInterval[_diffIdx];
		GD.Print($"[IA] Started - team {teamId}, level: {difficulty}");
	}

	// ── Boucle principale ─────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_gameTimer           += dt;
		_lastAttackTimer     += dt;
		_lastNavalAttackTimer += dt;

		// Deferred reaction currently pending
		if (_reactionPending)
		{
			_reactionTimer -= dt;
			if (_reactionTimer <= 0f)
			{
				_reactionPending = false;
				// Ensure target is still enemy-owned (it may have been captured during the delay)
				if (_pendingTarget != null && IsInstanceValid(_pendingTarget)
					&& _pendingTarget.GetTeamId() != _teamId)
					SendUnitsTo(_pendingTarget, _pendingAttackers);
				_pendingTarget   = null;
				_pendingAttackers = null;
			}
			return;
		}

		_tickTimer -= dt;
		if (_tickTimer > 0f) return;
		_tickTimer = TickInterval[_diffIdx];

		RunTick();
	}

	private void RunTick()
	{
		// Simulated inattention: chance to do nothing this tick
		if (SkipTickChance[_diffIdx] > 0f && _rng.NextDouble() < SkipTickChance[_diffIdx])
		{
			GD.Print($"[IA] Team {_teamId} - tick skipped (inattention)");
			return;
		}

		int    tier = GetCurrentTier();
		int    gold = GetGold();

		ManageProduction(tier, gold);
		ManageCombat(tier, gold);
	}

	// -- Economy and production ----------------------------------------------------

	private void ManageProduction(int tier, int gold)
	{
		var aiCamps = GetAICamps();
		if (aiCamps.Count == 0) return;

		// Medium/Hard: buy tier 2 as soon as affordable
		if (_diffIdx > 0 && tier < 2 && gold >= GameManager.Tier2Cost)
		{
			GameManager.Instance?.UnlockTier2(_teamId);
			GD.Print($"[IA] Purchased tier 2!");
			return; // wait for next tick to produce
		}

		if (_diffIdx > 0)
			ManagePortBuying(aiCamps, gold);

		if (_diffIdx > 0)
			ManageShipProduction(aiCamps, tier, gold);

		// Easy: spend all available gold
		// Medium/Hard: save when approaching the tier-2 threshold
		if (_diffIdx > 0 && tier < 2)
		{
			int reserve = Tier2SaveThreshold[_diffIdx];
			if (gold < reserve)
			{
				GD.Print($"[IA] Saving for tier 2 ({gold}/{reserve} gold)");
				return;
			}
		}

		int totalUnits = GetAIUnits().Count;
		int maxUnits   = GetMaxUnits();

		// Normal cap reached -> still allow if composition is unbalanced
		// (e.g., tier 3 just unlocked but army is full of tier-1 Infantry)
		if (totalUnits >= maxUnits)
		{
			if (!NeedsRebalancing(tier))
				return;
			// Rebalancing allowed up to 130% of max cap
			if (totalUnits >= (int)(maxUnits * 1.3f))
				return;
		}

		string unitType = PickUnitToBuy(tier);
		if (unitType == null) return;

		// Camp with shortest queue and enough capacity to buy
		var camp = aiCamps
			.Where(c => c.CanBuyUnit(unitType))
			.OrderBy(c => c.GetQueueCount())
			.FirstOrDefault();

		if (camp != null)
		{
			camp.BuyUnit(unitType);
			GD.Print($"[IA] Achat {unitType} (tier {tier}, or {gold})");
		}
	}

	// Buys a port only if the team fully controls at least one region.
	// Chooses the camp closest to water whose shoreline is within team territory.
	private void ManagePortBuying(List<CampSimple> aiCamps, int gold)
	{
		if (gold < CampSimple.PortCost) return;
		if (!CanBuildPort()) return;

		var sorted = aiCamps
			.Where(c => c.CanBuyPort())
			.OrderByDescending(c => c.GetNearbyWaterCount())
			.ToList();

		foreach (var camp in sorted)
		{
			if (camp.TryAIPlacePort())
			{
				GD.Print($"[IA team {_teamId}] Port built at camp #{camp.CampId}");
				return;
			}
		}
	}

	private bool CanBuildPort()
	{
		if (_diffIdx == 1) return ControlsHomeRegionFully();
		if (_diffIdx >= 2) return ControlsAnyFullRegion();
		return false;
	}

	private bool CanProduceShips()
	{
		if (_diffIdx == 1) return ControlsHomeRegionFully();
		if (_diffIdx >= 2) return ControlsHomeRegionFully() || ControlsAnyFullRegion();
		return false;
	}

	private void ManageShipProduction(List<CampSimple> aiCamps, int tier, int gold)
	{
		if (!CanProduceShips()) return;

		var portCamps = aiCamps.Where(c => c.HasPort).ToList();
		if (portCamps.Count == 0) return;

		string shipType = PickShipToBuy(tier, gold);
		if (shipType == null) return;

		var camp = portCamps
			.Where(c => c.CanBuyShip(shipType))
			.OrderBy(c => c.GetShipQueueCount())
			.FirstOrDefault();

		if (camp != null)
		{
			camp.BuyShip(shipType);
			GD.Print($"[IA team {_teamId}] Purchased ship {shipType} (tier {tier})");
		}
	}

	private string PickShipToBuy(int tier, int gold)
	{
		if (_diffIdx == 1)
		{
			if (tier >= 3 && gold >= ShipStats.GetStats("Destroyer").Price
				&& GameManager.GetShipTier("Destroyer") <= tier)
				return "Destroyer";
			if (GameManager.GetShipTier("Fregate") <= tier)
				return "Fregate";
			return null;
		}

		if (_diffIdx >= 2)
		{
			if (_rng.NextDouble() < 0.15 && GameManager.GetShipTier("Transport") <= tier)
				return "Transport";

			if (tier >= 3 && gold >= ShipStats.GetStats("Destroyer").Price
				&& _rng.NextDouble() < 0.4f)
				return "Destroyer";

			if (GameManager.GetShipTier("Fregate") <= tier)
				return "Fregate";
		}

		return null;
	}

	// True if the team controls 100% of camps in at least one region.
	private bool ControlsAnyFullRegion()
	{
		var allCamps = GameManager.Instance?.GetAllCamps();
		if (allCamps == null) return false;

		var regionGroups = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<CampSimple>>();
		foreach (var camp in allCamps)
		{
			int r = camp.RegionId;
			if (r <= 0) continue;
			if (!regionGroups.ContainsKey(r))
				regionGroups[r] = new System.Collections.Generic.List<CampSimple>();
			regionGroups[r].Add(camp);
		}

		foreach (var (_, camps) in regionGroups)
		{
			if (camps.Count == 0) continue;
			if (camps.TrueForAll(c => c.GetTeamId() == _teamId && !c.IsNeutralCamp))
				return true;
		}
		return false;
	}

	private bool ControlsHomeRegionFully()
	{
		if (GameManager.Instance == null) return false;
		int homeRegion = GameManager.Instance.GetHomeRegion(_teamId);
		if (homeRegion <= 0) return false;

		var allCamps = GameManager.Instance.GetAllCamps();
		if (allCamps == null) return false;

		var homeCamps = allCamps.Where(c => c.RegionId == homeRegion).ToList();
		if (homeCamps.Count == 0) return false;

		return homeCamps.TrueForAll(c => c.GetTeamId() == _teamId && !c.IsNeutralCamp);
	}

	/// <summary>
	/// Selects the unit type to buy based on target composition and unlocked tier.
	/// </summary>
	private string PickUnitToBuy(int tier)
	{
		var allUnits = GetAIUnits();

		// Build target composition based on current tier
		var composition = new Dictionary<string, float>(TargetComposition[_diffIdx]);

		// Tier 2: add Heal + AntiArmor (Medium and Hard)
		if (_diffIdx >= 1 && tier >= 2)
		{
			composition["Heal"]      = 0.12f;
			composition["AntiArmor"] = 0.08f;
			// Reduce Infantry/Range to make room
			if (composition.ContainsKey("Infantry")) composition["Infantry"] -= 0.08f;
			if (composition.ContainsKey("Range"))    composition["Range"]    -= 0.07f;
		}

		// Tier 3: add Mortar, Heavy (Medium+Hard), and Tank (Hard only, rare)
		if (tier >= 3)
		{
			if (_diffIdx >= 1) // Medium + Hard
			{
				composition["Mortar"] = 0.07f;
				composition["Heavy"]  = 0.05f;
				if (composition.ContainsKey("Infantry")) composition["Infantry"] -= 0.06f;
				if (composition.ContainsKey("Range"))    composition["Range"]    -= 0.06f;
			}
			if (_diffIdx >= 2) // Hard only - Tank is rare (expensive, slow to produce)
			{
				composition["Tank"] = 0.04f;
				if (composition.ContainsKey("Infantry")) composition["Infantry"] -= 0.04f;
			}
			// Floor at 0.05 to avoid negative ratios
			foreach (var key in composition.Keys.ToList())
				if (composition[key] < 0.05f) composition[key] = 0.05f;
		}

		// Filter units allowed by current tier
		var allowed = composition.Keys
			.Where(t => GameManager.GetUnitTier(t) <= tier)
			.ToList();

		if (allowed.Count == 0) return "Infantry";

		// Hard: counter-composition when enemy spams Heavy
		if (_diffIdx == 2 && tier >= 2 && allowed.Contains("AntiArmor"))
		{
			int enemyHeavyCount = GetTree().GetNodesInGroup("units")
				.OfType<Unit>()
				.Count(u => u.GetTeamId() == 1 && u.GetUnitType() == "Heavy");
			if (enemyHeavyCount >= 3)
				return "AntiArmor";
		}

		// Compute current composition
		var currentCounts = new Dictionary<string, int>();
		foreach (var t in allowed) currentCounts[t] = 0;
		foreach (var unit in allUnits)
		{
			string type = unit.GetUnitType();
			if (currentCounts.ContainsKey(type))
				currentCounts[type]++;
		}

		int total = Mathf.Max(1, allUnits.Count);

		// Find the most underrepresented type relative to target
		string best = null;
		float biggestGap = float.MinValue;

		foreach (var (type, targetRatio) in composition)
		{
			if (!allowed.Contains(type)) continue;
			float currentRatio = currentCounts[type] / (float)total;
			float gap = targetRatio - currentRatio;
			if (gap > biggestGap)
			{
				biggestGap = gap;
				best = type;
			}
		}

		return best ?? "Infantry";
	}

	// -- Combat management ---------------------------------------------------------

	private void ManageCombat(int tier, int gold)
	{
		if (_gameTimer < FirstAttackDelay[_diffIdx]) return;

		if (_diffIdx > 0)
			ManageNavalCombat(tier);

		bool forceAttack = _lastAttackTimer >= ForcedAttackDelay[_diffIdx];

		// -- Categorize idle units (Medium/Hard) --------------------------------
		// rallied = already at rally point -> protected, do not retask
		// enRoute = idle but not yet at rally -> available for defense or rallying
		List<Unit> rallied = new List<Unit>();
		List<Unit> enRoute;

		if (_diffIdx > 0 && !forceAttack)
		{
			Vector2 rallyPos = GetHomePosition();
			float   arrivalR = RallyArrivalRadius[_diffIdx];
			var idle = GetIdleAIUnits();
			rallied = idle.Where(u => u.GlobalPosition.DistanceTo(rallyPos) <= arrivalR).ToList();
			enRoute = idle.Where(u => u.GlobalPosition.DistanceTo(rallyPos) >  arrivalR).ToList();
		}
		else
		{
			enRoute = GetIdleAIUnits();
		}

		// -- Reactive defense (Medium/Hard) -------------------------------------
		// Uses ONLY en-route units, never the ones already at rally.
		if (_diffIdx > 0 && !forceAttack)
		{
			var threatenedCamp = FindThreatenedAICamp();
			if (threatenedCamp != null && enRoute.Count > 0)
			{
				int defCount = Mathf.Max(1, Mathf.RoundToInt(enRoute.Count * DefenseRatio[_diffIdx]));
				var defenders = enRoute.Take(defCount).ToList();
				foreach (var unit in defenders)
					unit.MoveTo(threatenedCamp.GlobalPosition + RandomOffset(180f));
				enRoute = enRoute.Skip(defCount).ToList();
				GD.Print($"[IA team {_teamId}] Reactive defense: {defCount} units to camp #{threatenedCamp.CampId}");
			}
		}
		// Easy: no reactive defense (DefenseRatio[0] = 0f)

		// -- Rallying (Medium/Hard) ---------------------------------------------
		if (_diffIdx > 0 && !forceAttack)
		{
			Vector2 rallyPos = GetHomePosition();
			// Send en-route units to rally point
			foreach (var unit in enRoute)
				unit.MoveTo(rallyPos + RandomOffset(280f));

			// Adaptive threshold: min(MinRallyUnits, 60% of total army)
			int totalArmy = GetAIUnits().Count;
			int minRally  = Mathf.Min(MinRallyUnits[_diffIdx], Mathf.Max(2, (int)(totalArmy * 0.6f)));

			if (rallied.Count < minRally)
			{
				GD.Print($"[IA team {_teamId}] Ralliement : {rallied.Count}/{minRally}");
				return;
			}

			// Threshold reached -> attack with arrived units
			var target2 = ChooseTarget(tier, false);
			if (target2 == null) return;

			if (ReactionDelay[_diffIdx] > 0f)
			{
				_reactionPending  = true;
				_reactionTimer    = ReactionDelay[_diffIdx];
				_pendingTarget    = target2;
				_pendingAttackers = rallied;
			}
			else
			{
				SendUnitsTo(target2, rallied);
			}
			return;
		}

		// -- Easy / ForceAttack: direct attack ----------------------------------
		if (enRoute.Count == 0) return;

		var target = ChooseTarget(tier, forceAttack);
		if (target == null) return;

		if (!forceAttack && ReactionDelay[_diffIdx] > 0f)
		{
			_reactionPending  = true;
			_reactionTimer    = ReactionDelay[_diffIdx];
			_pendingTarget    = target;
			_pendingAttackers = enRoute;
		}
		else
		{
			SendUnitsTo(target, enRoute);
		}
	}

	private void SendUnitsTo(CampSimple target, List<Unit> units)
	{
		if (units == null) return;
		foreach (var unit in units)
		{
			if (!IsInstanceValid(unit)) continue;

			// AttackCamp: all units navigate toward the same target camp,
			// using the same nav corridor. Two opposing armies attacking each other
			// mutuellement se croisent sur le même chemin et combattent via _opportunisticTarget.
			// Les Heal suivent en MoveTo (AttackCamp les ignore).
			if (unit.GetUnitType() == "Heal")
				unit.MoveTo(target.GlobalPosition + RandomOffset(350f));
			else
				unit.AttackCamp(target);
		}
		_lastAttackTimer = 0f;
		GD.Print($"[IA] {units.Count} units -> camp #{target.CampId} (team {target.GetTeamId()})");
	}

	// -- Naval combat (Medium / Hard) --------------------------------------------

	private void ManageNavalCombat(int tier)
	{
		if (!CanRunNavalOffensive()) return;

		var combatShips = GetIdleCombatShips();
		if (combatShips.Count == 0) return;

		var targetCamp = ChooseNavalTarget();
		if (targetCamp == null) return;

		var anchorCamp = GetAICamps().FirstOrDefault(c => c.HasPort) ?? GetAICamps().FirstOrDefault();
		if (anchorCamp == null) return;

		Vector2 moveTarget = anchorCamp.FindWaterApproachNear(targetCamp.GlobalPosition);
		if (!anchorCamp.IsWaterAtWorldPos(moveTarget))
			return;

		foreach (var ship in combatShips)
			ship.MoveTo(moveTarget + RandomOffset(120f));

		_lastNavalAttackTimer = 0f;
		GD.Print($"[IA team {_teamId}] {combatShips.Count} ships -> water near camp #{targetCamp.CampId}");
	}

	private bool CanRunNavalOffensive()
	{
		if (_diffIdx <= 0) return false;
		if (ControlsHomeRegionFully()) return true;
		if (_diffIdx < 2) return false;

		if (_lastNavalAttackTimer < NavalAttackCooldown[_diffIdx])
			return false;

		return _rng.NextDouble() < NavalEarlyAttackChance[_diffIdx];
	}

	private CampSimple ChooseNavalTarget()
	{
		var allCamps = GameManager.Instance?.GetAllCamps();
		if (allCamps == null) return null;

		var coastalEnemies = allCamps
			.Where(c => c.GetTeamId() != _teamId && c.GetNearbyWaterCount() > 0)
			.OrderBy(c => GetAICenter().DistanceTo(c.GlobalPosition))
			.ToList();

		if (coastalEnemies.Count == 0) return null;

		int pick = ErrorRate[_diffIdx] > 0f && _rng.NextDouble() < ErrorRate[_diffIdx] * 0.5f
			? _rng.Next(Mathf.Min(coastalEnemies.Count, 3))
			: 0;

		return coastalEnemies[pick];
	}

	private List<Ship> GetIdleCombatShips()
	{
		return GetTree().GetNodesInGroup("ships")
			.OfType<Ship>()
			.Where(s => IsInstanceValid(s)
				&& s.GetTeamId() == _teamId
				&& s.GetCurrentHealth() > 0
				&& s.GetShipType() != "Transport"
				&& !s.GetIsMoving())
			.ToList();
	}

	// -- Targeting (Utility scoring) ----------------------------------------------

	/// <summary>
	/// Chooses a camp to attack based on a utility score.
	/// AI prioritizes camps in its home region when aiming for tier 3.
	/// </summary>
	private CampSimple ChooseTarget(int tier, bool forceAttack)
	{
		var allCamps = GameManager.Instance?.GetAllCamps();
		if (allCamps == null) return null;

		// AI home region
		int homeRegion = GameManager.Instance.GetHomeRegion(_teamId);

		var candidates = allCamps
			.Where(c => c.GetTeamId() != _teamId && IsLandReachable(c))
			.Select(c => (camp: c, score: ScoreCamp(c, tier, homeRegion)))
			.OrderByDescending(x => x.score)
			.ToList();

		if (candidates.Count == 0) return null;

		// Error rate: Easy sometimes chooses a suboptimal target
		if (!forceAttack && ErrorRate[_diffIdx] > 0f && _rng.NextDouble() < ErrorRate[_diffIdx])
		{
			int idx = _rng.Next(Mathf.Min(candidates.Count, 3));
			return candidates[idx].camp;
		}

		return candidates[0].camp;
	}

	private float ScoreCamp(CampSimple camp, int currentTier, int homeRegion)
	{
		float score = 0f;
		Vector2 aiCenter = GetAICenter();

		if (!IsLandReachable(camp))
			score -= 10000f;

		// -- Bonus: neutral camp (limited defenses) ------------------------------
		if (camp.IsNeutralCamp)
			score += 2500f;

		// -- Bonus: damaged camp (easier to capture) -----------------------------
		float hpRatio = camp.GetCurrentHealth() / camp.MaxHealth;
		score += (1f - hpRatio) * 1800f;

		// -- Bonus: few defenders ------------------------------------------------
		int defenders = camp.GetLiveDefenders().Count;
		score += Mathf.Max(0, 5 - defenders) * 300f;

		// -- Critical bonus: camp in home region -> aims for tier 3 --------------
		// If AI does not have tier 3 yet, it prioritizes camps in that region
		if (currentTier < 3 && homeRegion > 0 && camp.RegionId == homeRegion)
			score += 3500f;

		// -- Penalty: distance (prefer nearby camps) -----------------------------
		float dist = aiCenter.DistanceTo(camp.GlobalPosition);
		score -= dist * 0.4f;

		// -- Easy: ignores strategic bonuses, attacks nearest --------------------
		if (_diffIdx == 0)
			score = -dist; // simply the nearest

		return score;
	}

	private HashSet<int> GetReachableLandRegions()
	{
		var graph = MapGenerator.TerritoryGraph;
		var ownedRegions = GetAICamps()
			.Select(c => c.RegionId)
			.Where(r => r > 0);
		return TerritoryConnectivity.GetReachableRegions(graph, ownedRegions);
	}

	private bool IsLandReachable(CampSimple camp)
	{
		if (camp.RegionId <= 0)
			return true;

		if (MapGenerator.TerritoryGraph == null)
			return true;

		return GetReachableLandRegions().Contains(camp.RegionId);
	}

	// -- Defense ------------------------------------------------------------------

	private CampSimple FindThreatenedAICamp()
	{
		// Thresholds: defend if camp is in real but still recoverable danger
		// HP < 60% OR enemy within 700px
		return GetAICamps()
			.Where(c =>
			{
				float hpRatio = c.GetCurrentHealth() / c.MaxHealth;
				if (hpRatio < 0.60f) return true;

				return GetTree().GetNodesInGroup("units")
					.OfType<Unit>()
					.Any(u => u.GetTeamId() != _teamId
					       && u.GlobalPosition.DistanceTo(c.GlobalPosition) < 700f);
			})
			.OrderBy(c => c.GetCurrentHealth())
			.FirstOrDefault();
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	private int GetCurrentTier()
		=> GameManager.Instance?.GetUnlockedTier(_teamId) ?? 1;

	// Dynamic cap: minimum between difficulty cap and global per-camp cap
	// Both must stay aligned to avoid wasted ticks attempting purchases
	// that CanBuyUnit() would reject anyway.
	private int GetMaxUnits()
	{
		int extraCamps = Mathf.Max(0, GetAICamps().Count - 1);
		int diffCap    = MaxUnits[_diffIdx] + extraCamps * 4;
		int globalCap  = GameManager.Instance?.GetMaxUnitsForTeam(_teamId) ?? diffCap;
		return Mathf.Min(diffCap, globalCap);
	}

	// True when a unit type in the current tier is significantly underrepresented (>15% gap)
	private bool NeedsRebalancing(int tier)
	{
		var allUnits = GetAIUnits();
		if (allUnits.Count == 0) return false;

		var composition = new Dictionary<string, float>(TargetComposition[_diffIdx]);
		if (_diffIdx >= 1 && tier >= 2) { composition["Heal"] = 0.12f; composition["AntiArmor"] = 0.08f; }
		if (tier >= 3 && _diffIdx >= 1) { composition["Mortar"] = 0.07f; composition["Heavy"] = 0.05f; }
		if (tier >= 3 && _diffIdx >= 2) { composition["Tank"] = 0.04f; }

		var counts = new Dictionary<string, int>();
		foreach (var key in composition.Keys) counts[key] = 0;
		foreach (var u in allUnits)
			if (counts.ContainsKey(u.GetUnitType())) counts[u.GetUnitType()]++;

		int total = allUnits.Count;
		foreach (var (type, targetRatio) in composition)
		{
			if (GameManager.GetUnitTier(type) > tier) continue;
			float currentRatio = counts.TryGetValue(type, out int c) ? c / (float)total : 0f;
			if (targetRatio - currentRatio > 0.15f)
				return true; // this type is short by more than 15%
		}
		return false;
	}

	private int GetGold()
		=> GameManager.Instance?.GetGold(_teamId) ?? 0;

	private List<CampSimple> GetAICamps()
		=> GameManager.Instance?.GetAllCamps()
			?.Where(c => c.GetTeamId() == _teamId)
			.ToList() ?? new List<CampSimple>();

	private List<Unit> GetAIUnits()
		=> GetTree().GetNodesInGroup("units")
			.OfType<Unit>()
			.Where(u => IsInstanceValid(u) && u.GetTeamId() == _teamId && u.GetCurrentHealth() > 0)
			.ToList();

	private List<Unit> GetIdleAIUnits()
		=> GetAIUnits().Where(u => u.IsIdleState()).ToList();

	private Vector2 GetAICenter()
	{
		var units = GetAIUnits();
		if (units.Count > 0)
		{
			Vector2 sum = Vector2.Zero;
			foreach (var u in units) sum += u.GlobalPosition;
			return sum / units.Count;
		}
		var camps = GetAICamps();
		return camps.Count > 0 ? camps[0].GlobalPosition : Vector2.Zero;
	}

	// Returns the centroid of all owned camps.
	// Unlike a fixed home camp, this point moves as AI captures new camps,
	// preventing units from being pulled backward after each capture.
	private Vector2 GetHomePosition()
	{
		var camps = GetAICamps();
		if (camps.Count == 0) return GetAICenter();

		Vector2 sum = Vector2.Zero;
		foreach (var c in camps) sum += c.GlobalPosition;
		return sum / camps.Count;
	}

	private Vector2 RandomOffset(float radius = 200f)
	{
		float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
		float dist  = (float)(_rng.NextDouble() * radius);
		return new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
	}
}
