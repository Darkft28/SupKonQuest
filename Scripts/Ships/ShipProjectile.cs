using Godot;

// Projectile specifique aux bateaux (cible un Ship au lieu d'un Unit)
public partial class ShipProjectile : Node2D
{
	private Vector2 _startPos;
	private Vector2 _targetPos;
	private float _speed;
	private float _progress = 0f;
	private float _arcHeight;
	private Sprite2D _sprite;

	private Ship _targetShip;
	private float _damage;
	private int _attackerTeamId;

	public void Initialize(Vector2 start, Ship target, float damage, int attackerTeamId, float speed)
	{
		_startPos = start;
		_targetShip = target;
		_targetPos = target.GlobalPosition;
		_damage = damage;
		_attackerTeamId = attackerTeamId;
		_speed = speed;
		GlobalPosition = start;

		float distance = start.DistanceTo(_targetPos);
		_arcHeight = distance * 0.3f;

		CreateSprite();
	}

	private void CreateSprite()
	{
		_sprite = new Sprite2D();
		string texturePath = "res://Assets/Units/Characters/Mortar/Mortar_Ammo.png";
		_sprite.Scale = new Vector2(0.2f, 0.2f);

		var texture = GD.Load<Texture2D>(texturePath);
		if (texture != null)
		{
			_sprite.Texture = texture;
		}
		AddChild(_sprite);
	}

	public override void _Process(double delta)
	{
		if (_progress >= 1f) return;

		if (_targetShip != null && IsInstanceValid(_targetShip) && _targetShip.IsInsideTree())
		{
			_targetPos = _targetShip.GlobalPosition;
		}

		float distance = _startPos.DistanceTo(_targetPos);
		if (distance < 1f)
		{
			ApplyDamageAndDestroy();
			return;
		}

		_progress += (float)delta * _speed / distance;

		if (_progress >= 1f)
		{
			_progress = 1f;
			ApplyDamageAndDestroy();
			return;
		}

		Vector2 linearPos = _startPos.Lerp(_targetPos, _progress);
		float arc = -4f * _arcHeight * _progress * (_progress - 1f);
		linearPos.Y -= arc;

		GlobalPosition = linearPos;
	}

	private void ApplyDamageAndDestroy()
	{
		if (_targetShip != null && IsInstanceValid(_targetShip) && _targetShip.IsInsideTree()
			&& _targetShip.GetCurrentHealth() > 0)
		{
			// Reseau : si la cible est un puppet, envoyer via RPC
			if (!_targetShip.IsLocalAuthority && !string.IsNullOrEmpty(_targetShip.NetworkId))
			{
				NetworkSync.Instance?.SendShipDamage(_targetShip.NetworkId, _damage, _attackerTeamId);
			}
			else
			{
				_targetShip.TakeDamageFrom(_damage, _attackerTeamId);
			}
		}
		QueueFree();
	}
}
