using Godot;
using System.Collections.Generic;

// Hub central de RPCs multijoueur. Enfant de Game.tscn.
public partial class NetworkSync : Node
{
	public static NetworkSync Instance { get; private set; }

	private float _syncTimer = 0f;
	private const float SyncInterval = 1f / 20f; // 20 Hz

	private float _goldSyncTimer = 0f;
	private const float GoldSyncInterval = 10f;
	private float _relayGoldSnapshotTimer = 0f;
	private const float RelayGoldSnapshotInterval = 1f;

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public override void _Process(double delta)
	{
		if (!IsMultiplayer()) return;

		if (IsNakamaRelayMode())
		{
			_relayGoldSnapshotTimer += (float)delta;
			if (_relayGoldSnapshotTimer >= RelayGoldSnapshotInterval)
			{
				_relayGoldSnapshotTimer = 0f;
				GameManager.Instance?.BroadcastRelayGoldSnapshotForLocalTeam();
			}
			return;
		}

		_syncTimer += (float)delta;
		if (_syncTimer >= SyncInterval)
		{
			_syncTimer = 0f;
			SendEntityStatesBatch();
		}

		if (Multiplayer.IsServer())
		{
			_goldSyncTimer += (float)delta;
			if (_goldSyncTimer >= GoldSyncInterval)
			{
				_goldSyncTimer = 0f;
				int team1Gold = GameManager.Instance?.GetGold(1) ?? 0;
				int team2Gold = GameManager.Instance?.GetGold(2) ?? 0;
				Rpc(nameof(RpcSyncGold), team1Gold, team2Gold);
			}
		}
	}

	public bool IsMultiplayer()
	{
		if (IsNakamaRelayMode())
			return true;

		// Utilise NetworkManager.IsConnected qui vérifie le peer ENet réel (_peer != null)
		// L'OfflineMultiplayerPeer par défaut de Godot 4 trompe HasMultiplayerPeer()
		var nm = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		return nm?.IsConnected ?? false;
	}

	private bool IsNakamaRelayMode()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		return gameState?.IsOnline == true && NakamaService.Instance?.IsSocketConnected == true;
	}

	public bool IsServer()
	{
		return IsMultiplayer() && Multiplayer.IsServer();
	}

	// =============================================
	// SPAWN RPCs (Reliable)
	// =============================================

	public void SendSpawnUnit(string networkId, string unitType, int teamId, float posX, float posY, float health, bool isNeutral)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcSpawnUnit), networkId, unitType, teamId, posX, posY, health, isNeutral);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSpawnUnit(string networkId, string unitType, int teamId, float posX, float posY, float health, bool isNeutral)
	{
		if (NetworkEntityRegistry.Get(networkId) != null) return;

		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null) return;

		var unit = unitScene.Instantiate<Unit>();
		unit.UnitType = unitType;
		unit.TeamId = teamId;
		unit.IsNeutralCampUnit = isNeutral;
		unit.GlobalPosition = new Vector2(posX, posY);
		unit.NetworkId = networkId;
		unit.IsLocalAuthority = false;

		GetTree().CurrentScene.AddChild(unit);
		unit.SetCurrentHealth(health);
	}

	public void SendSpawnShip(string networkId, string shipType, int teamId, float posX, float posY, float health)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcSpawnShip), networkId, shipType, teamId, posX, posY, health);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSpawnShip(string networkId, string shipType, int teamId, float posX, float posY, float health)
	{
		if (NetworkEntityRegistry.Get(networkId) != null) return;

		var shipScene = GD.Load<PackedScene>("res://Scenes/Ship.tscn");
		if (shipScene == null) return;

		var ship = shipScene.Instantiate<Ship>();
		ship.ShipType = shipType;
		ship.TeamId = teamId;
		ship.GlobalPosition = new Vector2(posX, posY);
		ship.NetworkId = networkId;
		ship.IsLocalAuthority = false;

		GetTree().CurrentScene.AddChild(ship);
	}

	// =============================================
	// MORT RPCs (Reliable)
	// =============================================

	public void SendEntityDied(string networkId)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcEntityDied), networkId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcEntityDied(string networkId)
	{
		var node = NetworkEntityRegistry.Get(networkId);
		if (node != null && GodotObject.IsInstanceValid(node))
			node.QueueFree();
	}

	// =============================================
	// DEGATS RPCs (Reliable)
	// =============================================

	public void SendUnitDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcApplyUnitDamage), targetNetworkId, damage, attackerTeamId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcApplyUnitDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		var unit = NetworkEntityRegistry.Get<Unit>(targetNetworkId);
		if (unit != null && unit.IsLocalAuthority)
		{
			unit.TakeDamageFrom(damage, attackerTeamId);
		}
	}

	public void SendShipDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcApplyShipDamage), targetNetworkId, damage, attackerTeamId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcApplyShipDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		var ship = NetworkEntityRegistry.Get<Ship>(targetNetworkId);
		if (ship != null && ship.IsLocalAuthority)
		{
			ship.TakeDamageFrom(damage, attackerTeamId);
		}
	}

	public void SendCampDamage(int campId, float damage, int attackerTeamId)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcApplyCampDamage), campId, damage, attackerTeamId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcApplyCampDamage(int campId, float damage, int attackerTeamId)
	{
		var camps = GetTree().GetNodesInGroup("camps");
		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetCampId() == campId)
			{
				if (camp.IsLocallyOwned())
					camp.TakeDamage(damage, attackerTeamId);
				break;
			}
		}
	}

	// =============================================
	// CAMP RPCs (Reliable)
	// =============================================

	public void SendCampCaptured(int campId, int newTeamId)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcCampCaptured), campId, newTeamId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCampCaptured(int campId, int newTeamId)
	{
		var camps = GetTree().GetNodesInGroup("camps");
		foreach (var node in camps)
		{
			if (node is CampSimple camp && camp.GetCampId() == campId)
			{
				camp.ApplyRemoteCapture(newTeamId);
				break;
			}
		}
	}

	public void SendSyncCampAssignments(int[] campIds, int[] teamIds, bool[] isNeutral)
	{
		if (!IsMultiplayer()) return;
		// Godot RPC ne supporte pas bool[] comme Variant, on convertit en int[]
		int[] isNeutralInt = new int[isNeutral.Length];
		for (int i = 0; i < isNeutral.Length; i++)
			isNeutralInt[i] = isNeutral[i] ? 1 : 0;
		Rpc(nameof(RpcSyncCampAssignments), campIds, teamIds, isNeutralInt);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSyncCampAssignments(int[] campIds, int[] teamIds, int[] isNeutralInt)
	{
		var camps = GetTree().GetNodesInGroup("camps");

		for (int i = 0; i < campIds.Length; i++)
		{
			bool neutral = isNeutralInt[i] != 0;
			foreach (var node in camps)
			{
				if (node is CampSimple camp && camp.GetCampId() == campIds[i])
				{
					camp.SetTeam(teamIds[i], neutral);
					camp.UpdateDefendersAuthority();
					break;
				}
			}
		}
	}

	// =============================================
	// TRANSPORT RPCs (Reliable)
	// =============================================

	public void SendUnitBoarded(string unitNetworkId, string shipNetworkId)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcUnitBoarded), unitNetworkId, shipNetworkId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcUnitBoarded(string unitNetworkId, string shipNetworkId)
	{
		var unit = NetworkEntityRegistry.Get<Unit>(unitNetworkId);
		if (unit != null && GodotObject.IsInstanceValid(unit))
			unit.QueueFree();
	}

	public void SendTransportUnloaded(string shipNetworkId, string[] unitNetworkIds, string[] unitTypes, int teamId, float[] posXs, float[] posYs, float[] healths)
	{
		if (!IsMultiplayer()) return;
		Rpc(nameof(RpcTransportUnloaded), shipNetworkId, unitNetworkIds, unitTypes, teamId, posXs, posYs, healths);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcTransportUnloaded(string shipNetworkId, string[] unitNetworkIds, string[] unitTypes, int teamId, float[] posXs, float[] posYs, float[] healths)
	{
		var unitScene = GD.Load<PackedScene>("res://Scenes/Unit.tscn");
		if (unitScene == null) return;

		for (int i = 0; i < unitNetworkIds.Length; i++)
		{
			if (NetworkEntityRegistry.Get(unitNetworkIds[i]) != null) continue;

			var unit = unitScene.Instantiate<Unit>();
			unit.UnitType = unitTypes[i];
			unit.TeamId = teamId;
			unit.IsNeutralCampUnit = false;
			unit.GlobalPosition = new Vector2(posXs[i], posYs[i]);
			unit.NetworkId = unitNetworkIds[i];
			unit.IsLocalAuthority = false;

			GetTree().CurrentScene.AddChild(unit);
			unit.SetCurrentHealth(healths[i]);
		}

	}

	private void SendEntityStatesBatch()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int localTeamId = gameState?.LocalTeamId ?? 1;

		List<string> ids = new();
		List<float> xs = new();
		List<float> ys = new();
		List<float> hps = new();
		List<int> states = new();

		foreach (var kvp in NetworkEntityRegistry.GetAll())
		{
			var node = kvp.Value;
			if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) continue;

			if (node is Unit unit && unit.IsLocalAuthority)
			{
				ids.Add(kvp.Key);
				xs.Add(unit.GlobalPosition.X);
				ys.Add(unit.GlobalPosition.Y);
				hps.Add(unit.GetCurrentHealth());
				states.Add(unit.GetStateInt());
			}
			else if (node is Ship ship && ship.IsLocalAuthority)
			{
				ids.Add(kvp.Key);
				xs.Add(ship.GlobalPosition.X);
				ys.Add(ship.GlobalPosition.Y);
				hps.Add(ship.GetCurrentHealth());
				states.Add(0);
			}
		}

		if (ids.Count == 0) return;

		Rpc(nameof(RpcSyncEntityStates), ids.ToArray(), xs.ToArray(), ys.ToArray(), hps.ToArray(), states.ToArray());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void RpcSyncEntityStates(string[] networkIds, float[] posXs, float[] posYs, float[] healths, int[] entityStates)
	{
		for (int i = 0; i < networkIds.Length; i++)
		{
			var node = NetworkEntityRegistry.Get(networkIds[i]);
			if (node == null || !GodotObject.IsInstanceValid(node)) continue;

			if (node is Unit unit && !unit.IsLocalAuthority)
			{
				unit.ApplyNetworkState(new Vector2(posXs[i], posYs[i]), healths[i], entityStates[i]);
			}
			else if (node is Ship ship && !ship.IsLocalAuthority)
			{
				ship.ApplyNetworkState(new Vector2(posXs[i], posYs[i]), healths[i]);
			}
		}
	}

	// =============================================
	// SYNC OR (Reliable, toutes les 10s, serveur -> clients)
	// =============================================

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcSyncGold(int team1Gold, int team2Gold)
	{
		// Correction légère : seulement si écart > 5 or pour éviter les micro-corrections
		GameManager.Instance?.SyncGold(1, team1Gold);
		GameManager.Instance?.SyncGold(2, team2Gold);
	}
}
