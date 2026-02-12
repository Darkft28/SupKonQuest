using Godot;
using System.Collections.Generic;

public class VictoryManager
{
	private readonly GameManager _gameManager;
	private float _victoryCheckTimer = 0f;
	private const float VictoryCheckInterval = 1f;

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
		var allCamps = _gameManager.GetAllCamps();
		if (allCamps.Count == 0)
			return;

		// Compter les camps par équipe (hors neutres)
		Dictionary<int, int> campCountByTeam = new Dictionary<int, int>();
		int nonNeutralCamps = 0;

		foreach (var camp in allCamps)
		{
			if (camp == null || !GodotObject.IsInstanceValid(camp))
				continue;

			int teamId = camp.GetTeamId();

			// Ignorer les camps neutres (teamId <= 0)
			if (teamId <= 0)
				continue;

			nonNeutralCamps++;

			if (!campCountByTeam.ContainsKey(teamId))
			{
				campCountByTeam[teamId] = 0;
			}
			campCountByTeam[teamId]++;
		}

		// Vérifier si un joueur possède tous les camps non-neutres
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
		GD.Print($"========================================");
		GD.Print($"   VICTOIRE! Joueur {winningTeamId} a gagne!");
		GD.Print($"========================================");

		// Afficher un message à l'écran
		DisplayVictoryMessage(winningTeamId);
	}

	private void DisplayVictoryMessage(int winningTeamId)
	{
		// Créer un label pour afficher la victoire
		Label victoryLabel = new Label();
		victoryLabel.Text = $"VICTOIRE!\nLe Joueur {winningTeamId} a conquis tous les camps!";
		victoryLabel.HorizontalAlignment = HorizontalAlignment.Center;
		victoryLabel.VerticalAlignment = VerticalAlignment.Center;
		victoryLabel.AddThemeFontSizeOverride("font_size", 48);
		victoryLabel.AddThemeColorOverride("font_color", new Color(1, 0.84f, 0, 1)); // Or
		victoryLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		victoryLabel.AddThemeConstantOverride("outline_size", 5);

		// Positionner au centre de l'écran
		victoryLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
		victoryLabel.GrowHorizontal = Control.GrowDirection.Both;
		victoryLabel.GrowVertical = Control.GrowDirection.Both;

		// Ajouter à la scène
		var canvasLayer = new CanvasLayer();
		canvasLayer.Layer = 100;
		canvasLayer.AddChild(victoryLabel);
		_gameManager.GetTree().Root.AddChild(canvasLayer);

		// Mettre le jeu en pause
		_gameManager.GetTree().Paused = true;
	}
}
