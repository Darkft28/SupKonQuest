using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Contrôle une équipe bot en mode solo contre IA.
/// Architecture Utility AI : score 4 modes à chaque tick, choisit le meilleur.
/// Trois niveaux de difficulté : Easy / Medium / Hard.
/// L'IA tient compte de ses tiers déverrouillés et adapte sa stratégie en conséquence.
/// </summary>
public partial class AIController : Node
{
	public enum Difficulty { Easy, Medium, Hard }

	private int _teamId = 2;

	// ── Paramètres par difficulté ─────────────────────────────────────────────

	// Intervalle entre chaque tick de décision
	private static readonly float[] TickInterval    = { 6f,  3.5f, 2f   };
	// Nombre max d'unités simultanées
	private static readonly int[]   MaxUnits         = { 8,   16,   28   };
	// Délai avant la toute première attaque (laisse le temps de buildup)
	private static readonly float[] FirstAttackDelay = { 20f, 12f,  5f   };
	// Délai de réaction avant d'exécuter un ordre (simule la lenteur humaine)
	private static readonly float[] ReactionDelay    = { 5f,  1.5f, 0.3f };
	// Chance de choisir une cible sous-optimale (imite l'erreur humaine)
	private static readonly float[] ErrorRate        = { 0.4f,0.15f, 0f  };
	// Si aucune attaque depuis N secondes → forcer une attaque
	private static readonly float[] ForcedAttackDelay= { 60f, 40f, 25f };
	// Seuil d'or pour commencer à économiser vers tier 2 (garde en réserve)
	private static readonly int[]   Tier2SaveThreshold = { 0, 900, 700 };
	// % d'unités gardées en défense (du total disponible)
	private static readonly float[] DefenseRatio     = { 0f,  0.15f, 0.20f };
	// % de chance de passer un tick entier sans rien faire (simule l'inattention)
	private static readonly float[] SkipTickChance   = { 0.35f, 0.10f, 0f  };
	// Nombre minimum d'unités arrivées au point de ralliement avant d'attaquer
	private static readonly int[]   MinRallyUnits    = { 1,   4,    6   };
	// Rayon pour considérer une unité comme "arrivée" au point de ralliement
	private static readonly float[] RallyArrivalRadius = { 0f, 600f, 500f };
	// Offensive navale Hard avant home region complète : chance par tick + cooldown min entre vagues
	private static readonly float[] NavalEarlyAttackChance = { 0f,  0f,   0.20f };
	private static readonly float[] NavalAttackCooldown    = { 0f,  0f,   30f  };

	// ── Composition d'armée cible (ratio par type) ────────────────────────────
	// Easy : spam Infantry, jamais de soutien
	// Medium : composition équilibrée
	// Hard : composition adaptative (voir PickUnitToBuy)

	private static readonly Dictionary<string, float>[] TargetComposition =
	{
		// Easy
		new Dictionary<string, float>
		{
			{ "Infantry", 1.0f }
		},
		// Medium — tier 1 uniquement dans la cible de base (Heal/AntiArmor s'ajoutent à tier 2)
		new Dictionary<string, float>
		{
			{ "Infantry", 0.45f },
			{ "Range",    0.35f },
			{ "Support",  0.20f },
			// Heal et AntiArmor ajoutés dynamiquement si tier 2 déverrouillé (voir PickUnitToBuy)
		},
		// Hard
		new Dictionary<string, float>
		{
			{ "Infantry", 0.30f },
			{ "Range",    0.30f },
			{ "Support",  0.20f },
			{ "Mortar",   0.10f },
			{ "Heavy",    0.10f }
			// Heal, AntiArmor, Tank ajoutés dynamiquement selon tier
		}
	};

	// Équipes boss connues (pour affichage visuel dans CampSimple)
	public static readonly HashSet<int> BossTeamIds = new HashSet<int>();

	// ── État interne ──────────────────────────────────────────────────────────

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
		GD.Print($"[IA] Démarrée — équipe {teamId}, niveau : {difficulty}");
	}

	// ── Boucle principale ─────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_gameTimer           += dt;
		_lastAttackTimer     += dt;
		_lastNavalAttackTimer += dt;

		// Réaction différée en cours
		if (_reactionPending)
		{
			_reactionTimer -= dt;
			if (_reactionTimer <= 0f)
			{
				_reactionPending = false;
				// Vérifier que la cible est toujours ennemie (peut avoir été capturée pendant le délai)
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
		// Inattention simulée : chance de ne rien faire ce tick
		if (SkipTickChance[_diffIdx] > 0f && _rng.NextDouble() < SkipTickChance[_diffIdx])
		{
			GD.Print($"[IA] Équipe {_teamId} — tick ignoré (inattention)");
			return;
		}

		int    tier = GetCurrentTier();
		int    gold = GetGold();

		ManageProduction(tier, gold);
		ManageCombat(tier, gold);
	}

	// ── Gestion économique et production ─────────────────────────────────────

	private void ManageProduction(int tier, int gold)
	{
		var aiCamps = GetAICamps();
		if (aiCamps.Count == 0) return;

		// Medium/Hard : achète le palier 2 dès qu'on peut se le permettre
		if (_diffIdx > 0 && tier < 2 && gold >= GameManager.Tier2Cost)
		{
			GameManager.Instance?.UnlockTier2(_teamId);
			GD.Print($"[IA] Achat palier 2 !");
			return; // on attend le prochain tick pour produire
		}

		if (_diffIdx > 0)
			ManagePortBuying(aiCamps, gold);

		if (_diffIdx > 0)
			ManageShipProduction(aiCamps, tier, gold);

		// Easy : dépense tout sans réfléchir
		// Medium/Hard : économise si on approche du seuil tier 2
		if (_diffIdx > 0 && tier < 2)
		{
			int reserve = Tier2SaveThreshold[_diffIdx];
			if (gold < reserve)
			{
				GD.Print($"[IA] Économise pour tier 2 ({gold}/{reserve} or)");
				return;
			}
		}

		int totalUnits = GetAIUnits().Count;
		int maxUnits   = GetMaxUnits();

		// Plafond normal atteint → autoriser quand même si la composition est déséquilibrée
		// (ex : tier 3 vient de se débloquer mais l'armée est pleine d'Infantry tier 1)
		if (totalUnits >= maxUnits)
		{
			if (!NeedsRebalancing(tier))
				return;
			// Rééquilibrage autorisé jusqu'à 130% du plafond max
			if (totalUnits >= (int)(maxUnits * 1.3f))
				return;
		}

		string unitType = PickUnitToBuy(tier);
		if (unitType == null) return;

		// Camp avec la file la plus courte et la capacité pour acheter
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

	// Achète un port uniquement si l'équipe contrôle entièrement au moins une région.
	// Choisit le camp le plus proche de l'eau dont la côte est dans le territoire de l'équipe.
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
				GD.Print($"[IA team {_teamId}] Port construit au camp #{camp.CampId}");
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
			GD.Print($"[IA team {_teamId}] Achat bateau {shipType} (tier {tier})");
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

	// Vrai si l'équipe contrôle 100% des camps d'au moins une région.
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
	/// Choisit le type d'unité à acheter selon la composition cible et le tier déverrouillé.
	/// </summary>
	private string PickUnitToBuy(int tier)
	{
		var allUnits = GetAIUnits();

		// Construire la composition cible selon le tier actuel
		var composition = new Dictionary<string, float>(TargetComposition[_diffIdx]);

		// Tier 2 : ajouter Heal + AntiArmor (Medium et Hard)
		if (_diffIdx >= 1 && tier >= 2)
		{
			composition["Heal"]      = 0.12f;
			composition["AntiArmor"] = 0.08f;
			// Réduire Infantry/Range pour faire de la place
			if (composition.ContainsKey("Infantry")) composition["Infantry"] -= 0.08f;
			if (composition.ContainsKey("Range"))    composition["Range"]    -= 0.07f;
		}

		// Tier 3 : intégrer Mortar, Heavy (Medium+Hard) et Tank (Hard uniquement, rare)
		if (tier >= 3)
		{
			if (_diffIdx >= 1) // Medium + Hard
			{
				composition["Mortar"] = 0.07f;
				composition["Heavy"]  = 0.05f;
				if (composition.ContainsKey("Infantry")) composition["Infantry"] -= 0.06f;
				if (composition.ContainsKey("Range"))    composition["Range"]    -= 0.06f;
			}
			if (_diffIdx >= 2) // Hard uniquement — Tank rare (coûteux, lent à produire)
			{
				composition["Tank"] = 0.04f;
				if (composition.ContainsKey("Infantry")) composition["Infantry"] -= 0.04f;
			}
			// Plancher à 0.05 pour éviter les ratios négatifs
			foreach (var key in composition.Keys.ToList())
				if (composition[key] < 0.05f) composition[key] = 0.05f;
		}

		// Filtrer les unités autorisées par le tier actuel
		var allowed = composition.Keys
			.Where(t => GameManager.GetUnitTier(t) <= tier)
			.ToList();

		if (allowed.Count == 0) return "Infantry";

		// Hard : contre-composition si l'ennemi spam les Heavy
		if (_diffIdx == 2 && tier >= 2 && allowed.Contains("AntiArmor"))
		{
			int enemyHeavyCount = GetTree().GetNodesInGroup("units")
				.OfType<Unit>()
				.Count(u => u.GetTeamId() == 1 && u.GetUnitType() == "Heavy");
			if (enemyHeavyCount >= 3)
				return "AntiArmor";
		}

		// Calculer la composition actuelle
		var currentCounts = new Dictionary<string, int>();
		foreach (var t in allowed) currentCounts[t] = 0;
		foreach (var unit in allUnits)
		{
			string type = unit.GetUnitType();
			if (currentCounts.ContainsKey(type))
				currentCounts[type]++;
		}

		int total = Mathf.Max(1, allUnits.Count);

		// Trouver le type le plus sous-représenté par rapport à la cible
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

	// ── Gestion du combat ─────────────────────────────────────────────────────

	private void ManageCombat(int tier, int gold)
	{
		if (_gameTimer < FirstAttackDelay[_diffIdx]) return;

		if (_diffIdx > 0)
			ManageNavalCombat(tier);

		bool forceAttack = _lastAttackTimer >= ForcedAttackDelay[_diffIdx];

		// ── Catégoriser les unités idle (Medium/Hard) ─────────────────────────
		// rallied  = déjà au point de rassemblement → protégées, ne pas toucher
		// enRoute  = idle mais pas encore au rally  → disponibles pour défense ou envoi au rally
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

		// ── Défense réactive (Medium/Hard) ────────────────────────────────────
		// N'utilise QUE les unités en route (enRoute), jamais celles déjà au rally.
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
				GD.Print($"[IA team {_teamId}] Défense réactive : {defCount} unités vers camp #{threatenedCamp.CampId}");
			}
		}
		// Easy : pas de défense réactive (DefenseRatio[0] = 0f)

		// ── Regroupement (Medium/Hard) ────────────────────────────────────────
		if (_diffIdx > 0 && !forceAttack)
		{
			Vector2 rallyPos = GetHomePosition();
			// Envoyer les unités en route vers le rally
			foreach (var unit in enRoute)
				unit.MoveTo(rallyPos + RandomOffset(280f));

			// Seuil adaptatif : min(MinRallyUnits, 60% de l'armée totale)
			int totalArmy = GetAIUnits().Count;
			int minRally  = Mathf.Min(MinRallyUnits[_diffIdx], Mathf.Max(2, (int)(totalArmy * 0.6f)));

			if (rallied.Count < minRally)
			{
				GD.Print($"[IA team {_teamId}] Ralliement : {rallied.Count}/{minRally}");
				return;
			}

			// Seuil atteint → attaquer avec les unités arrivées
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

		// ── Easy / ForceAttack : attaque directe ──────────────────────────────
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

			// AttackCamp : toutes les unités naviguent vers le même camp cible,
			// empruntant le même couloir nav. Deux armées adverses qui s'attaquent
			// mutuellement se croisent sur le même chemin et combattent via _opportunisticTarget.
			// Les Heal suivent en MoveTo (AttackCamp les ignore).
			if (unit.GetUnitType() == "Heal")
				unit.MoveTo(target.GlobalPosition + RandomOffset(350f));
			else
				unit.AttackCamp(target);
		}
		_lastAttackTimer = 0f;
		GD.Print($"[IA] {units.Count} unités → camp #{target.CampId} (team {target.GetTeamId()})");
	}

	// ── Combat naval (Medium / Hard) ─────────────────────────────────────────

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
		GD.Print($"[IA team {_teamId}] {combatShips.Count} bateaux → eau près camp #{targetCamp.CampId}");
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

	// ── Ciblage (Utility Scoring) ─────────────────────────────────────────────

	/// <summary>
	/// Choisit le camp à attaquer selon un score d'utilité.
	/// L'IA priorise les camps de sa région d'origine si elle vise le tier 3.
	/// </summary>
	private CampSimple ChooseTarget(int tier, bool forceAttack)
	{
		var allCamps = GameManager.Instance?.GetAllCamps();
		if (allCamps == null) return null;

		// Home region de l'IA
		int homeRegion = GameManager.Instance.GetHomeRegion(_teamId);

		var candidates = allCamps
			.Where(c => c.GetTeamId() != _teamId)
			.Select(c => (camp: c, score: ScoreCamp(c, tier, homeRegion)))
			.OrderByDescending(x => x.score)
			.ToList();

		if (candidates.Count == 0) return null;

		// Taux d'erreur : Easy choisit parfois une cible sous-optimale
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

		// ── Bonus : camp neutre (défenses limitées) ───────────────────────────
		if (camp.IsNeutralCamp)
			score += 2500f;

		// ── Bonus : camp endommagé (plus facile à prendre) ───────────────────
		float hpRatio = camp.GetCurrentHealth() / camp.MaxHealth;
		score += (1f - hpRatio) * 1800f;

		// ── Bonus : peu de défenseurs ─────────────────────────────────────────
		int defenders = camp.GetLiveDefenders().Count;
		score += Mathf.Max(0, 5 - defenders) * 300f;

		// ── Bonus critique : camp dans la région d'origine → vise tier 3 ──────
		// Si l'IA n'a pas encore le tier 3, elle priorise les camps de sa région
		if (currentTier < 3 && homeRegion > 0 && camp.RegionId == homeRegion)
			score += 3500f;

		// ── Malus : distance (préférer les camps proches) ────────────────────
		float dist = aiCenter.DistanceTo(camp.GlobalPosition);
		score -= dist * 0.4f;

		// ── Easy : ignore les bonus stratégiques, attaque au plus proche ──────
		if (_diffIdx == 0)
			score = -dist; // simplement le plus proche

		return score;
	}

	// ── Défense ──────────────────────────────────────────────────────────────

	private CampSimple FindThreatenedAICamp()
	{
		// Seuils : défendre si le camp est en danger réel mais encore sauvable
		// HP < 60% OU ennemi à moins de 700px
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

	// Plafond dynamique : minimum entre le cap de difficulté et le cap global par camp
	// Les deux doivent être cohérents pour éviter des ticks gaspillés à tenter d'acheter
	// quand CanBuyUnit() bloquerait de toute façon.
	private int GetMaxUnits()
	{
		int extraCamps = Mathf.Max(0, GetAICamps().Count - 1);
		int diffCap    = MaxUnits[_diffIdx] + extraCamps * 4;
		int globalCap  = GameManager.Instance?.GetMaxUnitsForTeam(_teamId) ?? diffCap;
		return Mathf.Min(diffCap, globalCap);
	}

	// Vrai si un type d'unité du tier actuel est significativement sous-représenté (>15% d'écart)
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
				return true; // ce type manque de plus de 15%
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

	// Retourne le centroïde de tous les camps possédés.
	// Contrairement au camp natal fixe, ce point avance quand l'IA capture de nouveaux camps,
	// évitant de rappeler les unités vers l'arrière après chaque capture.
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
