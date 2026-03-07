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
	private Button _territoryButton;
	private HBoxContainer _brushSizeContainer;
	private Button _portButton;

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
		CreateQuitButton();
		CreateTerritoryButton();
		CreatePortButton();

		GD.Print("[HUD] GameHUD initialisé");
	}

	private void CreateQuitButton()
	{
		var btn = new Button();
		btn.Text = "✕ Menu";
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.AnchorLeft   = 1f;
		btn.AnchorTop    = 0f;
		btn.AnchorRight  = 1f;
		btn.AnchorBottom = 0f;
		btn.OffsetLeft   = -120f;
		btn.OffsetTop    = 10f;
		btn.OffsetRight  = -10f;
		btn.OffsetBottom = 45f;
		btn.Pressed += () =>
		{
			TerritoryManager.Instance?.SetBuyMode(false);
			TerritoryManager.Instance?.CancelPortPlacement();
			GetTree().Paused = false;
			GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
		};
		AddChild(btn);
	}

	private void CreateTerritoryButton()
	{
		_territoryButton = new Button();
		_territoryButton.Text = $"🗺 Territoire ({TerritoryManager.TileCost}g/tuile)";
		_territoryButton.AddThemeFontSizeOverride("font_size", 15);
		_territoryButton.ToggleMode = true;
		_territoryButton.AnchorLeft   = 1f;
		_territoryButton.AnchorTop    = 0f;
		_territoryButton.AnchorRight  = 1f;
		_territoryButton.AnchorBottom = 0f;
		_territoryButton.OffsetLeft   = -300f;
		_territoryButton.OffsetTop    = 10f;
		_territoryButton.OffsetRight  = -130f;
		_territoryButton.OffsetBottom = 45f;
		_territoryButton.Toggled += (pressed) =>
		{
			TerritoryManager.Instance?.SetBuyMode(pressed);
			_brushSizeContainer.Visible = pressed;
		};
		AddChild(_territoryButton);

		_brushSizeContainer = new HBoxContainer();
		_brushSizeContainer.AnchorLeft   = 1f;
		_brushSizeContainer.AnchorTop    = 0f;
		_brushSizeContainer.AnchorRight  = 1f;
		_brushSizeContainer.AnchorBottom = 0f;
		_brushSizeContainer.OffsetLeft   = -300f;
		_brushSizeContainer.OffsetTop    = 50f;
		_brushSizeContainer.OffsetRight  = -130f;
		_brushSizeContainer.OffsetBottom = 85f;
		_brushSizeContainer.Visible = false;
		AddChild(_brushSizeContainer);

		foreach (var (label, size) in new[] { ("1×1", 1), ("3×3", 3), ("5×5", 5) })
		{
			int s = size; string l = label;
			var sizeBtn = new Button();
			sizeBtn.Text = l;
			sizeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			sizeBtn.Pressed += () => TerritoryManager.Instance?.SetBrushSize(s);
			_brushSizeContainer.AddChild(sizeBtn);
		}
	}

	private void CreatePortButton()
	{
		_portButton = new Button();
		_portButton.Text = $"⚓ Port ({CampSimple.PortCost}g)";
		_portButton.AddThemeFontSizeOverride("font_size", 16);
		_portButton.AnchorLeft   = 0.5f;
		_portButton.AnchorTop    = 1f;
		_portButton.AnchorRight  = 0.5f;
		_portButton.AnchorBottom = 1f;
		_portButton.OffsetLeft   = -80f;
		_portButton.OffsetTop    = -110f;
		_portButton.OffsetRight  = 80f;
		_portButton.OffsetBottom = -75f;
		_portButton.Visible = false;
		_portButton.Pressed += () =>
		{
			var camp = _selectionManager?.GetSelectedCamp();
			if (camp == null || !IsInstanceValid(camp)) return;
			if (camp.BuyPort())
				TerritoryManager.Instance?.StartPortPlacement(camp);
		};
		AddChild(_portButton);
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

	private int GetLocalTeamId()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		return gameState?.LocalTeamId ?? 1;
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

		// Reseau : ne pas acheter sur un camp qui ne nous appartient pas
		if (selectedCamp.GetTeamId() != GetLocalTeamId())
		{
			GD.Print($"[HUD] Ce camp appartient a l'equipe {selectedCamp.GetTeamId()}, pas a nous ({GetLocalTeamId()})");
			return;
		}

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

		// Reseau : ne pas acheter sur un port qui ne nous appartient pas
		if (selectedPort.GetTeamId() != GetLocalTeamId())
		{
			GD.Print($"[HUD] Ce port appartient a l'equipe {selectedPort.GetTeamId()}, pas a nous ({GetLocalTeamId()})");
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

		// Bouton port : visible si camp allie sans port selectionne
		if (_portButton != null)
		{
			bool showPort = selectedCamp != null && IsInstanceValid(selectedCamp)
				&& selectedCamp.GetTeamId() == GetLocalTeamId()
				&& !selectedCamp.HasPort
				&& !selectedCamp.IsNeutralCamp;
			_portButton.Visible = showPort;
			if (showPort)
				_portButton.Disabled = !selectedCamp.CanBuyPort();
		}
	}

	private void UpdateGoldDisplay()
	{
		if (GameManager.Instance == null)
		{
			_goldLabel.Text = "0";
			return;
		}

		// Toujours afficher l'or de notre equipe
		int localTeam = GetLocalTeamId();
		int gold = GameManager.Instance.GetGold(localTeam);
		_goldLabel.Text = $"{gold}";
	}
}
