using Godot;

public partial class Projectile : Node2D
{
	public enum ProjectileType
	{
		Arrow,
		Cannonball
	}

	private Vector2 _startPos;
	private Vector2 _targetPos;
	private float _speed;
	private float _progress = 0f;
	private ProjectileType _type;
	private float _arcHeight;
	private Sprite2D _sprite;

	// Donnees de degats (appliques a l'arrivee)
	private Unit _targetUnit;
	private float _damage;
	private int _attackerTeamId;

	public void Initialize(Vector2 start, Unit target, float damage, int attackerTeamId, ProjectileType type, float speed)
	{
		_startPos = start;
		_targetUnit = target;
		_targetPos = target.GlobalPosition;
		_damage = damage;
		_attackerTeamId = attackerTeamId;
		_type = type;
		_speed = speed;
		GlobalPosition = start;

		float distance = start.DistanceTo(_targetPos);
		_arcHeight = type == ProjectileType.Cannonball ? distance * 0.4f : 0f;

		CreateSprite();
	}

	private void CreateSprite()
	{
		_sprite = new Sprite2D();

		string texturePath;
		if (_type == ProjectileType.Cannonball)
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
		{
			_sprite.Texture = texture;
		}

		AddChild(_sprite);
	}

	public override void _Process(double delta)
	{
		if (_progress >= 1f)
			return;

		if (_targetUnit != null && IsInstanceValid(_targetUnit) && _targetUnit.IsInsideTree())
		{
			_targetPos = _targetUnit.GlobalPosition;
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

		// Arc parabolique (Cannonball uniquement)
		if (_type == ProjectileType.Cannonball)
		{
			float arc = -4f * _arcHeight * _progress * (_progress - 1f);
			linearPos.Y -= arc;
		}

		GlobalPosition = linearPos;

		if (_type == ProjectileType.Arrow && _sprite != null)
		{
			Vector2 direction = (_targetPos - GlobalPosition).Normalized();
			_sprite.Rotation = direction.Angle();
		}
	}

	private void ApplyDamageAndDestroy()
	{
		if (_targetUnit != null && IsInstanceValid(_targetUnit) && _targetUnit.IsInsideTree()
			&& _targetUnit.GetCurrentHealth() > 0)
		{
			// Réseau : si la cible est un puppet, envoyer via RPC
			bool isMulti = NetworkSync.Instance?.IsMultiplayer() == true;
			if (isMulti && !_targetUnit.IsLocalAuthority && !string.IsNullOrEmpty(_targetUnit.NetworkId))
			{
				NetworkSync.Instance?.SendUnitDamage(_targetUnit.NetworkId, _damage, _attackerTeamId);
			}
			else
			{
				_targetUnit.TakeDamageFrom(_damage, _attackerTeamId);
			}
		}

		if (_type == ProjectileType.Cannonball)
			ApplyMortarSplash();

		QueueFree();
	}

	private void ApplyMortarSplash()
	{
		const float SplashRadius = 200f;
		const float SplashDamage = 20f;

		var allUnits = GetTree().GetNodesInGroup("units");
		foreach (var node in allUnits)
		{
			if (node is not Unit unit) continue;
			if (unit == _targetUnit) continue;
			if (unit.GetTeamId() == _attackerTeamId) continue;
			if (unit.GetCurrentHealth() <= 0) continue;

			float dist = _targetPos.DistanceTo(unit.GlobalPosition);
			if (dist > SplashRadius) continue;

			bool isMultiSplash = NetworkSync.Instance?.IsMultiplayer() == true;
			if (isMultiSplash && !unit.IsLocalAuthority && !string.IsNullOrEmpty(unit.NetworkId))
				NetworkSync.Instance?.SendUnitDamage(unit.NetworkId, SplashDamage, _attackerTeamId);
			else
				unit.TakeDamageFrom(SplashDamage, _attackerTeamId);
		}
	}
}
