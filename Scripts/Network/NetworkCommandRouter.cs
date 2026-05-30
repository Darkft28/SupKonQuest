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
	public const long OpcodeSpawnShip = 1003;
	public const long OpcodeBuildPort = 1004;
	public const long OpcodeSpawnUnit = 1005;
	public const long OpcodeMoveUnits = 2001;
	public const long OpcodeAttackCamp = 2002;
	public const long OpcodeCampCaptured = 2003;
	public const long OpcodeMoveShips = 2004;
	public const long OpcodeCampDamage = 2005;
	public const long OpcodeBoardTransport = 2006;
	public const long OpcodeTransportUnloaded = 2007;
	public const long OpcodeUnitsMoveToTransport = 2008;
	public const long OpcodeGoldSnapshot = 3001;
	public const long OpcodeLobbyTick = 4001;
	public const long OpcodeMatchStart = 4002;
	public const long OpcodePlayerLeaveCleanup = 5002;
	public const long OpcodeCastUltimate = 6001;
	public const long OpcodeUltimateVfx = 6002;
	public const long OpcodeUnitDamage = 7001;
	public const long OpcodeShipDamage = 7002;
	public const long OpcodeEntityDied = 7003;
	private static readonly JsonSerializerOptions RelayJsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private static readonly JsonSerializerOptions RelayJsonReadOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private static int _sequence;
	private const int MaxDeferredMoveAttempts = 12;

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
		public bool UnloadRequested { get; set; }
		public float UnloadX { get; set; }
		public float UnloadY { get; set; }
	}

	[Serializable]
	private sealed class UnitDamageCommand : RelayCommandBase
	{
		public string TargetNetworkId { get; set; } = "";
		public float Damage { get; set; }
		public int AttackerTeamId { get; set; }
	}

	[Serializable]
	private sealed class ShipDamageCommand : RelayCommandBase
	{
		public string TargetNetworkId { get; set; } = "";
		public float Damage { get; set; }
		public int AttackerTeamId { get; set; }
	}

	[Serializable]
	private sealed class EntityDiedCommand : RelayCommandBase
	{
		public string NetworkId { get; set; } = "";
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

	[Serializable]
	private sealed class CampDamageCommand : RelayCommandBase
	{
		public int CampId { get; set; }
		public float Damage { get; set; }
		public int AttackerTeamId { get; set; }
	}

	[Serializable]
	private sealed class BoardTransportCommand : RelayCommandBase
	{
		public string ShipNetworkId { get; set; } = "";
		public string UnitNetworkId { get; set; } = "";
		public string UnitType { get; set; } = "";
		public int TeamId { get; set; }
		public float Health { get; set; }
	}

	[Serializable]
	private sealed class TransportUnloadedCommand : RelayCommandBase
	{
		public string ShipNetworkId { get; set; } = "";
		public string[] UnitNetworkIds { get; set; } = Array.Empty<string>();
		public string[] UnitTypes { get; set; } = Array.Empty<string>();
		public int TeamId { get; set; }
		public float[] PosXs { get; set; } = Array.Empty<float>();
		public float[] PosYs { get; set; } = Array.Empty<float>();
		public float[] Healths { get; set; } = Array.Empty<float>();
	}

	[Serializable]
	private sealed class UnitsMoveToTransportCommand : RelayCommandBase
	{
		public string ShipNetworkId { get; set; } = "";
		public string[] UnitIds { get; set; } = Array.Empty<string>();
	}

	[Serializable]
	private sealed class SpawnShipCommand : RelayCommandBase
	{
		public string NetworkId { get; set; } = "";
		public string ShipType { get; set; } = "";
		public int TeamId { get; set; }
		public float PosX { get; set; }
		public float PosY { get; set; }
		public float Health { get; set; }
	}

	[Serializable]
	private sealed class SpawnUnitCommand : RelayCommandBase
	{
		public string NetworkId { get; set; } = "";
		public string UnitType { get; set; } = "";
		public int TeamId { get; set; }
		public int CampId { get; set; }
		public float PosX { get; set; }
		public float PosY { get; set; }
		public float Health { get; set; }
	}

	[Serializable]
	private sealed class BuildPortCommand : RelayCommandBase
	{
		public int CampId { get; set; }
		public int TeamId { get; set; }
		public float PosX { get; set; }
		public float PosY { get; set; }
		public float Rotation { get; set; }
		public bool FlipH { get; set; }
	}

	[Serializable]
	private sealed class PlayerLeaveCleanupCommand : RelayCommandBase
	{
		public int TeamId { get; set; }
	}

	[Serializable]
	private sealed class CastUltimateCommand : RelayCommandBase
	{
		// Keep legacy field for currently deployed relay validator compatibility.
		public string[] UnitIds { get; set; } = Array.Empty<string>();
		public int TeamId { get; set; }
		public string AbilityId { get; set; } = "";
		public float TargetX { get; set; }
		public float TargetY { get; set; }
	}

	[Serializable]
	private sealed class UltimateVfxCommand : RelayCommandBase
	{
		public int TeamId { get; set; }
		public string AbilityId { get; set; } = "";
		public float TargetX { get; set; }
		public float TargetY { get; set; }
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

		string[] unitIds = validUnits
			.Where(unit => !string.IsNullOrWhiteSpace(unit.NetworkId))
			.Select(unit => unit.NetworkId)
			.ToArray();
		if (unitIds.Length == 0)
		{
			GD.PrintErr("[RELAY] MoveUnits aborted: selected units have no NetworkId.");
			return;
		}

		foreach (var unit in validUnits)
			unit.MoveTo(target);

		SendRelayAsync(OpcodeMoveUnits, new MoveUnitsCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			UnitIds = unitIds,
			StartX = validUnits[0].GlobalPosition.X,
			StartY = validUnits[0].GlobalPosition.Y,
			TargetX = target.X,
			TargetY = target.Y
		});
	}

	public static void RequestMoveShips(IEnumerable<Ship> ships, Vector2 target)
	{
		RequestMoveShipsInternal(ships, target, false, Vector2.Zero);
	}

	public static void RequestMoveShipsUnload(IEnumerable<Ship> ships, Vector2 landTarget)
	{
		RequestMoveShipsInternal(ships, landTarget, true, landTarget);
	}

	private static void RequestMoveShipsInternal(IEnumerable<Ship> ships, Vector2 target, bool unloadRequested, Vector2 unloadTarget)
	{
		var validShips = ships?.Where(ship => ship != null && GodotObject.IsInstanceValid(ship)).ToList() ?? new List<Ship>();
		if (validShips.Count == 0)
			return;

		string[] shipIds = validShips
			.Where(ship => !string.IsNullOrWhiteSpace(ship.NetworkId))
			.Select(ship => ship.NetworkId)
			.ToArray();
		if (shipIds.Length == 0)
		{
			GD.PrintErr("[RELAY] MoveShips aborted: selected ships have no NetworkId.");
			return;
		}

		if (unloadRequested)
		{
			foreach (var ship in validShips)
				ship.MoveToUnload(unloadTarget);
		}
		else
		{
			foreach (var ship in validShips)
				ship.MoveTo(target);
		}

		SendRelayAsync(OpcodeMoveShips, new MoveShipsCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			ShipIds = shipIds,
			TargetX = target.X,
			TargetY = target.Y,
			UnloadRequested = unloadRequested,
			UnloadX = unloadTarget.X,
			UnloadY = unloadTarget.Y
		});
	}

	public static void SendUnitDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		if (string.IsNullOrWhiteSpace(targetNetworkId) || damage <= 0f || attackerTeamId <= 0)
			return;

		SendRelayAsync(OpcodeUnitDamage, new UnitDamageCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			TargetNetworkId = targetNetworkId,
			Damage = damage,
			AttackerTeamId = attackerTeamId
		});
	}

	public static void SendShipDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		if (string.IsNullOrWhiteSpace(targetNetworkId) || damage <= 0f || attackerTeamId <= 0)
			return;

		SendRelayAsync(OpcodeShipDamage, new ShipDamageCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			TargetNetworkId = targetNetworkId,
			Damage = damage,
			AttackerTeamId = attackerTeamId
		});
	}

	public static void SendEntityDied(string networkId)
	{
		if (string.IsNullOrWhiteSpace(networkId))
			return;

		SendRelayAsync(OpcodeEntityDied, new EntityDiedCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			NetworkId = networkId
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

	public static void RequestUnitsMoveToTransport(Ship transport, IEnumerable<Unit> units)
	{
		if (transport == null || !GodotObject.IsInstanceValid(transport) || string.IsNullOrWhiteSpace(transport.NetworkId))
			return;

		var validUnits = units?
			.Where(unit => unit != null && GodotObject.IsInstanceValid(unit) && !string.IsNullOrWhiteSpace(unit.NetworkId))
			.ToList() ?? new List<Unit>();
		if (validUnits.Count == 0)
		{
			GD.PrintErr("[RELAY] UnitsMoveToTransport aborted: no units with NetworkId.");
			return;
		}

		foreach (var unit in validUnits)
			unit.MoveToTransport(transport);

		SendRelayAsync(OpcodeUnitsMoveToTransport, new UnitsMoveToTransportCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			ShipNetworkId = transport.NetworkId,
			UnitIds = validUnits.Select(unit => unit.NetworkId).ToArray()
		});
	}

	public static void SendBoardTransport(string shipNetworkId, string unitNetworkId, string unitType, int teamId, float health)
	{
		if (string.IsNullOrWhiteSpace(shipNetworkId) || string.IsNullOrWhiteSpace(unitNetworkId) || string.IsNullOrWhiteSpace(unitType) || teamId <= 0)
			return;

		SendRelayAsync(OpcodeBoardTransport, new BoardTransportCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			ShipNetworkId = shipNetworkId,
			UnitNetworkId = unitNetworkId,
			UnitType = unitType,
			TeamId = teamId,
			Health = health
		});
	}

	public static void SendTransportUnloaded(string shipNetworkId, string[] unitNetworkIds, string[] unitTypes, int teamId, float[] posXs, float[] posYs, float[] healths)
	{
		if (string.IsNullOrWhiteSpace(shipNetworkId) || unitNetworkIds == null || unitNetworkIds.Length == 0)
			return;

		SendRelayAsync(OpcodeTransportUnloaded, new TransportUnloadedCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			ShipNetworkId = shipNetworkId,
			UnitNetworkIds = unitNetworkIds,
			UnitTypes = unitTypes,
			TeamId = teamId,
			PosXs = posXs,
			PosYs = posYs,
			Healths = healths
		});
	}

	public static void SendCampDamage(int campId, float damage, int attackerTeamId)
	{
		if (campId <= 0 || damage <= 0f || attackerTeamId <= 0)
			return;

		SendRelayAsync(OpcodeCampDamage, new CampDamageCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			CampId = campId,
			Damage = damage,
			AttackerTeamId = attackerTeamId
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

	public static void SendSpawnShip(string networkId, string shipType, int teamId, float posX, float posY, float health)
	{
		if (string.IsNullOrWhiteSpace(networkId) || string.IsNullOrWhiteSpace(shipType) || teamId <= 0)
			return;

		SendRelayAsync(OpcodeSpawnShip, new SpawnShipCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			NetworkId = networkId,
			ShipType = shipType,
			TeamId = teamId,
			PosX = posX,
			PosY = posY,
			Health = health
		});
	}

	public static void SendSpawnUnit(string networkId, string unitType, int teamId, int campId, float posX, float posY, float health)
	{
		if (string.IsNullOrWhiteSpace(networkId) || string.IsNullOrWhiteSpace(unitType) || teamId <= 0 || campId <= 0)
			return;

		SendRelayAsync(OpcodeSpawnUnit, new SpawnUnitCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			NetworkId = networkId,
			UnitType = unitType,
			TeamId = teamId,
			CampId = campId,
			PosX = posX,
			PosY = posY,
			Health = health
		});
	}

	public static void SendBuildPort(CampSimple camp, float posX, float posY, float rotation, bool flipH)
	{
		if (camp == null || !GodotObject.IsInstanceValid(camp))
			return;

		SendRelayAsync(OpcodeBuildPort, new BuildPortCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			CampId = camp.GetCampId(),
			TeamId = camp.GetTeamId(),
			PosX = posX,
			PosY = posY,
			Rotation = rotation,
			FlipH = flipH
		});
	}

	public static void SendPlayerLeaveCleanup(int teamId)
	{
		if (teamId <= 0)
			return;

		SendRelayAsync(OpcodePlayerLeaveCleanup, new PlayerLeaveCleanupCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			TeamId = teamId
		});
	}

	public static void RequestCastUltimate(int teamId, string abilityId, Vector2 target)
	{
		if (teamId <= 0 || string.IsNullOrWhiteSpace(abilityId))
			return;

		bool castStarted = GameManager.Instance != null && GameManager.Instance.TryCastTeamUltimate(teamId, abilityId, target);
		if (!castStarted)
			return;

		SendRelayAsync(OpcodeCastUltimate, new CastUltimateCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			UnitIds = new[] { $"team_{teamId}"},
			TeamId = teamId,
			AbilityId = abilityId,
			TargetX = target.X,
			TargetY = target.Y
		});
	}

	public static void SendUltimateVfx(int teamId, string abilityId, Vector2 target)
	{
		if (teamId <= 0 || string.IsNullOrWhiteSpace(abilityId))
			return;

		SendRelayAsync(OpcodeUltimateVfx, new UltimateVfxCommand
		{
			SenderUserId = NakamaService.Instance?.UserId ?? "",
			Sequence = ++_sequence,
			TeamId = teamId,
			AbilityId = abilityId,
			TargetX = target.X,
			TargetY = target.Y
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
			case OpcodeCampDamage:
			{
				var command = JsonSerializer.Deserialize<CampDamageCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] CampDamage deserialize failed.");
					return;
				}

				// Apply on all peers (including sender): damage is only dispatched via relay.
				ApplyCampDamage(command);
				break;
			}
			case OpcodeUnitsMoveToTransport:
			{
				var command = JsonSerializer.Deserialize<UnitsMoveToTransportCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] UnitsMoveToTransport deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
					return;

				ApplyUnitsMoveToTransport(command);
				break;
			}
			case OpcodeBoardTransport:
			{
				var command = JsonSerializer.Deserialize<BoardTransportCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] BoardTransport deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
					return;

				ApplyBoardTransport(command);
				break;
			}
			case OpcodeTransportUnloaded:
			{
				var command = JsonSerializer.Deserialize<TransportUnloadedCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] TransportUnloaded deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
					return;

				ApplyTransportUnloaded(command);
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
			case OpcodeSpawnShip:
			{
				var command = JsonSerializer.Deserialize<SpawnShipCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] SpawnShip deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
					return;

				ApplySpawnShip(command);
				break;
			}
			case OpcodeSpawnUnit:
			{
				var command = JsonSerializer.Deserialize<SpawnUnitCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] SpawnUnit deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
					return;

				GD.Print($"[RELAY] SpawnUnit apply sender={command.SenderUserId} networkId={command.NetworkId} campId={command.CampId}");
				ApplySpawnUnit(command);
				break;
			}
			case OpcodeBuildPort:
			{
				var command = JsonSerializer.Deserialize<BuildPortCommand>(payload, RelayJsonReadOptions);
				if (command == null)
				{
					GD.PrintErr("[RELAY] BuildPort deserialize failed.");
					return;
				}

				if (command.SenderUserId == localUserId)
					return;

				ApplyBuildPort(command);
				break;
			}
			case OpcodePlayerLeaveCleanup:
			{
				var command = JsonSerializer.Deserialize<PlayerLeaveCleanupCommand>(payload, RelayJsonReadOptions);
				if (command == null || command.SenderUserId == localUserId)
					return;

				ApplyPlayerLeaveCleanup(command);
				break;
			}
			case OpcodeCastUltimate:
			{
				var command = JsonSerializer.Deserialize<CastUltimateCommand>(payload, RelayJsonReadOptions);
				if (command == null || command.SenderUserId == localUserId)
					return;

				ApplyCastUltimate(command);
				break;
			}
			case OpcodeUltimateVfx:
			{
				var command = JsonSerializer.Deserialize<UltimateVfxCommand>(payload, RelayJsonReadOptions);
				if (command == null || command.SenderUserId == localUserId)
					return;

				ApplyUltimateVfx(command);
				break;
			}
			case OpcodeUnitDamage:
			{
				var command = JsonSerializer.Deserialize<UnitDamageCommand>(payload, RelayJsonReadOptions);
				if (command == null)
					return;

				ApplyUnitDamage(command);
				break;
			}
			case OpcodeShipDamage:
			{
				var command = JsonSerializer.Deserialize<ShipDamageCommand>(payload, RelayJsonReadOptions);
				if (command == null)
					return;

				ApplyShipDamage(command);
				break;
			}
			case OpcodeEntityDied:
			{
				var command = JsonSerializer.Deserialize<EntityDiedCommand>(payload, RelayJsonReadOptions);
				if (command == null || command.SenderUserId == localUserId)
					return;

				ApplyEntityDied(command);
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
		// Buyer is authoritative for gold/queue; units replicate via OpcodeSpawnUnit when production completes.
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

	private static void ApplyMoveUnits(MoveUnitsCommand command, int attempt = 0)
	{
		Vector2 target = new(command.TargetX, command.TargetY);
		var missingIds = new List<string>();

		foreach (string unitId in command.UnitIds)
		{
			var unit = NetworkEntityRegistry.Get<Unit>(unitId);
			if (unit == null || !GodotObject.IsInstanceValid(unit))
			{
				missingIds.Add(unitId);
				continue;
			}

			unit.MoveTo(target);
		}

		if (missingIds.Count == 0 || attempt >= MaxDeferredMoveAttempts)
			return;

		var tree = GetTree();
		if (tree == null)
			return;

		var timer = tree.CreateTimer(0.1);
		timer.Timeout += () => ApplyMoveUnits(command, attempt + 1);
	}

	private static void ApplyMoveShips(MoveShipsCommand command, int attempt = 0)
	{
		Vector2 target = new(command.TargetX, command.TargetY);
		Vector2 unloadTarget = new(command.UnloadX, command.UnloadY);
		var missingIds = new List<string>();

		foreach (string shipId in command.ShipIds)
		{
			var ship = NetworkEntityRegistry.Get<Ship>(shipId);
			if (ship == null || !GodotObject.IsInstanceValid(ship))
			{
				missingIds.Add(shipId);
				continue;
			}

			if (command.UnloadRequested)
				ship.MoveToUnload(unloadTarget);
			else
				ship.MoveTo(target, trustRelayTarget: true);
		}

		if (missingIds.Count == 0 || attempt >= MaxDeferredMoveAttempts)
			return;

		var tree = GetTree();
		if (tree == null)
			return;

		var timer = tree.CreateTimer(0.1);
		timer.Timeout += () => ApplyMoveShips(command, attempt + 1);
	}

	private static void ApplyUnitDamage(UnitDamageCommand command)
	{
		var unit = NetworkEntityRegistry.Get<Unit>(command.TargetNetworkId);
		if (unit != null && GodotObject.IsInstanceValid(unit))
			unit.TakeDamageFrom(command.Damage, command.AttackerTeamId);
	}

	private static void ApplyShipDamage(ShipDamageCommand command)
	{
		var ship = NetworkEntityRegistry.Get<Ship>(command.TargetNetworkId);
		if (ship != null && GodotObject.IsInstanceValid(ship))
			ship.TakeDamageFrom(command.Damage, command.AttackerTeamId);
	}

	private static void ApplyEntityDied(EntityDiedCommand command)
	{
		var node = NetworkEntityRegistry.Get(command.NetworkId);
		if (node != null && GodotObject.IsInstanceValid(node))
			node.QueueFree();
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

	private static void ApplyUnitsMoveToTransport(UnitsMoveToTransportCommand command)
	{
		var ship = NetworkEntityRegistry.Get<Ship>(command.ShipNetworkId);
		if (ship == null || !GodotObject.IsInstanceValid(ship))
			return;

		foreach (string unitId in command.UnitIds)
		{
			var unit = NetworkEntityRegistry.Get<Unit>(unitId);
			if (unit != null && GodotObject.IsInstanceValid(unit))
				unit.MoveToTransport(ship);
		}
	}

	private static void ApplyBoardTransport(BoardTransportCommand command)
	{
		var ship = NetworkEntityRegistry.Get<Ship>(command.ShipNetworkId);
		if (ship != null && GodotObject.IsInstanceValid(ship))
			ship.ApplyRelayBoardTransport(command.UnitNetworkId, command.UnitType, command.TeamId, command.Health);
	}

	private static void ApplyTransportUnloaded(TransportUnloadedCommand command)
	{
		var ship = NetworkEntityRegistry.Get<Ship>(command.ShipNetworkId);
		if (ship != null && GodotObject.IsInstanceValid(ship))
		{
			ship.ApplyRelayTransportUnloaded(
				command.UnitNetworkIds,
				command.UnitTypes,
				command.TeamId,
				command.PosXs,
				command.PosYs,
				command.Healths);
		}
	}

	private static void ApplyCampDamage(CampDamageCommand command)
	{
		var camps = GetTree()?.GetNodesInGroup("camps");
		if (camps == null) return;

		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetCampId() == command.CampId)
			{
				camp.ApplyRelayCampDamage(command.Damage, command.AttackerTeamId);
				break;
			}
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

	private static void ApplySpawnUnit(SpawnUnitCommand command)
	{
		var existing = NetworkEntityRegistry.Get(command.NetworkId);
		if (existing is Unit existingUnit && GodotObject.IsInstanceValid(existingUnit))
		{
			if (existingUnit.UnitType == command.UnitType)
				return;

			existingUnit.QueueFree();
			NetworkEntityRegistry.Unregister(command.NetworkId);
		}

		CampSimple ownerCamp = null;
		var camps = GetTree()?.GetNodesInGroup("camps");
		if (camps != null)
		{
			foreach (var node in camps)
			{
				if (node is CampSimple camp && camp.GetCampId() == command.CampId)
				{
					ownerCamp = camp;
					break;
				}
			}
		}

		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null)
			return;

		var unit = unitScene.Instantiate<Unit>();
		unit.UnitType = command.UnitType;
		unit.TeamId = command.TeamId;
		unit.IsNeutralCampUnit = false;
		unit.GlobalPosition = new Vector2(command.PosX, command.PosY);
		unit.NetworkId = command.NetworkId;
		unit.IsLocalAuthority = false;
		if (ownerCamp != null)
		{
			unit.OwnerCamp = ownerCamp;
			unit.RegionId = ownerCamp.RegionId;
		}

		var scene = GetTree()?.CurrentScene;
		if (scene == null)
			return;

		scene.AddChild(unit);
		unit.SetCurrentHealth(command.Health);
		ownerCamp?.RegisterRelaySpawnedUnit(unit);
	}

	private static void ApplySpawnShip(SpawnShipCommand command)
	{
		var existing = NetworkEntityRegistry.Get(command.NetworkId);
		if (existing is Ship existingShip && GodotObject.IsInstanceValid(existingShip))
		{
			if (existingShip.ShipType == command.ShipType)
			{
				return;
			}

			existingShip.QueueFree();
			NetworkEntityRegistry.Unregister(command.NetworkId);
		}

		var shipScene = GD.Load<PackedScene>("res://Scenes/Ship.tscn");
		if (shipScene == null)
			return;

		var ship = shipScene.Instantiate<Ship>();
		ship.ShipType = command.ShipType;
		ship.TeamId = command.TeamId;
		ship.GlobalPosition = new Vector2(command.PosX, command.PosY);
		ship.NetworkId = command.NetworkId;
		ship.IsLocalAuthority = false;

		var scene = GetTree()?.CurrentScene;
		if (scene == null)
			return;

		scene.AddChild(ship);
		ship.SetTileMapSol(FindTileMapSolForSpawn());
		ship.SetCurrentHealth(command.Health);
	}

	private static TileMapLayer FindTileMapSolForSpawn()
	{
		var tree = GetTree();
		if (tree?.CurrentScene == null)
			return null;

		var mapGenerator = tree.CurrentScene.FindChild("MapGenerator", true, false);
		return mapGenerator?.GetNodeOrNull<TileMapLayer>("Sol");
	}

	private static void ApplyBuildPort(BuildPortCommand command)
	{
		var camps = GetTree()?.GetNodesInGroup("camps");
		if (camps == null) return;

		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetCampId() == command.CampId)
			{
				camp.ApplyRemotePortPlacement(command.PosX, command.PosY, command.Rotation, command.FlipH);
				break;
			}
		}
	}

	private static void ApplyPlayerLeaveCleanup(PlayerLeaveCleanupCommand command)
	{
		GameManager.Instance?.ApplyPlayerLeaveCleanup(command.TeamId);
	}

	private static void ApplyCastUltimate(CastUltimateCommand command)
	{
		GameManager.Instance?.ApplyRemoteTeamUltimateCast(
			command.TeamId,
			command.AbilityId,
			new Vector2(command.TargetX, command.TargetY)
		);
	}

	private static void ApplyUltimateVfx(UltimateVfxCommand command)
	{
		if (string.IsNullOrWhiteSpace(command.AbilityId))
			return;

		Unit.EmitUltimateCastVfx(
			null,
			command.AbilityId,
			new Vector2(command.TargetX, command.TargetY)
		);
	}

	private static SceneTree GetTree()
	{
		return Engine.GetMainLoop() as SceneTree;
	}
}

