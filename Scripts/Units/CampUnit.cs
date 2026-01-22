using Godot;

namespace SupKonQuest
{
	public partial class CampUnit : Area2D
	{
		[Export] public float MoveSpeed = 200.0f;
		[Export] public Color SelectedColor = new Color(1, 1, 0, 0.5f);
		[Export] public Color NormalColor = new Color(1, 1, 1, 1);

		private Sprite2D _sprite;
		private CollisionShape2D _collision;
		private Vector2 _targetPosition;
		private bool _isMoving = false;
		private bool _isSelected = false;

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				_isSelected = value;
				UpdateVisual();
			}
		}

		public override void _Ready()
		{
			_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			_collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			_targetPosition = GlobalPosition;

			// Connecter le signal de clic
			InputEvent += OnInputEvent;
		}

		public override void _Process(double delta)
		{
			if (_isMoving)
			{
				Vector2 direction = (_targetPosition - GlobalPosition).Normalized();
				float distance = GlobalPosition.DistanceTo(_targetPosition);

				if (distance > 5.0f)
				{
					GlobalPosition += direction * MoveSpeed * (float)delta;
				}
				else
				{
					GlobalPosition = _targetPosition;
					_isMoving = false;
				}
			}
		}

		public void MoveTo(Vector2 target)
		{
			_targetPosition = target;
			_isMoving = true;
		}

		private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				// Notifier le SelectionManager qu'on a été cliqué
				// Chercher dans le parent (Units -> Node -> SelectionManager)
				var selectionManager = GetTree().Root.GetNodeOrNull<SelectionManager>("Node/SelectionManager");
				selectionManager?.OnUnitClicked(this);
			}
		}

		private void UpdateVisual()
		{
			if (_sprite != null)
			{
				_sprite.Modulate = _isSelected ? SelectedColor : NormalColor;
			}
		}

		// Pour créer l'unité dynamiquement avec un sprite
		public static CampUnit CreateInstance(Texture2D texture, Vector2 position)
		{
			var unit = new CampUnit();
			unit.GlobalPosition = position;

			// Créer le sprite
			var sprite = new Sprite2D();
			sprite.Texture = texture;
			sprite.Name = "Sprite2D";
			unit.AddChild(sprite);

			// Créer la collision (cercle)
			var collision = new CollisionShape2D();
			var shape = new CircleShape2D();
			shape.Radius = 48.0f;
			collision.Shape = shape;
			collision.Name = "CollisionShape2D";
			unit.AddChild(collision);

			// Activer la détection d'input
			unit.InputPickable = true;

			return unit;
		}
	}
}
