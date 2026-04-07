using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public static class NetworkCommandRouter
{
	public const long OpcodeBuyUnit = 1001;
	public const long OpcodeMoveUnits = 2001;
	public const long OpcodeAttackCamp = 2002;
	public const long OpcodeGoldSnapshot = 3001;
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
	private sealed class MoveUnitsCommand : RelayCommandBase
	{
		public string[] UnitIds { get; set; } = Array.Empty<string>();
		public float StartX { get; set; }
		public float StartY { get; set; }
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
	private sealed class GoldSnapshotCommand : RelayCommandBase
	{
		public int TeamId { get; set; }
		public int Gold { get; set; }
		public int Version { get; set; }
		public string Reason { get; set; } = "";
	}

	public static void RequestBuyUnit(CampSimple camp, string unitType)
	{
		if (camp == null || !GodotObject.IsInstanceValid(camp))
			return;

		camp.BuyUnit(unitType);
		SendRelayAsync(OpcodeBuyUnit, new BuyUnitCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			CampId = camp.GetCampId(),
			TeamId = camp.GetTeamId(),
			UnitType = unitType
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

	public static void SendGoldSnapshot(int teamId, int gold, int version, string reason)
	{
		if (teamId <= 0)
			return;

		SendRelayAsync(OpcodeGoldSnapshot, new GoldSnapshotCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			TeamId = teamId,
			Gold = gold,
			Version = version,
			Reason = reason ?? ""
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
			case OpcodeGoldSnapshot:
			{
				var command = JsonSerializer.Deserialize<GoldSnapshotCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] GoldSnapshot deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
				{
					GD.Print($"[RELAY] GoldSnapshot skipped (self message) sender={command.SenderUserId}");
					return;
				}

				GD.Print($"[RELAY] GoldSnapshot apply sender={command.SenderUserId} teamId={command.TeamId} gold={command.Gold} version={command.Version}");
				ApplyGoldSnapshot(command);
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
				camp.BuyUnit(command.UnitType);
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

	private static void ApplyGoldSnapshot(GoldSnapshotCommand command)
	{
		GameManager.Instance?.ApplyRelayGoldSnapshot(command.TeamId, command.Gold, command.Version, command.SenderUserId);
	}

	private static SceneTree GetTree()
	{
		return Engine.GetMainLoop() as SceneTree;
	}
}

