using Godot;
using System.Collections.Generic;

public partial class SelectionManager : Node2D
{
	private Camera2D _camera;
	private List<Unit> _selectedUnits = new List<Unit>();
	private CampSimple _selectedCamp = null;

	// Rectangle de sélection
	private Vector2 _selectionStart;
	private bool _isSelecting = false;
	private ColorRect _selectionRect;

	public override void _Ready()
	{
		_camera = GetParent().GetNodeOrNull<Camera2D>("Camera2D");

		// Créer le rectangle de sélection visuel
		_selectionRect = new ColorRect();
		_selectionRect.Color = new Color(0.2f, 0.5f, 1.0f, 0.3f);
		_selectionRect.Visible = false;
		AddChild(_selectionRect);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					StartSelection(GetGlobalMousePosition());
				}
				else
				{
					EndSelection(GetGlobalMousePosition());
				}
			}
			else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
			{
				// Clic droit = déplacer les unités sélectionnées
				MoveSelectedUnits(GetGlobalMousePosition());
			}
		}

		if (@event is InputEventMouseMotion && _isSelecting)
		{
			UpdateSelectionRect(GetGlobalMousePosition());
		}
	}

	private void StartSelection(Vector2 position)
	{
		_selectionStart = position;
		_isSelecting = true;
		_selectionRect.Visible = true;
		_selectionRect.Position = position;
		_selectionRect.Size = Vector2.Zero;
	}

	private void UpdateSelectionRect(Vector2 currentPos)
	{
		Vector2 start = _selectionStart;
		Vector2 size = currentPos - start;

		// Gérer les sélections dans toutes les directions
		if (size.X < 0)
		{
			start.X = currentPos.X;
			size.X = -size.X;
		}
		if (size.Y < 0)
		{
			start.Y = currentPos.Y;
			size.Y = -size.Y;
		}

		_selectionRect.Position = start;
		_selectionRect.Size = size;
	}

	private void EndSelection(Vector2 endPosition)
	{
		_isSelecting = false;
		_selectionRect.Visible = false;

		// Désélectionner les unités précédentes
		foreach (var unit in _selectedUnits)
		{
			if (IsInstanceValid(unit))
			{
				unit.Modulate = Colors.White;
			}
		}
		_selectedUnits.Clear();

		// Désélectionner le camp précédent
		DeselectCamp();

		// Calculer le rectangle de sélection
		Rect2 selectionArea = new Rect2(
			Mathf.Min(_selectionStart.X, endPosition.X),
			Mathf.Min(_selectionStart.Y, endPosition.Y),
			Mathf.Abs(endPosition.X - _selectionStart.X),
			Mathf.Abs(endPosition.Y - _selectionStart.Y)
		);

		bool isClick = selectionArea.Size.Length() < 10;

		// Si c'est un clic, vérifier d'abord les camps
		if (isClick)
		{
			var camps = GetTree().GetNodesInGroup("camps");
			foreach (var node in camps)
			{
				if (node is CampSimple camp)
				{
					// Distance de détection basée sur la taille du camp (environ 200 pixels avec scale 3)
					if (camp.GlobalPosition.DistanceTo(_selectionStart) < 200)
					{
						SelectCamp(camp);
						return;
					}
				}
			}
		}

		// Sélectionner les unités dans le rectangle
		var units = GetTree().GetNodesInGroup("units");
		foreach (var node in units)
		{
			if (node is Unit unit)
			{
				if (selectionArea.HasPoint(unit.GlobalPosition) || isClick)
				{
					// Si c'est un petit clic, vérifier si on a cliqué sur l'unité
					if (isClick)
					{
						if (unit.GlobalPosition.DistanceTo(_selectionStart) < 64)
						{
							SelectUnit(unit);
						}
					}
					else
					{
						SelectUnit(unit);
					}
				}
			}
		}

		GD.Print($"{_selectedUnits.Count} unites selectionnees");
	}

	private void SelectCamp(CampSimple camp)
	{
		_selectedCamp = camp;
		_selectedCamp.Modulate = new Color(1.2f, 1.2f, 0.8f, 1);
		GD.Print($"Camp #{camp.CampId} selectionne (Equipe {camp.TeamId})");
	}

	private void DeselectCamp()
	{
		if (_selectedCamp != null && IsInstanceValid(_selectedCamp))
		{
			_selectedCamp.Modulate = Colors.White;
		}
		_selectedCamp = null;
	}

	private void SelectUnit(Unit unit)
	{
		_selectedUnits.Add(unit);
		unit.Modulate = new Color(1, 1, 0.5f, 1); // Jaune pour montrer la sélection
	}

	private void MoveSelectedUnits(Vector2 target)
	{
		foreach (var unit in _selectedUnits)
		{
			if (IsInstanceValid(unit))
			{
				unit.MoveTo(target);
			}
		}
	}

	public void OnUnitClicked(Unit unit)
	{
		// Désélectionner tout
		foreach (var u in _selectedUnits)
		{
			if (IsInstanceValid(u))
			{
				u.Modulate = Colors.White;
			}
		}
		_selectedUnits.Clear();
		DeselectCamp();

		// Sélectionner l'unité cliquée
		SelectUnit(unit);
	}

	public CampSimple GetSelectedCamp()
	{
		return _selectedCamp;
	}

	public List<Unit> GetSelectedUnits()
	{
		return _selectedUnits;
	}
}
