using Godot;

public partial class Unit
{
	public void MoveToTransport(Ship transport)
	{
		_targetTransport = transport;
		_targetPosition = transport.GlobalPosition;
		_savedTargetPosition = null;
		_stuckFrames = 0;
		_moveStartDelay = MoveStartDelayFrames;
		_lastPosition = GlobalPosition;
		ChangeState(UnitState.MovingToTransport);
		GD.Print($"[UNIT] {UnitType} T{TeamId} se dirige vers le Transport");
	}

	private void ProcessMovingToTransportState(double delta)
	{
		// Verifier que le transport est toujours valide
		if (_targetTransport == null || !IsInstanceValid(_targetTransport) || !_targetTransport.IsInsideTree())
		{
			_targetTransport = null;
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToTransport = GlobalPosition.DistanceTo(_targetTransport.GlobalPosition);

		// Si assez proche, embarquer
		if (distanceToTransport < BoardingDistance)
		{
			if (_targetTransport.BoardUnit(this))
			{
				// BoardUnit appelle QueueFree, plus rien a faire
				return;
			}
			else
			{
				GD.Print($"[UNIT] {UnitType} T{TeamId} ne peut pas embarquer, transport plein");
				_targetTransport = null;
				ChangeState(UnitState.Idle);
				return;
			}
		}

		// Se deplacer vers le transport (suivre sa position)
		Vector2 direction = (_targetTransport.GlobalPosition - GlobalPosition).Normalized();
		Velocity = direction * _stats.Speed;
		MoveAndSlide();

		// Detection de blocage
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
			// Bloque (probablement sur la cote) mais assez proche -> embarquer quand meme
			if (_stuckFrames > 60 && distanceToTransport < BoardingDistance * 1.6f)
			{
				if (_targetTransport.BoardUnit(this))
					return;
			}
			if (_stuckFrames > MaxStuckFrames)
			{
				GD.Print($"[UNIT] {UnitType} T{TeamId} bloque, abandon embarquement");
				_targetTransport = null;
				ChangeState(UnitState.Idle);
			}
		}
		else
		{
			_stuckFrames = 0;
		}
		_lastPosition = GlobalPosition;
	}
}
