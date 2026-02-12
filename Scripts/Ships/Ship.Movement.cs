using Godot;

public partial class Ship
{
	private void ProcessIdleState(double delta)
	{
		if (ShipType == "Transport") return;

		if (_currentTarget == null)
		{
			Ship enemy = FindEnemyShipInRange();
			if (enemy != null)
			{
				SetNewTarget(enemy);
			}
		}
	}

	private void ProcessMovingToTargetState(double delta)
	{
		if (!IsTargetValid())
		{
			ChangeState(ShipState.Idle);
			return;
		}

		float distanceToTarget = GlobalPosition.DistanceTo(_currentTarget.GlobalPosition);

		if (distanceToTarget <= _stats.Range)
		{
			ChangeState(ShipState.Attacking);
			return;
		}

		Vector2 direction = (_currentTarget.GlobalPosition - GlobalPosition).Normalized();
		Vector2 desiredVelocity = direction * _stats.Speed;

		// Verifier que la destination est sur l'eau
		Vector2 nextPos = GlobalPosition + desiredVelocity * (float)delta;
		if (IsWaterTile(nextPos))
		{
			Velocity = desiredVelocity;
			UpdateSpriteDirection(desiredVelocity);
			MoveAndSlide();
		}
		else
		{
			// Essayer de contourner l'obstacle
			Vector2 slideVelocity = FindAlternativeWaterDirection(direction, (float)delta);
			if (slideVelocity != Vector2.Zero)
			{
				Velocity = slideVelocity;
				UpdateSpriteDirection(slideVelocity);
				MoveAndSlide();
			}
			else
			{
				Velocity = Vector2.Zero;
			}
		}

		ProcessStuckDetection();
	}

	private void ProcessMovingToPointState(double delta)
	{
		if (!_targetPosition.HasValue)
		{
			ChangeState(ShipState.Idle);
			return;
		}

		// Chercher des ennemis en route (sauf Transport)
		if (ShipType != "Transport")
		{
			Ship enemy = FindEnemyShipInRange();
			if (enemy != null)
			{
				_currentTarget = enemy;
				float distToEnemy = GlobalPosition.DistanceTo(enemy.GlobalPosition);
				if (distToEnemy <= _stats.Range)
					ChangeState(ShipState.Attacking);
				else
					ChangeState(ShipState.MovingToTarget);
				return;
			}
		}

		Vector2 direction = (_targetPosition.Value - GlobalPosition).Normalized();
		float distance = GlobalPosition.DistanceTo(_targetPosition.Value);

		if (distance < ArrivalDistance)
		{
			_targetPosition = null;

			// Debarquement en attente : decharger les troupes a l'arrivee
			if (_pendingUnloadPosition.HasValue)
			{
				UnloadUnits(_pendingUnloadPosition.Value);
				_pendingUnloadPosition = null;
			}

			ChangeState(ShipState.Idle);
			return;
		}

		Vector2 desiredVelocity = direction * _stats.Speed;

		// Verifier que la destination est sur l'eau
		Vector2 nextPos = GlobalPosition + desiredVelocity * (float)delta;
		if (IsWaterTile(nextPos))
		{
			Velocity = desiredVelocity;
			UpdateSpriteDirection(desiredVelocity);
			MoveAndSlide();
		}
		else
		{
			// Essayer de contourner l'obstacle au lieu de s'arreter
			Vector2 slideVelocity = FindAlternativeWaterDirection(direction, (float)delta);
			if (slideVelocity != Vector2.Zero)
			{
				Velocity = slideVelocity;
				UpdateSpriteDirection(slideVelocity);
				MoveAndSlide();
			}
			else
			{
				// Bloque par la terre - si debarquement en attente, decharger ici
				if (_pendingUnloadPosition.HasValue)
				{
					UnloadUnits(_pendingUnloadPosition.Value);
					_pendingUnloadPosition = null;
				}

				_targetPosition = null;
				ChangeState(ShipState.Idle);
				GD.Print($"[SHIP] {ShipType} T{TeamId} bloque par la terre, arret");
				return;
			}
		}

		ProcessStuckDetection();
	}

	private void ProcessStuckDetection()
	{
		if (_moveStartDelay > 0)
		{
			_moveStartDelay--;
			_lastPosition = GlobalPosition;
			return;
		}

		float expectedMovement = _stats.Speed / 60f;
		float actualMovement = GlobalPosition.DistanceTo(_lastPosition);

		if (actualMovement < expectedMovement * 0.1f)
		{
			_stuckFrames++;
			if (_stuckFrames > MaxStuckFrames)
			{
				// Bloque - si debarquement en attente, decharger quand meme
				if (_pendingUnloadPosition.HasValue)
				{
					UnloadUnits(_pendingUnloadPosition.Value);
					_pendingUnloadPosition = null;
				}

				_targetPosition = null;
				ChangeState(ShipState.Idle);
			}
		}
		else
		{
			_stuckFrames = 0;
		}
		_lastPosition = GlobalPosition;
	}

	private Vector2 FindAlternativeWaterDirection(Vector2 desiredDirection, float delta)
	{
		// Essayer des angles alternatifs pour longer la cote
		float[] angles = { 30f, -30f, 60f, -60f, 90f, -90f };

		foreach (float angleDeg in angles)
		{
			float angleRad = Mathf.DegToRad(angleDeg);
			Vector2 rotated = desiredDirection.Rotated(angleRad);
			Vector2 altVelocity = rotated * _stats.Speed;
			Vector2 altNextPos = GlobalPosition + altVelocity * delta;

			if (IsWaterTile(altNextPos))
			{
				return altVelocity;
			}
		}

		return Vector2.Zero;
	}

	public bool IsWaterTile(Vector2 globalPos)
	{
		if (_tileMapSol == null) return false;

		Vector2I tileCoords = _tileMapSol.LocalToMap(_tileMapSol.ToLocal(globalPos));
		int tileId = _tileMapSol.GetCellSourceId(tileCoords);
		return tileId == 6; // IdEau
	}

	public void MoveTo(Vector2 target)
	{
		// Verifier que la destination est sur l'eau
		if (!IsWaterTile(target))
		{
			GD.Print($"[SHIP] Destination refusee: pas sur l'eau");
			return;
		}

		_pendingUnloadPosition = null; // Nouvel ordre annule le debarquement
		_targetPosition = target;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(ShipState.MovingToPoint);
	}

	public void Stop()
	{
		_targetPosition = null;
		_pendingUnloadPosition = null;
		Velocity = Vector2.Zero;
		ChangeState(ShipState.Idle);
	}
}
