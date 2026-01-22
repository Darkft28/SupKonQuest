using Godot;
using System.Collections.Generic;

namespace SupKonQuest
{
	public partial class SelectionManager : Node2D
	{
		private List<CampUnit> _selectedUnits = new List<CampUnit>();
		private Camera2D _camera;

		public override void _Ready()
		{
			_camera = GetParent().GetNodeOrNull<Camera2D>("Camera2D");
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			// Clic droit = ordre de déplacement
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
			{
				if (_selectedUnits.Count > 0)
				{
					Vector2 targetPos = GetGlobalMousePosition();
					foreach (var unit in _selectedUnits)
					{
						unit.MoveTo(targetPos);
					}
					GetViewport().SetInputAsHandled();
				}
			}

			// Clic gauche sur le vide = désélectionner
			if (@event is InputEventMouseButton mbLeft && mbLeft.Pressed && mbLeft.ButtonIndex == MouseButton.Left)
			{
				// On attend un frame pour voir si une unité a été cliquée
				CallDeferred(nameof(CheckDeselect));
			}
		}

		private bool _unitClickedThisFrame = false;

		public void OnUnitClicked(CampUnit unit)
		{
			_unitClickedThisFrame = true;

			// Si Shift est maintenu, ajouter à la sélection
			if (Input.IsKeyPressed(Key.Shift))
			{
				if (!_selectedUnits.Contains(unit))
				{
					_selectedUnits.Add(unit);
					unit.IsSelected = true;
				}
			}
			else
			{
				// Sinon, sélection unique
				DeselectAll();
				_selectedUnits.Add(unit);
				unit.IsSelected = true;
			}

			GD.Print($"Unité sélectionnée. Total: {_selectedUnits.Count}");
		}

		private void CheckDeselect()
		{
			if (!_unitClickedThisFrame)
			{
				DeselectAll();
			}
			_unitClickedThisFrame = false;
		}

		private void DeselectAll()
		{
			foreach (var unit in _selectedUnits)
			{
				unit.IsSelected = false;
			}
			_selectedUnits.Clear();
		}

		public List<CampUnit> GetSelectedUnits()
		{
			return _selectedUnits;
		}
	}
}
