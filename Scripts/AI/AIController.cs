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
	private static readonly float[] DefenseRatio     = { 0f,  0.25f, 0.3f };
	// % de chance de passer un tick entier sans rien faire (simule l'inattention)
	private static readonly float[] SkipTickChance   = { 0.35f, 0.10f, 0f  };

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
		_gameTimer      += dt;
		_lastAttackTimer += dt;

		// Réaction différée en cours
		if (_reactionPending)
		{
			_reactionTimer -= dt;
			if (_reactionTimer <= 0f)
			{
				_reactionPending = false;
				if (_pendingTarget != null && IsInstanceValid(_pendingTarget))
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
		if (totalUnits >= MaxUnits[_diffIdx]) return;

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

	/// <summary>
	/// Choisit le type d'unité à acheter selon la composition cible et le tier déverrouillé.
	/// </summary>
	private string PickUnitToBuy(int tier)
	{
		var allUnits = GetAIUnits();

		// Construire la composition cible selon le tier actuel
		var composition = new Dictionary<string, float>(TargetComposition[_diffIdx]);

		if (_diffIdx >= 1 && tier >= 2)
		{
			composition["Heal"]      = 0.15f;
			composition["AntiArmor"] = 0.10f;
		}
		if (_diffIdx >= 2 && tier >= 3)
		{
			composition["Tank"] = 0.15f;
		}

		// Filtrer les unités autorisées par le tier actuel
		var allowed = composition.Keys
			.Where(t => GameManager.GetUnitTier(t) <= tier)
			.ToList();

		if (allowed.Count == 0) return "Infantry";

		// Hard : adapter la composition si l'ennemi a beaucoup de Heavy
		if (_diffIdx == 2)
		{
			int enemyHeavyCount = GetTree().GetNodesInGroup("units")
				.OfType<Unit>()
				.Count(u => u.GetTeamId() == 1 && u.GetUnitType() == "Heavy");

			if (enemyHeavyCount >= 3 && tier >= 2 && allowed.Contains("AntiArmor"))
				return "AntiArmor";

			// Débloquer Tank dès que tier 3 accessible
			if (tier >= 3 && allowed.Contains("Tank"))
				return "Tank";
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

		var idleUnits = GetIdleAIUnits();
		if (idleUnits.Count == 0) return;

		bool forceAttack = _lastAttackTimer >= ForcedAttackDelay[_diffIdx];

		// ── Défense réactive (Medium/Hard uniquement) ─────────────────────────
		if (_diffIdx > 0)
		{
			var threatenedCamp = FindThreatenedAICamp();
			if (threatenedCamp != null && !forceAttack)
			{
				int defCount = Mathf.Max(1, Mathf.RoundToInt(idleUnits.Count * DefenseRatio[_diffIdx]));
				var defenders = idleUnits.Take(defCount).ToList();
				foreach (var unit in defenders)
					unit.MoveTo(threatenedCamp.GlobalPosition + RandomOffset(180f));
				idleUnits = idleUnits.Skip(defCount).ToList();
				GD.Print($"[IA] Défense réactive : {defCount} unités vers camp #{threatenedCamp.CampId}");
			}
		}

		if (idleUnits.Count == 0) return;

		// ── Choix de la cible ─────────────────────────────────────────────────
		var target = ChooseTarget(tier, forceAttack);
		if (target == null) return;

		// ── Délai de réaction ─────────────────────────────────────────────────
		if (!forceAttack && ReactionDelay[_diffIdx] > 0f)
		{
			_reactionPending  = true;
			_reactionTimer    = ReactionDelay[_diffIdx];
			_pendingTarget    = target;
			_pendingAttackers = idleUnits;
		}
		else
		{
			SendUnitsTo(target, idleUnits);
			_lastAttackTimer = 0f;
		}
	}

	private void SendUnitsTo(CampSimple target, List<Unit> units)
	{
		if (units == null) return;
		foreach (var unit in units)
		{
			if (!IsInstanceValid(unit)) continue;
			unit.MoveTo(target.GlobalPosition + RandomOffset(220f));
		}
		_lastAttackTimer = 0f;
		GD.Print($"[IA] {units.Count} unités → camp #{target.CampId} (team {target.GetTeamId()})");
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
		// Tri par HP croissant : camp le plus endommagé d'abord
		return GetAICamps()
			.Where(c =>
			{
				float hpRatio = c.GetCurrentHealth() / c.MaxHealth;
				if (hpRatio < 0.75f) return true;

				// Ennemi proche
				return GetTree().GetNodesInGroup("units")
					.OfType<Unit>()
					.Any(u => u.GetTeamId() == 1
					       && u.GlobalPosition.DistanceTo(c.GlobalPosition) < 850f);
			})
			.OrderBy(c => c.GetCurrentHealth())
			.FirstOrDefault();
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	private int GetCurrentTier()
		=> GameManager.Instance?.GetUnlockedTier(_teamId) ?? 1;

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

	private Vector2 RandomOffset(float radius = 200f)
	{
		float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
		float dist  = (float)(_rng.NextDouble() * radius);
		return new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
	}
}
