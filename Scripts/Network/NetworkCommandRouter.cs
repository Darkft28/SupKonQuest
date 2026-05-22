using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public static class NetworkCommandRouter
{
	public const long OpcodeBuyUnit = 1001;
	public const long OpcodeBuyShip = 1002;
	public const long OpcodeMoveUnits = 2001;
	public const long OpcodeAttackCamp = 2002;
	public const long OpcodeCampCaptured = 2003;
	public const long OpcodeMoveShips = 2004;
	public const long OpcodeGoldSnapshot = 3001;
	public const long OpcodeLobbyTick = 4001;
	public const long OpcodeMatchStart = 4002;
	private static readonly JsonSerializerOptions RelayJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private static readonly JsonSerializerOptions RelayJsonReadOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private static int _sequence;

	[Serializable]
	private abstract class RelayCommandBase
	{
		public string SenderUserId { get; set; } = "";
		public int Sequence { get; set; }
	}

	[Serializable]
	private sealed class BuyUnitCommand : RelayCommandBase
	{
		public int CampId { get; set; }
		public int TeamId { get; set; }
		public string UnitType { get; set; } = "";
	}

	[Serializable]
	private sealed class BuyShipCommand : RelayCommandBase
	{
		public int CampId { get; set; }
		public int TeamId { get; set; }
		public string ShipType { get; set; } = "";
	}

	[Serializable]
	private sealed class MoveUnitsCommand : RelayCommandBase
	{
		public string[] UnitIds { get; set; } = Array.Empty<string>();
		public float StartX { get; set; }
		public float StartY { get; set; }
		public float TargetX { get; set; }
		public float TargetY { get; set; }
	}

	[Serializable]
	private sealed class MoveShipsCommand : RelayCommandBase
	{
		public string[] ShipIds { get; set; } = Array.Empty<string>();
		public float TargetX { get; set; }
		public float TargetY { get; set; }
	}

	[Serializable]
	private sealed class AttackCampCommand : RelayCommandBase
	{
		public string[] UnitIds { get; set; } = Array.Empty<string>();
		public int CampId { get; set; }
	}

	[Serializable]
	private sealed class CampCapturedCommand : RelayCommandBase
	{
		public int CampId { get; set; }
		public int NewTeamId { get; set; }
	}

	public static void RequestBuyUnit(CampSimple camp, string unitType)
	{
		if (camp == null || !GodotObject.IsInstanceValid(camp))
			return;

		if (!camp.BuyUnit(unitType))
			return;

		SendRelayAsync(OpcodeBuyUnit, new BuyUnitCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			CampId = camp.GetCampId(),
			TeamId = camp.GetTeamId(),
			UnitType = unitType
		});
	}

	public static void RequestBuyShip(CampSimple camp, string shipType)
	{
		if (camp == null || !GodotObject.IsInstanceValid(camp))
			return;

		if (!camp.BuyShip(shipType))
			return;

		SendRelayAsync(OpcodeBuyShip, new BuyShipCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			CampId = camp.GetCampId(),
			TeamId = camp.GetTeamId(),
			ShipType = shipType
		});
	}

	public static void RequestMoveUnits(IEnumerable<Unit> units, Vector2 target)
	{
		var validUnits = units?.Where(unit => unit != null && GodotObject.IsInstanceValid(unit)).ToList() ?? new List<Unit>();
		if (validUnits.Count == 0)
			return;

		foreach (var unit in validUnits)
			unit.MoveTo(target);

		SendRelayAsync(OpcodeMoveUnits, new MoveUnitsCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			UnitIds = validUnits.Where(unit => !string.IsNullOrWhiteSpace(unit.NetworkId)).Select(unit => unit.NetworkId).ToArray(),
			StartX = validUnits[0].GlobalPosition.X,
			StartY = validUnits[0].GlobalPosition.Y,
			TargetX = target.X,
			TargetY = target.Y
		});
	}

	public static void RequestMoveShips(IEnumerable<Ship> ships, Vector2 target)
	{
		var validShips = ships?.Where(ship => ship != null && GodotObject.IsInstanceValid(ship)).ToList() ?? new List<Ship>();
		if (validShips.Count == 0)
			return;

		foreach (var ship in validShips)
			ship.MoveTo(target);

		SendRelayAsync(OpcodeMoveShips, new MoveShipsCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			ShipIds = validShips.Where(ship => !string.IsNullOrWhiteSpace(ship.NetworkId)).Select(ship => ship.NetworkId).ToArray(),
			TargetX = target.X,
			TargetY = target.Y
		});
	}

	public static void RequestAttackCamp(IEnumerable<Unit> units, CampSimple camp)
	{
		var validUnits = units?.Where(unit => unit != null && GodotObject.IsInstanceValid(unit)).ToList() ?? new List<Unit>();
		if (camp == null || !GodotObject.IsInstanceValid(camp) || validUnits.Count == 0)
			return;

		foreach (var unit in validUnits)
			unit.AttackCamp(camp);

		SendRelayAsync(OpcodeAttackCamp, new AttackCampCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			UnitIds = validUnits.Where(unit => !string.IsNullOrWhiteSpace(unit.NetworkId)).Select(unit => unit.NetworkId).ToArray(),
			CampId = camp.GetCampId()
		});
	}

	public static void SendCampCaptured(int campId, int newTeamId)
	{
		if (campId <= 0 || newTeamId <= 0)
			return;

		SendRelayAsync(OpcodeCampCaptured, new CampCapturedCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			CampId = campId,
			NewTeamId = newTeamId
		});
	}

	public static void HandleIncomingRelayCommand(long opcode, string payload)
	{
		string localUserId = NakamaService.Instance?.UserId ?? "";
		GD.Print($"[RELAY] Received opcode={opcode} localUserId={localUserId} payloadBytes={payload?.Length ?? 0}");

		switch (opcode)
		{
			case OpcodeBuyUnit:
			{
				var command = JsonSerializer.Deserialize<BuyUnitCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] BuyUnit deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
				{
					GD.Print($"[RELAY] BuyUnit skipped (self message) sender={command.SenderUserId}");
					return;
				}

				GD.Print($"[RELAY] BuyUnit apply sender={command.SenderUserId} campId={command.CampId} unitType={command.UnitType}");
				ApplyBuyUnit(command);
				break;
			}
			case OpcodeBuyShip:
			{
				var command = JsonSerializer.Deserialize<BuyShipCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] BuyShip deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
				{
					GD.Print($"[RELAY] BuyShip skipped (self message) sender={command.SenderUserId}");
					return;
				}

				GD.Print($"[RELAY] BuyShip apply sender={command.SenderUserId} campId={command.CampId} shipType={command.ShipType}");
				ApplyBuyShip(command);
				break;
			}
			case OpcodeMoveUnits:
			{
				var command = JsonSerializer.Deserialize<MoveUnitsCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] MoveUnits deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
				{
					GD.Print($"[RELAY] MoveUnits skipped (self message) sender={command.SenderUserId}");
					return;
				}

				GD.Print($"[RELAY] MoveUnits apply sender={command.SenderUserId} unitCount={command.UnitIds.Length}");
				ApplyMoveUnits(command);
				break;
			}
			case OpcodeMoveShips:
			{
				var command = JsonSerializer.Deserialize<MoveShipsCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] MoveShips deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
				{
					GD.Print($"[RELAY] MoveShips skipped (self message) sender={command.SenderUserId}");
					return;
				}

				GD.Print($"[RELAY] MoveShips apply sender={command.SenderUserId} shipCount={command.ShipIds.Length}");
				ApplyMoveShips(command);
				break;
			}
			case OpcodeAttackCamp:
			{
				var command = JsonSerializer.Deserialize<AttackCampCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] AttackCamp deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
				{
					GD.Print($"[RELAY] AttackCamp skipped (self message) sender={command.SenderUserId}");
					return;
				}

				GD.Print($"[RELAY] AttackCamp apply sender={command.SenderUserId} campId={command.CampId} unitCount={command.UnitIds.Length}");
				ApplyAttackCamp(command);
				break;
			}
			case OpcodeCampCaptured:
			{
				var command = JsonSerializer.Deserialize<CampCapturedCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] CampCaptured deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
				{
					GD.Print($"[RELAY] CampCaptured skipped (self message) sender={command.SenderUserId}");
					return;
				}

				GD.Print($"[RELAY] CampCaptured apply sender={command.SenderUserId} campId={command.CampId} newTeam={command.NewTeamId}");
				ApplyCampCaptured(command);
				break;
			}
			default:
				GD.Print($"[RELAY] Unsupported opcode={opcode}");
				break;
		}
	}

	private static async void SendRelayAsync<T>(long opcode, T command)
	{
		if (NakamaService.Instance == null || !NakamaService.Instance.IsSocketConnected || string.IsNullOrEmpty(NakamaService.Instance.MatchId))
			return;

		await NakamaService.Instance.SendMatchCommandAsync(opcode, command, RelayJsonOptions);
	}

	private static void ApplyBuyUnit(BuyUnitCommand command)
	{
		var camps = GetTree()?.GetNodesInGroup("camps");
		if (camps == null) return;

		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetCampId() == command.CampId)
			{
				camp.ApplyRelayBuyUnit(command.UnitType);
				break;
			}
		}
	}

	private static void ApplyBuyShip(BuyShipCommand command)
	{
		var camps = GetTree()?.GetNodesInGroup("camps");
		if (camps == null) return;

		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetCampId() == command.CampId)
			{
				camp.ApplyRelayBuyShip(command.ShipType);
				break;
			}
		}
	}

	private static void ApplyMoveUnits(MoveUnitsCommand command)
	{
		Vector2 target = new(command.TargetX, command.TargetY);
		foreach (string unitId in command.UnitIds)
		{
			var unit = NetworkEntityRegistry.Get<Unit>(unitId);
			if (unit != null && GodotObject.IsInstanceValid(unit))
				unit.MoveTo(target);
		}
	}

	private static void ApplyMoveShips(MoveShipsCommand command)
	{
		Vector2 target = new(command.TargetX, command.TargetY);
		foreach (string shipId in command.ShipIds)
		{
			var ship = NetworkEntityRegistry.Get<Ship>(shipId);
			if (ship != null && GodotObject.IsInstanceValid(ship))
				ship.MoveTo(target);
		}
	}

	private static void ApplyAttackCamp(AttackCampCommand command)
	{
		CampSimple camp = null;
		var camps = GetTree()?.GetNodesInGroup("camps");
		if (camps == null) return;

		foreach (var node in camps)
		{
			if (node is CampSimple candidate && candidate.GetCampId() == command.CampId)
			{
				camp = candidate;
				break;
			}
		}

		if (camp == null)
			return;

		foreach (string unitId in command.UnitIds)
		{
			var unit = NetworkEntityRegistry.Get<Unit>(unitId);
			if (unit != null && GodotObject.IsInstanceValid(unit))
				unit.AttackCamp(camp);
		}
	}

	private static void ApplyCampCaptured(CampCapturedCommand command)
	{
		var camps = GetTree()?.GetNodesInGroup("camps");
		if (camps == null) return;

		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetCampId() == command.CampId)
			{
				camp.ApplyRemoteCapture(command.NewTeamId);
				break;
			}
		}
	}

	private static SceneTree GetTree()
	{
		return Engine.GetMainLoop() as SceneTree;
	}
}

