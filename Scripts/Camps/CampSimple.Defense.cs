using Godot;

public partial class CampSimple
{
	private void ProcessTurret(double delta)
	{
		_turretTimer += (float)delta;

		if (_turretTimer >= TurretAttackInterval)
		{
			_turretTimer = 0f;  // Reset à 0, pas 60 !
			AttackEnemiesInRange();
		}
	}

	private void AttackEnemiesInRange()
	{
		// Les camps neutres n'attaquent pas
		if (IsNeutralCamp)
			return;

		// Récupérer toutes les unités
		var allUnits = GetTree().GetNodesInGroup("units");

		foreach (var node in allUnits)
		{
			if (node is Unit unit)
			{
				// Vérifier si l'unité est valide et initialisée
				if (!IsInstanceValid(unit))
					continue;

				// Vérifier si l'unité est dans l'arbre de scène (initialisée)
				if (!unit.IsInsideTree())
					continue;

				// Récupérer le TeamId de l'unité
				int unitTeamId = unit.GetTeamId();

				// Vérifier si c'est un allié (même TeamId)
				if (unitTeamId == TeamId)
					continue; // Allié, on ignore

				// Vérifier si l'unité est vivante
				if (unit.GetCurrentHealth() <= 0)
					continue;

				// Vérifier la distance
				float distance = GlobalPosition.DistanceTo(unit.GlobalPosition);

				if (distance <= TurretRange)
				{
					// Infliger des dégâts
					unit.TakeDamage(TurretDamage);
					GD.Print($"Camp #{CampId} (Team {TeamId}) attaque {unit.GetUnitType()} (Team {unitTeamId}) - Degats: {TurretDamage}");
				}
			}
		}
	}

	public bool TakeDamage(float damage, int attackerTeamId)
	{
		//attaquable seulement si les unitées sont mortes
		if (!AreAllUnitsDefeated())
			return false;

		// Tracker le dernier attaquant
		_lastAttackerTeamId = attackerTeamId;

		SetCurrentHealth(GetCurrentHealth() - damage);

		if (GetCurrentHealth() <= 0)
		{
			CaptureCamp(attackerTeamId);
		}

		return true;
	}

	// Appelée quand la dernière unité défendant le camp meurt
	public void OnDefenderDied(int killerTeamId, bool mutualKill)
	{
		// Si mort mutuelle (attaquant et défenseur meurent en même temps)
		if (mutualKill)
		{
			// Le camp devient neutre
			SetTeam(0, true);
			SetCurrentHealth(MaxHealth);
			GD.Print($"Camp #{CampId} devient neutre suite a une mort mutuelle!");
		}
		else
		{
			// Sinon, le camp peut être capturé par l'équipe du tueur
			_lastAttackerTeamId = killerTeamId;
		}
	}

	private void CaptureCamp(int newTeamId)
	{
		int oldTeamId = TeamId;
		TeamId = newTeamId;
		IsNeutralCamp = false; //les camps neutre ne le sont plus apt=res capture


		SetCurrentHealth(MaxHealth);

		//changement couleur barre de vie
		if (_healthBarForeground != null)
		{
			_healthBarForeground.Color = GetTeamColor();
		}

		//or gagné pour la capture
		if (GameManager.Instance != null)
		{
			GameManager.Instance.GiveCaptureBonus(newTeamId);

			// Transferer l'or accumule du camp neutre au conquerant
			if (_localGold > 0)
			{
				GameManager.Instance.AddGold(newTeamId, _localGold);
				GD.Print($"Camp #{CampId} transfere {_localGold} or accumule a l'equipe {newTeamId}");
				_localGold = 0;
			}
		}

		//spawn quelques troupes après capture
		SpawnBonusUnits();

		GD.Print($"Camp capture! Equipe {oldTeamId} -> Equipe {newTeamId}");
		EmitSignal(SignalName.CampCaptured, newTeamId);
	}

	//spawn 3 troupes
	private void SpawnBonusUnits()
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
			return;

		//position du camp
		Vector2 campPos = GlobalPosition;


		string[] bonusUnits = new[] { "Infantry", "Range", "Infantry" };

		//spawn en cercle
		for (int i = 0; i < bonusUnits.Length; i++)
		{
			var unit = unitScene.Instantiate<Unit>();

			float angle = (i * Mathf.Tau) / bonusUnits.Length;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 525f;

			unit.GlobalPosition = campPos + offset;
			unit.UnitType = bonusUnits[i];
			unit.TeamId = TeamId;
			unit.IsNeutralCampUnit = false;

			GetParent().AddChild(unit);
			_spawnedUnits.Add(unit);
		}
	}
}
