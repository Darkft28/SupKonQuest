using Godot;
using System.Collections.Generic;

// Registre statique NetworkId -> Node pour retrouver les entites reseau
public static class NetworkEntityRegistry
{
	private static readonly Dictionary<string, Node> _entities = new();
	private static int _counter = 0;

	// ID format: "{peerId}_{counter}"- deterministe par peer pour eviter les collisions
	public static string GenerateId()
	{
		long peerId = 1;
		var tree = Engine.GetMainLoop() as SceneTree;
		if (tree != null)
		{
			var mp = tree.GetMultiplayer();
			if (mp.MultiplayerPeer != null)
				peerId = mp.GetUniqueId();
		}

		_counter++;
		return $"{peerId}_{_counter}";
	}

	public static void Register(string networkId, Node node)
	{
		_entities[networkId] = node;
	}

	public static void Unregister(string networkId)
	{
		_entities.Remove(networkId);
	}

	public static T Get<T>(string networkId) where T : Node
	{
		if (_entities.TryGetValue(networkId, out var node) && node is T typed && GodotObject.IsInstanceValid(typed))
			return typed;
		return null;
	}

	public static Node Get(string networkId)
	{
		if (_entities.TryGetValue(networkId, out var node) && GodotObject.IsInstanceValid(node))
			return node;
		return null;
	}

	public static void Clear()
	{
		_entities.Clear();
		_counter = 0;
	}

	// Tous les IDs enregistres (pour le batch sync)
	public static IEnumerable<KeyValuePair<string, Node>> GetAll()
	{
		return _entities;
	}
}
