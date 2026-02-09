using Godot;
using System.Collections.Generic;

public partial class GameHUD : Control
{
	private Label _goldLabel;
	private Control _goldPanel;
	private SelectionManager _selectionManager;
	private Dictionary<string, TextureButton> _unitButtons = new Dictionary<string, TextureButton>();
	private Dictionary<string, TextureButton> _shipButtons = new Dictionary<string, TextureButton>();
	private HBoxContainer _unitsContainer;
	private HBoxContainer _shipsContainer;

	// Liste des types d'unités disponibles dans le HUD
	private static readonly string[] UnitTypes = new[]
	{
		"Infantry", "Support", "Heal", "Range",
		"AntiArmor", "Heavy", "Mortar", "Tank"
	};

	private static readonly string[] ShipTypes = new[]
	{
		"Destroyer", "Fregate", "Transport"
	};

	public override void _Ready()
	{
		// Récupérer les éléments UI
		_goldPanel = GetNode<Control>("GoldPanel");
		_goldLabel = GetNode<Label>("GoldPanel/HBoxContainer/GoldLabel");

		// Toujours visible
		_goldPanel.Visible = true;
		_goldLabel.Text = "0";

		// Recuperer les conteneurs
		_unitsContainer = GetNode<HBoxContainer>("NinePatchRect/UnitsContainer");
		_shipsContainer = GetNode<HBoxContainer>("NinePatchRect/ShipsContainer");

		// Connecter les boutons d'unités et de bateaux
		ConnectUnitButtons();
		ConnectShipButtons();

		GD.Print("[HUD] GameHUD initialisé");
	}

	private void ConnectUnitButtons()
	{
		foreach (string unitType in UnitTypes)
		{
			var buttonPath = $"{unitType}/Button";
			var button = _unitsContainer.GetNodeOrNull<TextureButton>(buttonPath);

			if (button != null)
			{
				_unitButtons[unitType] = button;
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

	private void ConnectShipButtons()
	{
		foreach (string shipType in ShipTypes)
		{
			var buttonPath = $"{shipType}/Button";
			var button = _shipsContainer.GetNodeOrNull<TextureButton>(buttonPath);

			if (button != null)
			{
				_shipButtons[shipType] = button;
				string capturedType = shipType;
				button.Pressed += () => OnShipButtonPressed(capturedType);
				GD.Print($"[HUD] Bouton bateau {shipType} connecté");
			}
			else
			{
				GD.PrintErr($"[HUD] Bouton bateau {shipType} non trouvé!");
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

		// Vérifier pourquoi l'achat pourrait échouer
		int totalInQueue = selectedCamp.GetQueueCount();
		int maxQueue = selectedCamp.GetMaxQueueSize();

		if (totalInQueue >= maxQueue)
		{
			GD.Print($"[HUD] Impossible d'acheter {unitType} - file d'attente pleine ({totalInQueue}/{maxQueue})");
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

	private void OnShipButtonPressed(string shipType)
	{
		if (_selectionManager == null)
		{
			GD.Print("[HUD] Pas de SelectionManager!");
			return;
		}

		var selectedPort = _selectionManager.GetSelectedPort();

		if (selectedPort == null || !IsInstanceValid(selectedPort))
		{
			GD.Print("[HUD] Aucun port sélectionné!");
			return;
		}

		bool success = selectedPort.BuyShip(shipType);

		if (success)
		{
			GD.Print($"[HUD] Bateau {shipType} acheté sur le port du camp #{selectedPort.CampId}");
		}
		else
		{
			GD.Print($"[HUD] Impossible d'acheter {shipType}");
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
		UpdateContainerVisibility();
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

	private void UpdateContainerVisibility()
	{
		if (_selectionManager == null) return;

		var selectedCamp = _selectionManager.GetSelectedCamp();
		var selectedPort = _selectionManager.GetSelectedPort();

		if (selectedPort != null && IsInstanceValid(selectedPort))
		{
			// Port selectionne -> afficher bateaux, masquer unites
			_shipsContainer.Visible = true;
			_unitsContainer.Visible = false;
		}
		else if (selectedCamp != null && IsInstanceValid(selectedCamp))
		{
			// Camp selectionne -> afficher unites, masquer bateaux
			_unitsContainer.Visible = true;
			_shipsContainer.Visible = false;
		}
		else
		{
			// Rien selectionne -> masquer les deux
			_unitsContainer.Visible = false;
			_shipsContainer.Visible = false;
		}
	}

	private void UpdateGoldDisplay()
	{
		if (_selectionManager == null)
		{
			_goldLabel.Text = "0";
			return;
		}

		if (GameManager.Instance == null)
		{
			_goldLabel.Text = "0";
			return;
		}

		var selectedCamp = _selectionManager.GetSelectedCamp();
		var selectedPort = _selectionManager.GetSelectedPort();

		if (selectedPort != null && IsInstanceValid(selectedPort))
		{
			int gold = selectedPort.GetGold();
			_goldLabel.Text = $"{gold}";
		}
		else if (selectedCamp != null && IsInstanceValid(selectedCamp))
		{
			int gold = selectedCamp.GetGold();
			_goldLabel.Text = $"{gold}";
		}
		else
		{
			_goldLabel.Text = "0";
		}
	}
}
