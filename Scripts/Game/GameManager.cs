using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
	private static GameManager _instance;
	
	public static GameManager Instance
	{
		get { return _instance; }
	}

	// Or par équipe
	private Dictionary<int, int> _teamGold = new Dictionary<int, int>();

	// Or de départ et bonus de capture
	private const int StartingGold = 100;
	private const int CaptureBonus = 50;
	private const int PassiveGoldPerSecond = 5;

	private float _passiveGoldTimer = 0f;
	
	// Timer pour vérifier la victoire
	private float _victoryCheckTimer = 0f;
	private const float VictoryCheckInterval = 1f;
	
	// Liste des camps de la scène
	private List<CampSimple> _allCamps = new List<CampSimple>();
	
	// Nombre de joueurs
	private const int NumberOfPlayers = 2;

	public override void _Ready()
	{
		_instance = this;
		// L'initialisation des camps sera faite après la génération de la map
		// via OnMapGenerationComplete()
	}
	
	// Méthode publique appelée par MapGenerator après la génération des camps
	public void OnMapGenerationComplete()
	{
		InitializeCamps();
	}
	
	private void InitializeCamps()
	{
		// Récupérer tous les camps de la scène
		_allCamps.Clear();
		var campNodes = GetTree().GetNodesInGroup("camps");
		
		foreach (var node in campNodes)
		{
			if (node is CampSimple camp)
			{
				_allCamps.Add(camp);
			}
		}
		
		GD.Print($"Nombre de camps trouves: {_allCamps.Count}");
		
		// Attribuer les camps aux joueurs
		AssignCampsToPlayers();
	}
	
	private void AssignCampsToPlayers()
	{
		if (_allCamps.Count == 0)
		{
			GD.Print("Aucun camp a attribuer");
			return;
		}
		
		// Mélanger la liste des camps de manière aléatoire
		List<CampSimple> shuffledCamps = new List<CampSimple>(_allCamps);
		ShuffleList(shuffledCamps);
		
		// Calculer le nombre de camps par joueur
		int campsPerPlayer = shuffledCamps.Count / NumberOfPlayers;
		
		int campIndex = 0;
		
		// Attribuer les camps équitablement aux joueurs
		for (int playerId = 1; playerId <= NumberOfPlayers; playerId++)
		{
			for (int i = 0; i < campsPerPlayer; i++)
			{
				if (campIndex < shuffledCamps.Count)
				{
					CampSimple camp = shuffledCamps[campIndex];
					camp.SetTeam(playerId, false);
					GD.Print($"Camp #{camp.GetCampId()} attribue au Joueur {playerId}");
					campIndex++;
				}
			}
			
			// Initialiser l'or de l'équipe
			InitializeTeam(playerId);
		}
		
		// Les camps restants deviennent neutres
		while (campIndex < shuffledCamps.Count)
		{
			CampSimple camp = shuffledCamps[campIndex];
			camp.SetTeam(0, true);
			GD.Print($"Camp #{camp.GetCampId()} est Neutre");
			campIndex++;
		}
	}
	
	private void ShuffleList(List<CampSimple> list)
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();
		
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.RandiRange(0, i);
			CampSimple temp = list[i];
			list[i] = list[j];
			list[j] = temp;
		}
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
		
		// Vérification périodique de victoire
		_victoryCheckTimer += (float)delta;
		if (_victoryCheckTimer >= VictoryCheckInterval)
		{
			_victoryCheckTimer = 0f;
			CheckVictoryCondition();
		}
	}
	
	private void CheckVictoryCondition()
	{
		if (_allCamps.Count == 0)
			return;
		
		// Compter les camps par équipe (hors neutres)
		Dictionary<int, int> campCountByTeam = new Dictionary<int, int>();
		int nonNeutralCamps = 0;
		
		foreach (var camp in _allCamps)
		{
			if (camp == null || !IsInstanceValid(camp))
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
		GetTree().Root.AddChild(canvasLayer);
		
		// Mettre le jeu en pause
		GetTree().Paused = true;
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
