using Godot;

public partial class CampSimple
{
	private float _alertTimer = 0f;
	private float _alertCooldownTimer = 0f;
	private const float AlertCheckInterval = 1.5f;
	private const float AlertCooldown = 6f;
	private const float TerritoryRadius = 1024f; // 8 tuiles * 128px

	private void ProcessTerritoryAlert(double delta)
	{
		if (IsNeutralCamp) return;
		if (!IsLocallyOwned()) return;

		_alertCooldownTimer -= (float)delta;
		_alertTimer += (float)delta;

		if (_alertTimer < AlertCheckInterval) return;
		_alertTimer = 0f;

		if (_alertCooldownTimer > 0f) return;

		Vector2? intruderPos = FindIntruderInTerritory();
		if (intruderPos.HasValue)
		{
			AlertDefenders(intruderPos.Value);
			_alertCooldownTimer = AlertCooldown;
		}
	}

	private Vector2? FindIntruderInTerritory()
	{
		var allUnits = GetTree().GetNodesInGroup("units");
		foreach (var node in allUnits)
		{
			if (node is Unit unit && unit.GetTeamId() != TeamId
				&& unit.GetCurrentHealth() > 0
				&& GlobalPosition.DistanceTo(unit.GlobalPosition) <= TerritoryRadius)
			{
				return unit.GlobalPosition;
			}
		}
		return null;
	}

	private void AlertDefenders(Vector2 intruderPos)
	{
		GD.Print($"[ALERTE] Camp #{CampId} (T{TeamId}) - intrus a {intruderPos}!");

		var allUnits = GetTree().GetNodesInGroup("units");
		int alerted = 0;
		foreach (var node in allUnits)
		{
			if (node is Unit unit && unit.GetTeamId() == TeamId
				&& unit.IsIdleState() && unit.GetCurrentHealth() > 0
				&& GlobalPosition.DistanceTo(unit.GlobalPosition) <= TerritoryRadius)
			{
				unit.MoveTo(intruderPos);
				alerted++;
			}
		}

		if (alerted > 0)
			GD.Print($"[ALERTE] {alerted} unite(s) alertee(s) vers {intruderPos}");
	}

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

		// La tourelle ne tire que quand tous les défenseurs sont morts (dernier recours)
		if (!AreAllUnitsDefeated())
			return;

		// Reseau : seul le peer qui possede ce camp fait tirer la tourelle
		if (!IsLocallyOwned())
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
					// Infliger des degats (localement ou via RPC)
					if (unit.IsLocalAuthority)
					{
						unit.TakeDamage(TurretDamage);
					}
					else if (!string.IsNullOrEmpty(unit.NetworkId))
					{
						NetworkSync.Instance?.SendUnitDamage(unit.NetworkId, TurretDamage, TeamId);
					}
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

		// Reseau : si on n'a pas l'autorite sur ce camp, envoyer via RPC
		if (!IsLocallyOwned())
		{
			NetworkSync.Instance?.SendCampDamage(CampId, damage, attackerTeamId);
			return true;
		}

		// Tracker le dernier attaquant
		_lastAttackerTeamId = attackerTeamId;

		SetCurrentHealth(GetCurrentHealth() - damage);

		if (GetCurrentHealth() <= 0)
		{
			CaptureCamp(attackerTeamId);
		}

		return true;
	}

	// Appelée quand une unité défendant le camp meurt
	public void OnDefenderDied(int killerTeamId, bool mutualKill)
	{
		_lastAttackerTeamId = killerTeamId;
	}

	private void CaptureCamp(int newTeamId)
	{
		int oldTeamId = TeamId;
		TeamId = newTeamId;
		IsNeutralCamp = false;

		SetCurrentHealth(MaxHealth);

		if (_healthBarForeground != null)
		{
			_healthBarForeground.Color = GetTeamColor();
		}

		if (_campIdLabel != null)
		{
			_campIdLabel.AddThemeColorOverride("font_color", GetTeamColor());
		}

		if (GameManager.Instance != null)
		{
			GameManager.Instance.GiveCaptureBonus(newTeamId);

			if (_localGold > 0)
			{
				GameManager.Instance.AddGold(newTeamId, _localGold);
				GD.Print($"Camp #{CampId} transfere {_localGold} or accumule a l'equipe {newTeamId}");
				_localGold = 0;
			}
		}

		SpawnBonusUnits();

		GD.Print($"Camp capture! Equipe {oldTeamId} -> Equipe {newTeamId}");
		EmitSignal(SignalName.CampCaptured, newTeamId);

		// Reseau : notifier l'autre peer
		NetworkSync.Instance?.SendCampCaptured(CampId, newTeamId);
	}

	private void SpawnBonusUnits()
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
			return;

		Vector2 campPos = GlobalPosition;
		string[] bonusUnits = new[] { "Infantry", "Range", "Infantry" };

		for (int i = 0; i < bonusUnits.Length; i++)
		{
			var unit = unitScene.Instantiate<Unit>();

			float angle = (i * Mathf.Tau) / bonusUnits.Length;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 525f;

			unit.GlobalPosition = campPos + offset;
			unit.UnitType = bonusUnits[i];
			unit.TeamId = TeamId;
			unit.IsNeutralCampUnit = false;

			// Reseau : assigner un NetworkId et broadcaster
			string networkId = NetworkEntityRegistry.GenerateId();
			unit.NetworkId = networkId;
			unit.IsLocalAuthority = true;
			unit.OwnerCamp = this;

			GetParent().AddChild(unit);
			_spawnedUnits.Add(unit);

			NetworkSync.Instance?.SendSpawnUnit(networkId, bonusUnits[i], TeamId,
				unit.GlobalPosition.X, unit.GlobalPosition.Y, unit.GetCurrentHealth(), false);
		}
	}
}
