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
	}

	private void ProcessMovingToTransportState(double delta)
	{
		if (_targetTransport == null || !IsInstanceValid(_targetTransport) || !_targetTransport.IsInsideTree())
		{
			_targetTransport = null;
			ChangeState(UnitState.Idle);
			return;
		}

		float distanceToTransport = GlobalPosition.DistanceTo(_targetTransport.GlobalPosition);

		if (distanceToTransport < BoardingDistance)
		{
			if (_targetTransport.BoardUnit(this))
			{
				// BoardUnit appelle QueueFree
				return;
			}
			else
			{
				_targetTransport = null;
				ChangeState(UnitState.Idle);
				return;
			}
		}

		Vector2 direction = (_targetTransport.GlobalPosition - GlobalPosition).Normalized();
		Velocity = direction * _stats.Speed;
		MoveAndSlide();

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
			// Bloqué sur la côte mais assez proche → embarquer quand même
			if (_stuckFrames > 60 && distanceToTransport < BoardingDistance * 1.6f)
			{
				if (_targetTransport.BoardUnit(this))
					return;
			}
			if (_stuckFrames > MaxStuckFrames)
			{
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
