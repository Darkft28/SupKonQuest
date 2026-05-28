using Godot;

public partial class ShipProjectile : Node2D
{
	public enum ShipProjectileType
	{
		Arrow,
		Cannonball
	}

	private const float SplashRadius = 200f;
	private const float SplashDamage = 20f;

	private Vector2 _startPos;
	private Vector2 _targetPos;
	private float _speed;
	private float _progress = 0f;
	private float _arcHeight;
	private Sprite2D _sprite;
	private ShipProjectileType _type;
	private bool _applySplash;

	private Ship _targetShip;
	private float _damage;
	private int _attackerTeamId;

	public void Initialize(Vector2 start, Ship target, float damage, int attackerTeamId,
		ShipProjectileType type, float speed, bool applySplash)
	{
		_startPos = start;
		_targetShip = target;
		_targetPos = target.GlobalPosition;
		_damage = damage;
		_attackerTeamId = attackerTeamId;
		_type = type;
		_speed = speed;
		_applySplash = applySplash;
		GlobalPosition = start;

		float distance = start.DistanceTo(_targetPos);
		_arcHeight = type == ShipProjectileType.Cannonball ? distance * 0.4f : distance * 0.3f;

		CreateSprite();
	}

	private void CreateSprite()
	{
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite == null)
		{
			_sprite = new Sprite2D();
			_sprite.Name = "Sprite2D";
			AddChild(_sprite);
		}

		string texturePath;
		if (_type == ShipProjectileType.Cannonball)
		{
			texturePath = "res://Assets/Units/Characters/Mortar/Mortar_Ammo.png";
			_sprite.Scale = new Vector2(0.15f, 0.15f);
		}
		else
		{
			bool goingRight = _targetPos.X >= _startPos.X;
			texturePath = goingRight
				? "res://Assets/Units/Characters/Range/Ammo_Range_Right.png"
				: "res://Assets/Units/Characters/Range/Ammo_Range_Left.png";
			_sprite.Scale = new Vector2(0.15f, 0.15f);
		}

		var texture = GD.Load<Texture2D>(texturePath);
		if (texture != null)
			_sprite.Texture = texture;
	}

	public override void _Process(double delta)
	{
		if (_progress >= 1f) return;

		if (_targetShip != null && IsInstanceValid(_targetShip) && _targetShip.IsInsideTree())
			_targetPos = _targetShip.GlobalPosition;

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
			ApplyShipDamage(_targetShip, _damage);
		}

		if (_applySplash)
			ApplyDestroyerSplash();

		QueueFree();
	}

	private void ApplyDestroyerSplash()
	{
		var allShips = GetTree().GetNodesInGroup("ships");
		foreach (var node in allShips)
		{
			if (node is not Ship ship) continue;
			if (ship == _targetShip) continue;
			if (ship.GetTeamId() == _attackerTeamId) continue;
			if (ship.GetCurrentHealth() <= 0) continue;
			if (ship.GetShipType() == "Transport") continue;

			float dist = _targetPos.DistanceTo(ship.GlobalPosition);
			if (dist > SplashRadius) continue;

			ApplyShipDamage(ship, SplashDamage);
		}
	}

	private void ApplyShipDamage(Ship ship, float damage)
	{
		bool isMulti = NetworkSync.Instance?.IsMultiplayer() == true;
		if (isMulti && !string.IsNullOrEmpty(ship.NetworkId))
			NetworkSync.Instance?.SendShipDamage(ship.NetworkId, damage, _attackerTeamId);
		else
			ship.TakeDamageFrom(damage, _attackerTeamId);
	}
}
