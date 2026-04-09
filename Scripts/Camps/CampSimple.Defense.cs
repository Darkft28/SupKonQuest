using Godot;

public partial class CampSimple
{
	private const float TerritoryRadius = 600f; // réduit pour laisser des failles au joueur

	private void SetupCampTimers()
	{
		if (_campTimersSetup)
			return;

		_turretTimerNode = GetNodeOrNull<Timer>("TurretTimer");
		_territoryAlertTimerNode = GetNodeOrNull<Timer>("TerritoryAlertTimer");
		_territoryAlertCooldownTimerNode = GetNodeOrNull<Timer>("TerritoryAlertCooldownTimer");

		if (_turretTimerNode != null)
			_turretTimerNode.Timeout += ProcessTurret;

		if (_territoryAlertTimerNode != null)
			_territoryAlertTimerNode.Timeout += ProcessTerritoryAlert;

		if (_turretTimerNode != null)
		{
			_turretTimerNode.WaitTime = 1.0f;
			_turretTimerNode.OneShot = false;
		}

		if (_territoryAlertTimerNode != null)
		{
			_territoryAlertTimerNode.WaitTime = 1.5f;
			_territoryAlertTimerNode.OneShot = false;
		}

		if (_territoryAlertCooldownTimerNode != null)
		{
			_territoryAlertCooldownTimerNode.WaitTime = 8.0f;
			_territoryAlertCooldownTimerNode.OneShot = true;
		}

		_campTimersSetup = true;
	}

	private void UpdateCampTimers()
	{
		bool shouldRunTimers = !IsNeutralCamp && (IsRelayModeActive() || IsLocallyOwned());

		if (_turretTimerNode != null)
		{
			if (shouldRunTimers)
			{
				if (_turretTimerNode.IsStopped())
					_turretTimerNode.Start();
			}
			else
			{
				_turretTimerNode.Stop();
			}
		}

		if (_territoryAlertTimerNode != null)
		{
			if (shouldRunTimers)
			{
				if (_territoryAlertTimerNode.IsStopped())
					_territoryAlertTimerNode.Start();
			}
			else
			{
				_territoryAlertTimerNode.Stop();
			}
		}

		if (_territoryAlertCooldownTimerNode != null && !shouldRunTimers)
			_territoryAlertCooldownTimerNode.Stop();
	}

	private void ProcessTerritoryAlert()
	{
		if (IsNeutralCamp) return;
		if (!IsRelayModeActive() && !IsLocallyOwned()) return;

		if (_territoryAlertCooldownTimerNode != null && !_territoryAlertCooldownTimerNode.IsStopped())
			return;

		Vector2? intruderPos = FindIntruderInTerritory();
		if (intruderPos.HasValue)
		{
			AlertDefenders(intruderPos.Value);
			_territoryAlertCooldownTimerNode?.Start();
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
		var allUnits = GetTree().GetNodesInGroup("units");
		foreach (var node in allUnits)
		{
			if (node is Unit unit && unit.GetTeamId() == TeamId
				&& unit.IsIdleState() && unit.GetCurrentHealth() > 0
				&& GlobalPosition.DistanceTo(unit.GlobalPosition) <= TerritoryRadius)
			{
				unit.MoveTo(intruderPos);
			}
		}
	}

	private void ProcessTurret()
	{
		AttackEnemiesInRange();
	}

	private void AttackEnemiesInRange()
	{
		if (IsNeutralCamp)
			return;

		if (!IsRelayModeActive() && !IsLocallyOwned())
			return;

		var allUnits = GetTree().GetNodesInGroup("units");

		foreach (var node in allUnits)
		{
			if (node is Unit unit)
			{
				if (!IsInstanceValid(unit))
					continue;

				if (!unit.IsInsideTree())
					continue;

				int unitTeamId = unit.GetTeamId();

				if (unitTeamId == TeamId)
					continue;

				if (unit.GetCurrentHealth() <= 0)
					continue;

				float distance = GlobalPosition.DistanceTo(unit.GlobalPosition);

				if (distance <= TurretRange)
				{
					if (unit.IsLocalAuthority)
					{
						unit.TakeDamage(TurretDamage);
					}
					else if (!string.IsNullOrEmpty(unit.NetworkId))
					{
						NetworkSync.Instance?.SendUnitDamage(unit.NetworkId, TurretDamage, TeamId);
					}
				}
			}
		}
	}

	public bool TakeDamage(float damage, int attackerTeamId)
	{
		if (!IsRelayModeActive() && !IsLocallyOwned())
		{
			NetworkSync.Instance?.SendCampDamage(CampId, damage, attackerTeamId);
			return true;
		}

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

		// Rembourser les files de production avant de changer d'equipe
		RefundProductionQueue(oldTeamId);
		RefundShipProductionQueue(oldTeamId);

		TeamId = newTeamId;
		IsNeutralCamp = false;

		SetCurrentHealth(MaxHealth);
		UpdateCampVisualTheme();
		UpdateCampTimers();
		RefreshCampLabel(); // texte boss/normal selon la nouvelle équipe

		if (GameManager.Instance != null)
		{
			GameManager.Instance.GiveCaptureBonus(newTeamId);

			if (_localGold > 0)
			{
				GameManager.Instance.AddGold(newTeamId, _localGold);
				_localGold = 0;
			}
		}

		SpawnBonusUnits();

		EmitSignal(SignalName.CampCaptured, newTeamId);
		if (!IsRelayModeActive())
			NetworkSync.Instance?.SendCampCaptured(CampId, newTeamId);

		// Appel direct garanti — ne dépend pas de la connexion signal
		TerritoryManager.Instance?.RefreshTerritory(newTeamId);
		GD.Print($"[TERRITOIRE] Camp #{CampId} capturé : Team {oldTeamId} → {newTeamId}");

		// Si l'ancienne équipe n'a plus aucun camp → toutes ses unités meurent
		if (oldTeamId > 0)
		{
			bool hasAnyCamp = false;
			foreach (var camp in GameManager.Instance?.GetAllCamps() ?? new System.Collections.Generic.List<CampSimple>())
			{
				if (camp.GetTeamId() == oldTeamId) { hasAnyCamp = true; break; }
			}

			if (!hasAnyCamp)
			{
				GD.Print($"[ELIMINATION] Team {oldTeamId} n'a plus de camp → toutes ses unités meurent");
				foreach (var node in GetTree().GetNodesInGroup("units"))
				{
					if (node is Unit unit && unit.GetTeamId() == oldTeamId && IsInstanceValid(unit))
						unit.TakeDamage(999999f);
				}
			}
		}
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
			unit.OwnerCamp = this;

			// Reseau : assigner un NetworkId et broadcaster
			int spawnSequence = ++_dynamicUnitSpawnSequence;
			string networkId = BuildDynamicUnitNetworkId(spawnSequence);
			unit.NetworkId = networkId;
			unit.IsLocalAuthority = true;
			unit.OwnerCamp = this;

			GetParent().AddChild(unit);
			_spawnedUnits.Add(unit);
			_defenders.Add(unit); // les bonus units défendent le camp nouvellement capturé

			if (!IsRelayModeActive())
			{
				NetworkSync.Instance?.SendSpawnUnit(networkId, bonusUnits[i], TeamId,
					unit.GlobalPosition.X, unit.GlobalPosition.Y, unit.GetCurrentHealth(), false);
			}
		}
	}
}
