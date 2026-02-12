using Godot;
using System.Collections.Generic;

public class ShipStatsData
{
	public string Name { get; set; }
	public float MaxHealth { get; set; }
	public float Attack { get; set; }
	public float Defense { get; set; }
	public float Speed { get; set; }
	public float Range { get; set; }
	public int Price { get; set; }
	public float ProductionTime { get; set; }
	public int Capacity { get; set; } // Nombre d'unites transportables (Transport uniquement)
}

public static class ShipStats
{
	private static readonly Dictionary<string, ShipStatsData> Stats = new Dictionary<string, ShipStatsData>
	{
		["Transport"] = new ShipStatsData
		{
			Name = "Transport",
			MaxHealth = 200f,
			Attack = 0f,
			Defense = 10f,
			Speed = 120f,
			Range = 0f,
			Price = 150,
			ProductionTime = 5f,
			Capacity = 10
		},
		["Fregate"] = new ShipStatsData
		{
			Name = "Fregate",
			MaxHealth = 180f,
			Attack = 20f,
			Defense = 15f,
			Speed = 100f,
			Range = 250f,
			Price = 200,
			ProductionTime = 5f,
			Capacity = 0
		},
		["Destroyer"] = new ShipStatsData
		{
			Name = "Destroyer",
			MaxHealth = 250f,
			Attack = 35f,
			Defense = 20f,
			Speed = 80f,
			Range = 350f,
			Price = 300,
			ProductionTime = 7f,
			Capacity = 0
		}
	};

	public static ShipStatsData GetStats(string shipType)
	{
		if (Stats.TryGetValue(shipType, out var stats))
		{
			return stats;
		}

		GD.PrintErr($"Type de bateau inconnu: {shipType}, utilisation des stats par defaut");
		return new ShipStatsData
		{
			Name = shipType,
			MaxHealth = 100f,
			Attack = 10f,
			Defense = 5f,
			Speed = 80f,
			Range = 100f,
			Price = 100,
			ProductionTime = 5f,
			Capacity = 0
		};
	}
}
