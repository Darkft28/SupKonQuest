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

		private Panel _disconnectPanel;
	private Label _disconnectLabel;

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
		_goldPanel = GetNode<Control>("GoldPanel");
		_goldLabel = GetNode<Label>("GoldPanel/HBoxContainer/GoldLabel");

		_goldPanel.Visible = true;
		_goldLabel.Text = "0";

		_unitsContainer = GetNode<HBoxContainer>("NinePatchRect/UnitsContainer");
		_shipsContainer = GetNode<HBoxContainer>("NinePatchRect/ShipsContainer");

		UpdatePriceLabels();

		ConnectUnitButtons();
		ConnectShipButtons();
		CreateQuitButton();
		CreateTerritoryButton();
		CreatePortButton();

		CreateDisconnectOverlay();

		var networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager != null)
		{
			networkManager.PlayerDisconnected += OnPlayerDisconnected;
			networkManager.ServerDisconnected += OnServerDisconnectedHUD;
		}

	}

	private void UpdatePriceLabels()
	{
		foreach (string unitType in UnitTypes)
		{
			var priceLabel = _unitsContainer.GetNodeOrNull<Label>($"{unitType}/PriceContainer/Price");
			if (priceLabel != null)
			{
				priceLabel.Text = UnitStats.GetStats(unitType).Price.ToString();
			}
			else
			{
				GD.PrintErr($"[HUD] Label de prix introuvable pour l'unité: {unitType}");
			}
		}

		foreach (string shipType in ShipTypes)
		{
			var priceLabel = _shipsContainer.GetNodeOrNull<Label>($"{shipType}/PriceContainer/Price");
			if (priceLabel != null)
			{
				priceLabel.Text = ShipStats.GetStats(shipType).Price.ToString();
			}
			else
			{
				GD.PrintErr($"[HUD] Label de prix introuvable pour le bateau: {shipType}");
			}
		}

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

	private void CreateDisconnectOverlay()
	{
		_disconnectPanel = new Panel();
		_disconnectPanel.SetAnchorsPreset(LayoutPreset.FullRect);
		_disconnectPanel.MouseFilter = MouseFilterEnum.Ignore;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0f, 0f, 0f, 0.6f);
		_disconnectPanel.AddThemeStyleboxOverride("panel", style);

		_disconnectLabel = new Label();
		_disconnectLabel.SetAnchorsPreset(LayoutPreset.Center);
		_disconnectLabel.GrowHorizontal = GrowDirection.Both;
		_disconnectLabel.GrowVertical = GrowDirection.Both;
		_disconnectLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_disconnectLabel.VerticalAlignment = VerticalAlignment.Center;
		_disconnectLabel.AddThemeFontSizeOverride("font_size", 28);
		_disconnectLabel.Modulate = new Color(1f, 0.3f, 0.3f, 1f);
		_disconnectLabel.Text = "Adversaire déconnecté\nRetour au menu dans 5s...";

		_disconnectPanel.AddChild(_disconnectLabel);

		AddChild(_disconnectPanel);
		_disconnectPanel.Visible = false;
	}

	private void OnPlayerDisconnected(long id)
	{
		ShowDisconnectMessage("Adversaire déconnecté\nRetour au menu dans 5s...");
	}

	private void OnServerDisconnectedHUD()
	{
		ShowDisconnectMessage("Connexion au serveur perdue\nRetour au menu dans 5s...");
	}

	private void ShowDisconnectMessage(string message)
	{
		if (_disconnectPanel == null || _disconnectLabel == null) return;
		_disconnectLabel.Text = message;
		_disconnectPanel.Visible = true;
	}

	public override void _ExitTree()
	{
		var networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager != null)
		{
			networkManager.PlayerDisconnected -= OnPlayerDisconnected;
			networkManager.ServerDisconnected -= OnServerDisconnectedHUD;
		}
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
		if (_selectionManager == null) return;

		var selectedCamp = _selectionManager.GetSelectedCamp();
		if (selectedCamp == null || !IsInstanceValid(selectedCamp)) return;

		if (selectedCamp.GetTeamId() != GetLocalTeamId()) return;

		int totalInQueue = selectedCamp.GetQueueCount();
		int maxQueue = selectedCamp.GetMaxQueueSize();

		if (totalInQueue >= maxQueue) return;

		selectedCamp.BuyUnit(unitType);
	}

	private void OnShipButtonPressed(string shipType)
	{
		if (_selectionManager == null) return;

		var selectedPort = _selectionManager.GetSelectedPort();
		if (selectedPort == null || !IsInstanceValid(selectedPort)) return;

		if (selectedPort.GetTeamId() != GetLocalTeamId()) return;

		selectedPort.BuyShip(shipType);
	}

	public override void _Process(double delta)
	{
		if (_selectionManager == null)
		{
			FindSelectionManager();
		}

		UpdateGoldDisplay();
		UpdateContainerVisibility();
		UpdateUnitButtons();
		UpdateShipButtons();
	}

	private void UpdateUnitButtons()
	{
		if (_selectionManager == null) return;

		var selectedCamp = _selectionManager.GetSelectedCamp();

		if (selectedCamp == null || !IsInstanceValid(selectedCamp)) return;
		if (selectedCamp.GetTeamId() != GetLocalTeamId()) return;

		bool queueFull = selectedCamp.GetQueueCount() >= selectedCamp.GetMaxQueueSize();

		foreach (string unitType in UnitTypes)
		{
			if (!_unitButtons.TryGetValue(unitType, out var btn)) continue;

			bool canBuy = !queueFull && selectedCamp.CanBuyUnit(unitType);

			btn.Disabled = !canBuy;
			btn.Modulate = canBuy ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);

			if (queueFull)
				btn.TooltipText = "File de production pleine !";
			else if (!selectedCamp.CanBuyUnit(unitType))
				btn.TooltipText = $"Or insuffisant ({UnitStats.GetStats(unitType).Price}g requis)";
			else
				btn.TooltipText = "";
		}
	}

	private void UpdateShipButtons()
	{
		if (_selectionManager == null) return;

		var selectedPort = _selectionManager.GetSelectedPort();

		if (selectedPort == null || !IsInstanceValid(selectedPort)) return;
		if (selectedPort.GetTeamId() != GetLocalTeamId()) return;

		foreach (string shipType in ShipTypes)
		{
			if (!_shipButtons.TryGetValue(shipType, out var btn)) continue;

			bool canBuy = selectedPort.CanBuyShip(shipType);

			btn.Disabled = !canBuy;
			btn.Modulate = canBuy ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);

			if (!canBuy)
			{
				int queueCount = selectedPort.GetShipQueueCount();
				int maxQueue = selectedPort.GetMaxShipQueueSize();
				if (queueCount >= maxQueue)
					btn.TooltipText = "File navale pleine !";
				else
					btn.TooltipText = $"Or insuffisant ({ShipStats.GetStats(shipType).Price}g requis)";
			}
			else
			{
				btn.TooltipText = "";
			}
		}
	}

	private void FindSelectionManager()
	{
		var currentScene = GetTree().CurrentScene;
		if (currentScene == null)
			return;

		_selectionManager = currentScene.FindChild("SelectionManager", true, false) as SelectionManager;
	}

	private void UpdateContainerVisibility()
	{
		if (_selectionManager == null) return;

		var selectedCamp = _selectionManager.GetSelectedCamp();
		var selectedPort = _selectionManager.GetSelectedPort();

		if (selectedPort != null && IsInstanceValid(selectedPort))
		{
			_shipsContainer.Visible = true;
			_unitsContainer.Visible = false;
		}
		else if (selectedCamp != null && IsInstanceValid(selectedCamp))
		{
			_unitsContainer.Visible = true;
			_shipsContainer.Visible = false;
		}
		else
		{
			_unitsContainer.Visible = false;
			_shipsContainer.Visible = false;
		}

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

		int localTeam = GetLocalTeamId();
		int gold = GameManager.Instance.GetGold(localTeam);
		_goldLabel.Text = $"{gold}";
	}
}
