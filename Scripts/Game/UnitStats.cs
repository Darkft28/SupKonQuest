using Godot;
using System.Collections.Generic;

public class UnitStatsData
{
	public string Name { get; set; }
	public float MaxHealth { get; set; }
	public float Attack { get; set; }
	public float Defense { get; set; }
	public float Speed { get; set; }
	public float Range { get; set; }
	public int Price { get; set; }
	public float ProductionTime { get; set; } // Temps de production en secondes
}

public static class UnitStats
{
	private static readonly Dictionary<string, UnitStatsData> Stats = new Dictionary<string, UnitStatsData>
	{
		["Infantry"] = new UnitStatsData
		{
			Name = "Infantry",
			MaxHealth = 100f,
			Attack = 15f,
			Defense = 10f,
			Speed = 150f,
			Range = 50f,
			Price = 50,
			ProductionTime = 2f
		},
		["Support"] = new UnitStatsData
		{
			Name = "Support",
			MaxHealth = 80f,
			Attack = 8f,
			Defense = 5f,
			Speed = 120f,
			Range = 100f,
			Price = 75,
			ProductionTime = 3f
		},
		["Heal"] = new UnitStatsData
		{
			Name = "Heal",
			MaxHealth = 60f,
			Attack = 0f,
			Defense = 3f,
			Speed = 100f,
			Range = 150f,
			Price = 100,
			ProductionTime = 3f
		},
		["Range"] = new UnitStatsData
		{
			Name = "Range",
			MaxHealth = 70f,
			Attack = 20f,
			Defense = 5f,
			Speed = 100f,
			Range = 300f,
			Price = 80,
			ProductionTime = 3f
		},
		["AntiArmor"] = new UnitStatsData
		{
			Name = "AntiArmor",
			MaxHealth = 80f,
			Attack = 35f,
			Defense = 8f,
			Speed = 90f,
			Range = 120f,
			Price = 120,
			ProductionTime = 4f
		},
		["Heavy"] = new UnitStatsData
		{
			Name = "Heavy",
			MaxHealth = 150f,
			Attack = 25f,
			Defense = 20f,
			Speed = 70f,
			Range = 60f,
			Price = 150,
			ProductionTime = 5f
		},
		["Mortar"] = new UnitStatsData
		{
			Name = "Mortar",
			MaxHealth = 50f,
			Attack = 40f,
			Defense = 3f,
			Speed = 60f,
			Range = 400f,
			Price = 130,
			ProductionTime = 4f
		},
		["Tank"] = new UnitStatsData
		{
			Name = "Tank",
			MaxHealth = 200f,
			Attack = 30f,
			Defense = 25f,
			Speed = 50f,
			Range = 100f,
			Price = 200,
			ProductionTime = 6f
		}
	};

	public static UnitStatsData GetStats(string unitType)
	{
		if (Stats.TryGetValue(unitType, out var stats))
		{
			return stats;
		}

		// Retourner des stats par défaut si type inconnu
		GD.PrintErr($"Type d'unite inconnu: {unitType}, utilisation des stats par defaut");
		return new UnitStatsData
		{
			Name = unitType,
			MaxHealth = 50f,
			Attack = 10f,
			Defense = 5f,
			Speed = 100f,
			Range = 50f,
			Price = 50,
			ProductionTime = 3f
		};
	}
}
