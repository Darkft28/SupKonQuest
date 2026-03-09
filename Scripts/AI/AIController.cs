using Godot;
using System.Collections.Generic;

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

	// Intervalle de décision selon la difficulté
	private float TickInterval => Level switch
	{
		Difficulty.Easy => 10f,
		Difficulty.Hard => 1.5f,
		_ => 3f
	};

	// Nombre max d'unités envoyées en attaque par tick
	private int MaxUnitsPerOrder => Level switch
	{
		Difficulty.Easy => 1,
		Difficulty.Hard => int.MaxValue,
		_ => 6
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

	public override void _Process(double delta)
	{
		_tickTimer += (float)delta;
		if (_tickTimer >= TickInterval)
		{
			_tickTimer = 0f;
			RunTick();
		}
	}

	private void RunTick()
	{
		// Easy : 40% de chance de ne rien faire ce tick
		if (Level == Difficulty.Easy && GD.Randf() < 0.4f)
			return;

		BuyUnits();
		CommandIdleUnits();
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
		foreach (string unitType in AllowedUnits)
		{
			if (camp.CanBuyUnit(unitType))
			{
				camp.BuyUnit(unitType);
				return;
			}
		}
	}

	private void CommandIdleUnits()
	{
		CampSimple target = FindBestAttackTarget();
		if (target == null) return;

		var idleUnits = FindIdleAIUnits();
		int sent = 0;

		foreach (var unit in idleUnits)
		{
			if (sent >= MaxUnitsPerOrder) break;
			unit.AttackCamp(target);
			sent++;
		}
	}

	private List<Unit> FindIdleAIUnits()
	{
		var result = new List<Unit>();
		var units = GetTree().GetNodesInGroup("units");
		foreach (var node in units)
		{
			if (node is Unit unit && unit.GetTeamId() == AITeamId && unit.IsIdleState())
				result.Add(unit);
		}
		return result;
	}

	/// <summary>
	/// Choisit le camp à attaquer en priorité :
	/// 1. Camp neutre dont les défenseurs sont morts (facile à capturer)
	/// 2. Camp ennemi dont les défenseurs sont morts
	/// 3. Camp le plus proche
	/// Score-based : distance de base + pénalités/bonus selon défenseurs et type de camp
	/// </summary>
	private CampSimple FindBestAttackTarget()
	{
		// Position de référence = centre de masse des unités IA
		Vector2 aiCenter = GetAICenter();

		var camps = GetTree().GetNodesInGroup("camps");
		CampSimple bestCamp = null;
		float bestScore = float.MaxValue;

		foreach (var node in camps)
		{
			if (node is not CampSimple camp) continue;
			if (camp.GetTeamId() == AITeamId && !camp.IsNeutralCamp) continue;

			// Easy : ignore les camps encore défendus
			if (Level == Difficulty.Easy && !camp.AreAllUnitsDefeated())
				continue;

			float dist = aiCenter.DistanceTo(camp.GlobalPosition);
			float score = dist;

			if (!camp.AreAllUnitsDefeated())
				score += 4000f; // camp avec défenseurs = plus dur

			if (camp.IsNeutralCamp)
				score -= 2000f; // camps neutres prioritaires (plus faciles)

			if (Level == Difficulty.Hard && !camp.IsNeutralCamp)
				score -= 1000f; // en Hard, l'IA cible aussi les camps ennemis

			if (score < bestScore)
			{
				bestScore = score;
				bestCamp = camp;
			}
		}

		return bestCamp;
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
