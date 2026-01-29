using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	// Or par équipe
	private Dictionary<int, int> _teamGold = new Dictionary<int, int>();

	// Or de départ et bonus de capture
	private const int StartingGold = 100;
	private const int CaptureBonus = 50;
	private const int PassiveGoldPerSecond = 5;

	private float _passiveGoldTimer = 0f;

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _Process(double delta)
	{
		// Or passif chaque seconde
		_passiveGoldTimer += (float)delta;
		if (_passiveGoldTimer >= 1.0f)
		{
			_passiveGoldTimer = 0f;
			foreach (var teamId in _teamGold.Keys)
			{
				_teamGold[teamId] += PassiveGoldPerSecond;
			}
		}
	}

	public void InitializeTeam(int teamId)
	{
		// Ne pas initialiser les camps neutres (teamId <= 0)
		if (teamId <= 0)
			return;

		if (!_teamGold.ContainsKey(teamId))
		{
			_teamGold[teamId] = StartingGold;
			GD.Print($"Equipe {teamId} initialisee avec {StartingGold} or");
		}
	}

	public int GetGold(int teamId)
	{
		return _teamGold.TryGetValue(teamId, out int gold) ? gold : 0;
	}

	public bool CanAfford(int teamId, int cost)
	{
		return GetGold(teamId) >= cost;
	}

	public bool SpendGold(int teamId, int amount)
	{
		if (!CanAfford(teamId, amount))
			return false;

		_teamGold[teamId] -= amount;
		return true;
	}

	public void AddGold(int teamId, int amount)
	{
		if (!_teamGold.ContainsKey(teamId))
		{
			_teamGold[teamId] = 0;
		}
		_teamGold[teamId] += amount;
	}

	public void GiveCaptureBonus(int teamId)
	{
		AddGold(teamId, CaptureBonus);
		GD.Print($"Equipe {teamId} recoit {CaptureBonus} or pour la capture!");
	}
}
