using Godot;
using System.Collections.Generic;

public partial class SelectionManager : Node2D
{
	private Camera2D _camera;
	private List<Unit> _selectedUnits = new List<Unit>();
	private CampSimple _selectedCamp = null;
	private CampSimple _selectedPort = null;
	private List<Ship> _selectedShips = new List<Ship>();

	private Vector2 _selectionStart;
	private bool _isSelecting = false;
	private ColorRect _selectionRect;

	public override void _Ready()
	{
		_camera = GetParent().GetNodeOrNull<Camera2D>("Camera2D");

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
				HandleRightClick(GetGlobalMousePosition());
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

		foreach (var unit in _selectedUnits)
		{
			if (IsInstanceValid(unit))
			{
				unit.Modulate = Colors.White;
			}
		}
		_selectedUnits.Clear();

		foreach (var ship in _selectedShips)
		{
			if (IsInstanceValid(ship))
			{
				ship.Modulate = Colors.White;
			}
		}
		_selectedShips.Clear();

		DeselectCamp();
		DeselectPort();

		Rect2 selectionArea = new Rect2(
			Mathf.Min(_selectionStart.X, endPosition.X),
			Mathf.Min(_selectionStart.Y, endPosition.Y),
			Mathf.Abs(endPosition.X - _selectionStart.X),
			Mathf.Abs(endPosition.Y - _selectionStart.Y)
		);

		bool isClick = selectionArea.Size.Length() < 10;

		if (isClick)
		{
			var camps = GetTree().GetNodesInGroup("camps");
			foreach (var node in camps)
			{
				if (node is CampSimple camp && camp.HasPort)
				{
					Vector2 portPos = camp.GetPortGlobalPosition();
					if (portPos.DistanceTo(_selectionStart) < 100)
					{
						SelectPort(camp);
						return;
					}
				}
			}

			foreach (var node in camps)
			{
				if (node is CampSimple camp)
				{
					if (camp.GlobalPosition.DistanceTo(_selectionStart) < 200)
					{
						SelectCamp(camp);
						return;
					}
				}
			}
		}

		var ships = GetTree().GetNodesInGroup("ships");
		foreach (var node in ships)
		{
			if (node is Ship ship)
			{
				if (selectionArea.HasPoint(ship.GlobalPosition) || isClick)
				{
					if (isClick)
					{
						if (ship.GlobalPosition.DistanceTo(_selectionStart) < 80)
						{
							SelectShip(ship);
						}
					}
					else
					{
						SelectShip(ship);
					}
				}
			}
		}

		if (_selectedShips.Count > 0)
			return;

		var units = GetTree().GetNodesInGroup("units");
		foreach (var node in units)
		{
			if (node is Unit unit)
			{
				if (selectionArea.HasPoint(unit.GlobalPosition) || isClick)
				{
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

	}

	private void SelectCamp(CampSimple camp)
	{
		_selectedCamp = camp;
		_selectedCamp.Modulate = new Color(1.2f, 1.2f, 0.8f, 1);
	}

	private void DeselectCamp()
	{
		if (_selectedCamp != null && IsInstanceValid(_selectedCamp))
		{
			_selectedCamp.Modulate = Colors.White;
		}
		_selectedCamp = null;
	}

	private void SelectPort(CampSimple camp)
	{
		if (camp.GetTeamId() != GetLocalTeamId()) return;
		_selectedPort = camp;
		_selectedPort.Modulate = new Color(0.8f, 1.0f, 1.2f, 1);
	}

	private void DeselectPort()
	{
		if (_selectedPort != null && IsInstanceValid(_selectedPort))
		{
			_selectedPort.Modulate = Colors.White;
		}
		_selectedPort = null;
	}

	private int GetLocalTeamId()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		return gameState?.LocalTeamId ?? 1;
	}

	private void SelectUnit(Unit unit)
	{
		if (unit.GetTeamId() != GetLocalTeamId()) return;

		_selectedUnits.Add(unit);
		unit.Modulate = new Color(1, 1, 0.5f, 1);
	}

	private void SelectShip(Ship ship)
	{
		if (ship.GetTeamId() != GetLocalTeamId()) return;

		_selectedShips.Add(ship);
		ship.Modulate = new Color(0.5f, 1, 1, 1);
	}

	private void HandleRightClick(Vector2 target)
	{
		if (_selectedUnits.Count > 0)
		{
			var ships = GetTree().GetNodesInGroup("ships");
			foreach (var node in ships)
			{
				if (node is Ship ship && ship.GetShipType() == "Transport")
				{
					if (ship.GlobalPosition.DistanceTo(target) < 150)
					{
						int unitTeam = _selectedUnits[0].GetTeamId();
						if (ship.GetTeamId() == unitTeam)
						{
							SendUnitsToTransport(ship);
							return;
						}
					}
				}
			}

			int localTeamId = _selectedUnits[0].GetTeamId();
			CampSimple targetCamp = null;
			float closestCampDist = 400f;
			var allCamps = GetTree().GetNodesInGroup("camps");
			foreach (var node in allCamps)
			{
				if (node is CampSimple camp && camp.GetTeamId() != localTeamId)
				{
					float dist = target.DistanceTo(camp.GlobalPosition);
					if (dist < closestCampDist)
					{
						closestCampDist = dist;
						targetCamp = camp;
					}
				}
			}

			if (targetCamp != null)
			{
				foreach (var unit in _selectedUnits)
					if (IsInstanceValid(unit))
						unit.AttackCamp(targetCamp);
				return;
			}

			MoveSelectedUnits(target);
			return;
		}

		if (_selectedShips.Count > 0)
		{
			bool hasTransportWithUnits = false;
			foreach (var ship in _selectedShips)
			{
				if (IsInstanceValid(ship) && ship.GetShipType() == "Transport" && ship.GetLoadedUnitCount() > 0)
				{
					hasTransportWithUnits = true;
					break;
				}
			}

			if (hasTransportWithUnits)
			{
				bool isWater = false;
				foreach (var ship in _selectedShips)
				{
					if (IsInstanceValid(ship))
					{
						isWater = ship.IsWaterTile(target);
						break;
					}
				}

				if (!isWater)
				{
					foreach (var ship in _selectedShips)
					{
						if (IsInstanceValid(ship) && ship.GetShipType() == "Transport" && ship.GetLoadedUnitCount() > 0)
						{
							if (ship.IsValidUnloadPosition(target))
							{
								ship.MoveToUnload(target);
							}
						}
					}
					return;
				}
			}

				MoveSelectedShips(target);
			return;
		}
	}

	private void SendUnitsToTransport(Ship transport)
	{
		int capacity = transport.GetCapacity() - transport.GetLoadedUnitCount();
		int sent = 0;

		foreach (var unit in _selectedUnits)
		{
			if (IsInstanceValid(unit) && sent < capacity)
			{
				unit.MoveToTransport(transport);
				sent++;
			}
		}


		foreach (var unit in _selectedUnits)
		{
			if (IsInstanceValid(unit))
				unit.Modulate = Colors.White;
		}
		_selectedUnits.Clear();
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

	private void MoveSelectedShips(Vector2 target)
	{
		foreach (var ship in _selectedShips)
		{
			if (IsInstanceValid(ship))
			{
				ship.MoveTo(target);
			}
		}
	}

	public void OnUnitClicked(Unit unit)
	{
		foreach (var u in _selectedUnits)
		{
			if (IsInstanceValid(u))
			{
				u.Modulate = Colors.White;
			}
		}
		_selectedUnits.Clear();

		foreach (var s in _selectedShips)
		{
			if (IsInstanceValid(s))
			{
				s.Modulate = Colors.White;
			}
		}
		_selectedShips.Clear();

		DeselectCamp();
		DeselectPort();

		SelectUnit(unit);
	}

	public CampSimple GetSelectedCamp()
	{
		return _selectedCamp;
	}

	public CampSimple GetSelectedPort()
	{
		return _selectedPort;
	}

	public List<Unit> GetSelectedUnits()
	{
		return _selectedUnits;
	}

	public List<Ship> GetSelectedShips()
	{
		return _selectedShips;
	}
}
