using Godot;

// Online multiplayer sync helpers (Nakama relay). Child of Game.tscn.
public partial class NetworkSync : Node
{
	public static NetworkSync Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	public bool IsMultiplayer() => IsRelayMode();

	public bool IsRelayMode() => GameState.IsOnlineMultiplayer;

	public void SendUnitDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		if (!IsMultiplayer()) return;
		NetworkCommandRouter.SendUnitDamage(targetNetworkId, damage, attackerTeamId);
	}

	public void SendShipDamage(string targetNetworkId, float damage, int attackerTeamId)
	{
		if (!IsMultiplayer()) return;
		NetworkCommandRouter.SendShipDamage(targetNetworkId, damage, attackerTeamId);
	}

	public void SendEntityDied(string networkId)
	{
		if (!IsMultiplayer()) return;
		NetworkCommandRouter.SendEntityDied(networkId);
	}
}
