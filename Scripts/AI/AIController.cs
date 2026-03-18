using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Contrôleur IA pour le mode "Contre IA".
/// Gère l'achat d'unités et les ordres de déplacement pour l'équipe AITeamId.
/// Trois niveaux : Easy, Medium, Hard.
/// </summary>
public partial class AIController : Node
{
	public enum Difficulty { Easy, Medium, Hard }

	public int AITeamId { get; set; } = 2;
	public Difficulty Level { get; set; } = Difficulty.Medium;

	private float _tickTimer = 0f;

	// IA-02-01 : timer de début de partie avant la première attaque
	private float _gameStartTimer = 0f;

	// IA-02-02 : délai de réaction entre décision et exécution des ordres
	private float _pendingCommandTimer = 0f;
	private bool _hasPendingCommand = false;

	// IA-02-06 : temps écoulé depuis la dernière attaque lancée
	private float _timeSinceLastAttack = 0f;

	// IA-02-04 : tracker les groupes d'attaque actifs pour la retraite
	private class AttackGroup
	{
		public List<Unit> Units;
		public int InitialCount;
		public CampSimple Target;
	}
	private readonly List<AttackGroup> _activeAttackGroups = new List<AttackGroup>();

	// Naval : l'IA a conquis tous les camps de sa région de départ
	private bool _homeRegionConquered = false;

	// RNG interne par instance : initialisé avec mapSeed ^ teamId pour briser la symétrie
	private Random _rng;
	// Fix 4 : seuil de retraite unique par instance (entre 0.40 et 0.60)
	private float _retreatThreshold;

	// Intervalle de décision selon la difficulté
	private float TickInterval => Level switch
	{
		Difficulty.Easy => 5f,
		Difficulty.Hard => 1.5f,
		_ => 3f
	};

	// Nombre max d'unités envoyées en attaque par tick
	private int MaxUnitsPerOrder => Level switch
	{
		Difficulty.Easy => 2,
		Difficulty.Hard => 12,
		_ => 6
	};

	// Nombre minimum de défenseurs à conserver par camp IA
	private int MinDefendersPerCamp => Level switch
	{
		Difficulty.Easy => 2,
		Difficulty.Hard => 1,
		_ => 2
	};

	// Nombre minimum d'unités disponibles pour lancer une attaque
	private int MinAttackForce => Level switch
	{
		Difficulty.Easy => 3,
		Difficulty.Hard => 2,
		_ => 3
	};

	// IA-02-01 : délai avant la première attaque selon la difficulté
	private float FirstAttackDelay => Level switch
	{
		Difficulty.Easy => 15f,
		Difficulty.Hard => 7f,
		_ => 10f
	};

	// IA-02-02 : délai de réaction entre décision et exécution des ordres
	private float ReactionDelay => Level switch
	{
		Difficulty.Easy => 2f,
		Difficulty.Hard => 0.5f,
		_ => 1.5f
	};

	// IA-02-06 : intervalle max sans attaque avant d'en forcer une (anti-turtling)
	private float ForceAttackInterval => Level switch
	{
		Difficulty.Easy => 180f,
		Difficulty.Hard => 90f,
		_ => 120f
	};

	// Probabilité de sauter un tick selon la difficulté
	private float SkipChance => Level switch
	{
		Difficulty.Easy => 0.15f,
		Difficulty.Hard => 0.05f,
		_ => 0.10f
	};

	// IA-02-03 : taux d'erreur de ciblage (chance de choisir une cible sous-optimale)
	private float MistakeRate => Level switch
	{
		Difficulty.Easy => 0.40f,
		Difficulty.Hard => 0.05f,
		_ => 0.20f
	};

	// Types d'unités autorisés selon la difficulté
	private static readonly string[] EasyUnits   = { "Infantry", "Range" };
	private static readonly string[] MediumUnits = { "Infantry", "Range", "Support", "AntiArmor", "Heavy" };
	private static readonly string[] HardUnits   = { "Infantry", "Range", "Support", "AntiArmor", "Heavy", "Mortar", "Tank" };

	private string[] AllowedUnits => Level switch
	{
		Difficulty.Easy => EasyUnits,
		Difficulty.Hard => HardUnits,
		_ => MediumUnits
	};

	public override void _Ready()
	{
		// Fix 2 : RNG interne unique par instance — seed = mapSeed XOR (teamId * 1337)
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int seed = (gameState?.MapSeed ?? 0) ^ (AITeamId * 1337);
		_rng = new Random(seed);

		// Fix 1 : décalage de phase du tick — premier tick à un moment unique dans [0, TickInterval]
		_tickTimer = (float)(_rng.NextDouble() * TickInterval);

		// Fix 4 : seuil de retraite variable entre 40% et 60%
		_retreatThreshold = 0.40f + (float)(_rng.NextDouble() * 0.20f);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// IA-02-01 : accumuler le temps de jeu
		_gameStartTimer += dt;

		// IA-02-06 : accumuler le temps sans attaque
		_timeSinceLastAttack += dt;

		// IA-02-02 : exécuter les ordres en attente après le délai de réaction
		if (_hasPendingCommand)
		{
			_pendingCommandTimer += dt;
			if (_pendingCommandTimer >= ReactionDelay)
			{
				_hasPendingCommand = false;
				_pendingCommandTimer = 0f;
				CommandIdleUnits();
			}
		}

		_tickTimer += dt;
		if (_tickTimer >= TickInterval)
		{
			_tickTimer = 0f;
			RunTick();
		}
	}

	private void RunTick()
	{
		if ((float)_rng.NextDouble() < SkipChance)
			return;

		// IA-02-04 : vérifier les retraites avant tout
		CheckRetreat();

		// Naval : mettre à jour l'état de conquête de la région de départ
		if (!_homeRegionConquered)
			_homeRegionConquered = HasConqueredHomeRegion();

		BuyUnits();

		// IA-02-02 : déclencher la commande avec délai de réaction
		_hasPendingCommand = true;
		_pendingCommandTimer = 0f;
	}

	// Retourne true si tous les camps de la région de départ IA appartiennent à l'IA.
	// La région de départ est celle du camp IA le plus proche du centre de masse des unités IA.
	private bool HasConqueredHomeRegion()
	{
		int homeRegion = GetAIRegionId();
		if (homeRegion <= 0) return false;

		var camps = GetTree().GetNodesInGroup("camps");
		bool foundAnyCamp = false;
		foreach (var node in camps)
		{
			if (node is not CampSimple camp) continue;
			if (camp.RegionId != homeRegion) continue;
			foundAnyCamp = true;
			if (camp.GetTeamId() != AITeamId) return false;
		}
		return foundAnyCamp;
	}

	// IA-02-04 : vérifie si un groupe d'attaque a perdu >50% de ses unités initiales
	// Si oui (Easy/Medium uniquement), les survivants retraitent vers le camp IA le plus proche
	private void CheckRetreat()
	{
		if (Level == Difficulty.Hard)
			return;

		for (int i = _activeAttackGroups.Count - 1; i >= 0; i--)
		{
			AttackGroup group = _activeAttackGroups[i];
			int survivors = group.Units.Count(u => IsInstanceValid(u) && u.GetCurrentHealth() > 0);

			if (survivors == 0)
			{
				_activeAttackGroups.RemoveAt(i);
			}
			else if (survivors < group.InitialCount * _retreatThreshold)
			{
				// Les survivants sont libérés — leur état Idle reprend et ils trouvent
				// de nouvelles cibles seuls, sans forcer un retour à la base
				_activeAttackGroups.RemoveAt(i);
			}
		}
	}

	// Retourne le camp IA le plus proche d'une unité donnée
	private CampSimple GetNearestAICamp(Unit unit)
	{
		var camps = GetTree().GetNodesInGroup("camps");
		CampSimple nearest = null;
		float minDist = float.MaxValue;

		foreach (var node in camps)
		{
			if (node is not CampSimple camp) continue;
			if (camp.GetTeamId() != AITeamId || camp.IsNeutralCamp) continue;

			float dist = unit.GlobalPosition.DistanceTo(camp.GlobalPosition);
			if (dist < minDist)
			{
				minDist = dist;
				nearest = camp;
			}
		}
		return nearest;
	}

	private void BuyUnits()
	{
		var camps = GetTree().GetNodesInGroup("camps");
		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetTeamId() == AITeamId && !camp.IsNeutralCamp)
			{
				if (camp.GetQueueCount() < camp.GetMaxQueueSize())
					TryBuyUnit(camp);
			}
		}
	}

	private void TryBuyUnit(CampSimple camp)
	{
		// Collecter toutes les unités que l'on peut se permettre
		var affordable = new List<string>();
		foreach (string unitType in AllowedUnits)
		{
			if (camp.CanBuyUnit(unitType))
				affordable.Add(unitType);
		}

		if (affordable.Count == 0) return;

		// Choisir aléatoirement parmi les unités abordables pour diversifier la composition
		string chosen = affordable[_rng.Next(0, affordable.Count)];
		camp.BuyUnit(chosen);
	}

	private void CommandIdleUnits()
	{
		// IA-02-01 : ne pas attaquer avant la fin du délai initial
		if (_gameStartTimer < FirstAttackDelay) return;

		// IA-02-05 : priorité absolue à la défense si un camp IA est menacé
		CampSimple threatenedCamp = FindThreatenedAICamp();
		if (threatenedCamp != null)
		{
			var idleUnits = FindIdleAIUnits();
			idleUnits.Sort((a, b) =>
				a.GlobalPosition.DistanceTo(threatenedCamp.GlobalPosition)
				.CompareTo(b.GlobalPosition.DistanceTo(threatenedCamp.GlobalPosition)));

			int defenseSent = 0;
			foreach (var unit in idleUnits)
			{
				if (defenseSent >= MaxUnitsPerOrder) break;
				unit.MoveTo(threatenedCamp.GlobalPosition);
				defenseSent++;
			}
			return; // pas d'attaque ce tick
		}

		CampSimple target = FindBestAttackTarget();
		if (target == null) return;

		var allIdleUnits = FindIdleAIUnits();

		// Grouper les unités idle par camp propriétaire
		var unitsByCamp = new Dictionary<CampSimple, List<Unit>>();
		var roamingUnits = new List<Unit>();

		foreach (var unit in allIdleUnits)
		{
			var owner = unit.OwnerCamp;
			if (owner != null && IsInstanceValid(owner) && owner.GetTeamId() == AITeamId && !owner.IsNeutralCamp)
			{
				if (!unitsByCamp.ContainsKey(owner))
					unitsByCamp[owner] = new List<Unit>();
				unitsByCamp[owner].Add(unit);
			}
			else
			{
				roamingUnits.Add(unit);
			}
		}

		// Pour chaque camp, retenir MinDefendersPerCamp unités en défense
		// Les Heal ne comptent pas comme attaquants (AttackCamp() les ignore silencieusement)
		var attackers = new List<Unit>();
		foreach (var (_, units) in unitsByCamp)
		{
			for (int i = MinDefendersPerCamp; i < units.Count; i++)
			{
				if (units[i].UnitType != "Heal")
					attackers.Add(units[i]);
			}
		}
		foreach (var unit in roamingUnits)
		{
			if (unit.UnitType != "Heal")
				attackers.Add(unit);
		}

		// Seuil minimal : ne pas attaquer avec trop peu d'unités
		// IA-02-06 : bypasser si l'intervalle max sans attaque est dépassé (anti-turtling)
		bool forceAttack = _timeSinceLastAttack >= ForceAttackInterval;
		if (attackers.Count < MinAttackForce && !forceAttack) return;

		var sentUnits = new List<Unit>();
		int sent = 0;
		foreach (var unit in attackers)
		{
			if (sent >= MaxUnitsPerOrder) break;
			unit.AttackCamp(target);
			sentUnits.Add(unit);
			sent++;
		}

		if (sent > 0)
		{
			// IA-02-04 : enregistrer le groupe pour suivi de retraite
			_activeAttackGroups.Add(new AttackGroup
			{
				Units = sentUnits,
				InitialCount = sentUnits.Count,
				Target = target
			});
			// IA-02-06 : réinitialiser le compteur anti-turtling
			_timeSinceLastAttack = 0f;
		}
	}

	// IA-02-05 : retourne le camp IA le plus menacé, ou null si aucun n'est en danger
	private CampSimple FindThreatenedAICamp()
	{
		var camps = GetTree().GetNodesInGroup("camps");
		CampSimple mostThreatened = null;
		float lowestHpRatio = 1f;

		foreach (var node in camps)
		{
			if (node is not CampSimple camp) continue;
			if (camp.GetTeamId() != AITeamId || camp.IsNeutralCamp) continue;

			float hpRatio = camp.GetCurrentHealth() / camp.MaxHealth;

			bool threatened;
			if (Level == Difficulty.Easy)
				threatened = hpRatio < 0.6f; // réaction tardive sur Easy
			else
				threatened = hpRatio < 0.8f || HasEnemiesNearCamp(camp);

			if (threatened && hpRatio < lowestHpRatio)
			{
				lowestHpRatio = hpRatio;
				mostThreatened = camp;
			}
		}
		return mostThreatened;
	}

	// Retourne true si au moins un ennemi vivant est dans le rayon de détection du camp
	private bool HasEnemiesNearCamp(CampSimple camp)
	{
		const float detectionRadius = 500f;
		var units = GetTree().GetNodesInGroup("units");
		foreach (var node in units)
		{
			if (node is Unit unit
				&& unit.GetTeamId() != AITeamId
				&& unit.GetCurrentHealth() > 0
				&& camp.GlobalPosition.DistanceTo(unit.GlobalPosition) <= detectionRadius)
				return true;
		}
		return false;
	}

	private List<Unit> FindIdleAIUnits()
	{
		var result = new List<Unit>();
		var units = GetTree().GetNodesInGroup("units");
		foreach (var node in units)
		{
			if (node is not Unit unit) continue;
			if (!IsInstanceValid(unit)) continue;
			if (unit.GetCurrentHealth() <= 0) continue;
			if (unit.GetTeamId() == AITeamId && unit.IsIdleState())
				result.Add(unit);
		}
		return result;
	}

	/// <summary>
	/// Choisit le camp à attaquer en priorité :
	/// 1. Camp neutre dont les défenseurs sont morts (facile à capturer)
	/// 2. Camp ennemi dont les défenseurs sont morts
	/// 3. Camp le plus proche
	/// Score-based : distance de base + pénalités/bonus selon défenseurs, type de camp et territoire.
	/// IA-02-03 : sur Easy/Medium, un taux d'erreur peut faire choisir une cible aléatoire.
	/// </summary>
	private CampSimple FindBestAttackTarget()
	{
		Vector2 aiCenter = GetAICenter();
		int currentAIRegion = GetAIRegionId();
		var graph = MapGenerator.TerritoryGraph;

		var camps = GetTree().GetNodesInGroup("camps");
		var candidates = new List<CampSimple>();
		CampSimple bestCamp = null;
		float bestScore = float.MaxValue;

		foreach (var node in camps)
		{
			if (node is not CampSimple camp) continue;
			if (camp.GetTeamId() == AITeamId && !camp.IsNeutralCamp) continue;

			if (Level == Difficulty.Easy && !camp.AreAllUnitsDefeated())
				continue;

			float dist = aiCenter.DistanceTo(camp.GlobalPosition);
			// Fix 3 : bruit ±200 pour briser la symétrie de ciblage entre deux IA identiques
			float noise = (float)(_rng.NextDouble() * 400.0 - 200.0);
			float score = dist + noise;

			if (!camp.AreAllUnitsDefeated())
				score += 4000f;

			if (camp.IsNeutralCamp)
				score -= 2000f;

			if (Level == Difficulty.Hard && !camp.IsNeutralCamp)
				score -= 1000f;

			if (currentAIRegion > 0 && graph != null)
			{
				if (camp.RegionId == currentAIRegion)
					score -= 3000f;
				else if (camp.RegionId > 0 && TerritoryConnectivity.AreConnected(graph, currentAIRegion, camp.RegionId))
					score -= 1500f;
			}

			// Naval : si la région de départ est conquise, prioriser fortement les camps avec port
			if (_homeRegionConquered && camp.HasPort)
				score -= 5000f;

			candidates.Add(camp);

			if (score < bestScore)
			{
				bestScore = score;
				bestCamp = camp;
			}
		}

		// IA-02-03 : erreur de ciblage — retourner une cible aléatoire parmi les candidats
		if (bestCamp != null && candidates.Count > 1 && (float)_rng.NextDouble() < MistakeRate)
			return candidates[_rng.Next(0, candidates.Count)];

		return bestCamp;
	}

	// Retourne le RegionId du camp IA le plus proche du centre de masse des unités IA
	private int GetAIRegionId()
	{
		Vector2 aiCenter = GetAICenter();
		var camps = GetTree().GetNodesInGroup("camps");
		float closestDist = float.MaxValue;
		int regionId = 0;

		foreach (var node in camps)
		{
			if (node is not CampSimple camp) continue;
			if (camp.GetTeamId() != AITeamId || camp.IsNeutralCamp) continue;
			if (camp.RegionId <= 0) continue;

			float dist = aiCenter.DistanceTo(camp.GlobalPosition);
			if (dist < closestDist)
			{
				closestDist = dist;
				regionId = camp.RegionId;
			}
		}
		return regionId;
	}

	private Vector2 GetAICenter()
	{
		var units = GetTree().GetNodesInGroup($"team_{AITeamId}");
		if (units.Count == 0) return Vector2.Zero;

		Vector2 sum = Vector2.Zero;
		int count = 0;
		foreach (var node in units)
		{
			if (node is Unit unit)
			{
				sum += unit.GlobalPosition;
				count++;
			}
		}
		return count > 0 ? sum / count : Vector2.Zero;
	}
}
