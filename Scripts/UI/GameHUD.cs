using Godot;
using System.Collections.Generic;

public partial class GameHUD : Control
{
	private Label _goldLabel;
	private Control _goldPanel;
	private SelectionManager _selectionManager;
	private Dictionary<string, TextureButton> _unitButtons = new Dictionary<string, TextureButton>();

	// Liste des types d'unités disponibles dans le HUD
	private static readonly string[] UnitTypes = new[]
	{
		"Infantry", "Support", "Heal", "Range",
		"AntiArmor", "Heavy", "Mortar", "Tank"
	};


	public override void _Ready()
	{
		// Récupérer les éléments UI
		_goldPanel = GetNode<Control>("GoldPanel");
		_goldLabel = GetNode<Label>("GoldPanel/HBoxContainer/GoldLabel");

		// Toujours visible
		_goldPanel.Visible = true;
		_goldLabel.Text = "0";

		// Connecter les boutons d'unités
		ConnectUnitButtons();

		GD.Print("[HUD] GameHUD initialisé");
	}

	private void ConnectUnitButtons()
	{
		var unitsContainer = GetNode<HBoxContainer>("NinePatchRect/UnitsContainer");

		foreach (string unitType in UnitTypes)
		{
			var buttonPath = $"{unitType}/Button";
			var button = unitsContainer.GetNodeOrNull<TextureButton>(buttonPath);

			if (button != null)
			{
				_unitButtons[unitType] = button;
				// Capturer le type d'unité pour le callback
				string capturedType = unitType;
				button.Pressed += () => OnUnitButtonPressed(capturedType);
				GD.Print($"[HUD] Bouton {unitType} connecté");
			}
			else
			{
				GD.PrintErr($"[HUD] Bouton {unitType} non trouvé!");
			}
		}
	}

	private void OnUnitButtonPressed(string unitType)
	{
		if (_selectionManager == null)
		{
			GD.Print("[HUD] Pas de SelectionManager!");
			return;
		}

		var selectedCamp = _selectionManager.GetSelectedCamp();

		if (selectedCamp == null || !IsInstanceValid(selectedCamp))
		{
			GD.Print("[HUD] Aucun camp sélectionné!");
			return;
		}

		// Tenter d'acheter l'unité sur le camp sélectionné
		bool success = selectedCamp.BuyUnit(unitType);

		if (success)
		{
			GD.Print($"[HUD] Unité {unitType} achetée sur le camp #{selectedCamp.CampId}");
		}
		else
		{
			GD.Print($"[HUD] Impossible d'acheter {unitType} - pas assez d'or");
		}
	}

	public override void _Process(double delta)
	{
		// Chercher le SelectionManager si pas encore trouvé
		if (_selectionManager == null)
		{
			FindSelectionManager();
		}

		UpdateGoldDisplay();
	}

	private void FindSelectionManager()
	{
		var currentScene = GetTree().CurrentScene;
		if (currentScene == null)
			return;

		// Chercher récursivement dans la scène
		_selectionManager = currentScene.FindChild("SelectionManager", true, false) as SelectionManager;

		if (_selectionManager != null)
		{
			GD.Print("GameHUD: SelectionManager trouvé!");
		}
	}

	private void UpdateGoldDisplay()
	{
		// Pas de camp sélectionné ou pas de SelectionManager → afficher 0
		if (_selectionManager == null)
		{
			_goldLabel.Text = "0";
			return;
		}

		if (GameManager.Instance == null)
		{
			_goldLabel.Text = "0";
			GD.Print("[HUD] GameManager.Instance est NULL!");
			return;
		}

		var selectedCamp = _selectionManager.GetSelectedCamp();

		if (selectedCamp != null && IsInstanceValid(selectedCamp))
		{
			// Camp sélectionné → afficher l'or du camp
			int gold = selectedCamp.GetGold();
			_goldLabel.Text = $"{gold}";
		}
		else
		{
			// Pas de camp sélectionné → afficher 0
			_goldLabel.Text = "0";
		}
	}

}
