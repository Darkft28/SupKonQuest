using System.Collections.Generic;

public class VictoryManager
{
	private readonly GameManager _gameManager;
	private float _victoryCheckTimer = 0f;
	private const float VictoryCheckInterval = 1f;
	private bool _victoryDeclared = false;

	public VictoryManager(GameManager gameManager)
	{
		_gameManager = gameManager;
	}

	public void Update(double delta)
	{
		_victoryCheckTimer += (float)delta;
		if (_victoryCheckTimer >= VictoryCheckInterval)
		{
			_victoryCheckTimer = 0f;
			CheckVictoryCondition();
		}
	}

	private void CheckVictoryCondition()
	{
		if (_victoryDeclared) return;

		var allCamps = _gameManager.GetAllCamps();
		if (allCamps.Count == 0)
			return;

		Dictionary<int, int> campCountByTeam = new Dictionary<int, int>();
		int nonNeutralCamps = 0;

		foreach (var camp in allCamps)
		{
			if (camp == null || !Godot.GodotObject.IsInstanceValid(camp))
				continue;

			int teamId = camp.GetTeamId();
			if (teamId <= 0)
				continue;

			nonNeutralCamps++;

			if (!campCountByTeam.ContainsKey(teamId))
				campCountByTeam[teamId] = 0;

			campCountByTeam[teamId]++;
		}

		foreach (var pair in campCountByTeam)
		{
			if (pair.Value == nonNeutralCamps && nonNeutralCamps > 0)
			{
				DeclareVictory(pair.Key);
				return;
			}
		}
	}

	private void DeclareVictory(int winningTeamId)
	{
		_victoryDeclared = true;
		_gameManager.NotifyGameWon(winningTeamId);
	}
}
