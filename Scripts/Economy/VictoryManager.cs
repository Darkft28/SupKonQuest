using Godot;
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
		_victoryDeclared = true;

		GD.Print($"========================================");
		GD.Print($"   VICTOIRE! Joueur {winningTeamId} a gagne!");
		GD.Print($"========================================");

		// Afficher un message à l'écran
		DisplayVictoryMessage(winningTeamId);
	}

	private void DisplayVictoryMessage(int winningTeamId)
	{
		var canvasLayer = new CanvasLayer();
		canvasLayer.Layer = 100;

		// Conteneur centré
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.GrowHorizontal = Control.GrowDirection.Both;
		vbox.GrowVertical = Control.GrowDirection.Both;
		vbox.AddThemeConstantOverride("separation", 20);

		// Label victoire
		var victoryLabel = new Label();
		string victoryText = LocalizationManager.Instance != null
			? LocalizationManager.Instance.GetText("victory")
			: "VICTOIRE!";
		victoryLabel.Text = $"{victoryText}\n{winningTeamId}";
		victoryLabel.HorizontalAlignment = HorizontalAlignment.Center;
		victoryLabel.AddThemeFontSizeOverride("font_size", 48);
		victoryLabel.AddThemeColorOverride("font_color", new Color(1, 0.84f, 0, 1));
		victoryLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		victoryLabel.AddThemeConstantOverride("outline_size", 5);

		// Bouton retour menu
		var menuButton = new Button();
		menuButton.Text = LocalizationManager.Instance != null
			? LocalizationManager.Instance.GetText("main_menu")
			: "Menu principal";
		menuButton.AddThemeFontSizeOverride("font_size", 28);
		menuButton.ProcessMode = Node.ProcessModeEnum.Always;
		menuButton.Pressed += () =>
		{
			canvasLayer.QueueFree();
			_gameManager.GetTree().Paused = false;
			_gameManager.GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
		};

		vbox.AddChild(victoryLabel);
		vbox.AddChild(menuButton);
		canvasLayer.AddChild(vbox);
		_gameManager.GetTree().Root.AddChild(canvasLayer);

		// Mettre le jeu en pause
		_gameManager.GetTree().Paused = true;
	}
}
