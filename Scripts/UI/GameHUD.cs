using Godot;
using System.Collections.Generic;

public partial class GameHUD : Control
{
	private Label _goldLabel;
	private Control _goldPanel;
	private SelectionManager _selectionManager;
	private Dictionary<string, TextureButton> _unitButtons = new Dictionary<string, TextureButton>();
	private Dictionary<string, TextureButton> _shipButtons = new Dictionary<string, TextureButton>();
	private Dictionary<string, Label> _lockLabels = new Dictionary<string, Label>();
	private HBoxContainer _unitsContainer;
	private HBoxContainer _shipsContainer;
	private Button _quitButton;
	private Button _territoryButton;
	private HBoxContainer _brushSizeContainer;
	private Button _portButton;
	private Label _tierInfoLabel;
	private Button _unlockTier2Button;

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
		CreateLockLabels();
		CreateTierInfoLabel();
		BindSceneHudControls();

		var networkManager = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (networkManager != null)
		{
			networkManager.PlayerDisconnected += OnPlayerDisconnected;
			networkManager.ServerDisconnected += OnServerDisconnectedHUD;
		}

	}

	private void BindSceneHudControls()
	{
		_quitButton = GetNode<Button>("QuitButton");
		_territoryButton = GetNode<Button>("TerritoryButton");
		_brushSizeContainer = GetNode<HBoxContainer>("BrushSizeContainer");
		_portButton = GetNode<Button>("PortButton");
		_disconnectPanel = GetNode<Panel>("DisconnectPanel");
		_disconnectLabel = GetNode<Label>("DisconnectPanel/DisconnectLabel");

		_quitButton.Text = "✕ Menu";
		UIStyle.ApplyStone(_quitButton);
		_quitButton.Pressed += OnQuitButtonPressed;

		_territoryButton.Text = $"🗺 Territoire ({TerritoryManager.TileCost}g/tuile)";
		_territoryButton.ToggleMode = true;
		UIStyle.ApplyStone(_territoryButton);
		_territoryButton.Toggled += OnTerritoryButtonToggled;

		var brush1 = _brushSizeContainer.GetNode<Button>("Brush1Button");
		var brush3 = _brushSizeContainer.GetNode<Button>("Brush3Button");
		var brush5 = _brushSizeContainer.GetNode<Button>("Brush5Button");
		UIStyle.ApplyStone(brush1);
		UIStyle.ApplyStone(brush3);
		UIStyle.ApplyStone(brush5);
		brush1.Pressed += () => OnBrushSizeButtonPressed(1);
		brush3.Pressed += () => OnBrushSizeButtonPressed(3);
		brush5.Pressed += () => OnBrushSizeButtonPressed(5);

		_portButton.Text = $"⚓ Port ({CampSimple.PortCost}g)";
		UIStyle.ApplyStone(_portButton);
		_portButton.Pressed += OnPortButtonPressed;
		_portButton.Visible = false;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0f, 0f, 0f, 0.6f);
		_disconnectPanel.AddThemeStyleboxOverride("panel", style);
		_disconnectPanel.Visible = false;
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

	private void OnQuitButtonPressed()
	{
		TerritoryManager.Instance?.SetBuyMode(false);
		TerritoryManager.Instance?.CancelPortPlacement();
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	private void OnTerritoryButtonToggled(bool pressed)
	{
		TerritoryManager.Instance?.SetBuyMode(pressed);
		_brushSizeContainer.Visible = pressed;
	}

	private void OnBrushSizeButtonPressed(int size)
	{
		TerritoryManager.Instance?.SetBrushSize(size);
	}

	private void OnPortButtonPressed()
	{
		var camp = _selectionManager?.GetSelectedCamp();
		if (camp == null || !IsInstanceValid(camp)) return;
		if (camp.BuyPort())
			TerritoryManager.Instance?.StartPortPlacement(camp);
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


	private void CreateLockLabels()
	{
		foreach (string unitType in UnitTypes)
		{
			if (!_unitButtons.TryGetValue(unitType, out var btn)) continue;

			var lbl = new Label();
			lbl.Text = "";
			lbl.HorizontalAlignment = HorizontalAlignment.Center;
			lbl.VerticalAlignment = VerticalAlignment.Center;
			lbl.AddThemeFontSizeOverride("font_size", 14);
			lbl.Modulate = new Color(1f, 0.85f, 0.2f, 1f);
			lbl.MouseFilter = Control.MouseFilterEnum.Ignore;

			// Positionné au centre du bouton
			lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			btn.AddChild(lbl);
			_lockLabels[unitType] = lbl;
		}
	}

	private void CreateTierInfoLabel()
	{
		_tierInfoLabel = new Label();
		_tierInfoLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_tierInfoLabel.AddThemeFontSizeOverride("font_size", 13);
		_tierInfoLabel.Modulate = new Color(1f, 0.9f, 0.5f, 1f);
		_tierInfoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_tierInfoLabel.Visible = false;

		_tierInfoLabel.AnchorLeft   = 0f;
		_tierInfoLabel.AnchorTop    = 1f;
		_tierInfoLabel.AnchorRight  = 1f;
		_tierInfoLabel.AnchorBottom = 1f;
		_tierInfoLabel.OffsetLeft   = 10f;
		_tierInfoLabel.OffsetTop    = -145f;
		_tierInfoLabel.OffsetRight  = -10f;
		_tierInfoLabel.OffsetBottom = -115f;
		AddChild(_tierInfoLabel);

		// Bouton d'achat palier 2
		_unlockTier2Button = new Button();
		_unlockTier2Button.Text = $"Débloquer Palier 2 ({GameManager.Tier2Cost}g)";
		_unlockTier2Button.AddThemeFontSizeOverride("font_size", 13);
		UIStyle.ApplyStone(_unlockTier2Button);
		_unlockTier2Button.AnchorLeft   = 0.5f;
		_unlockTier2Button.AnchorTop    = 1f;
		_unlockTier2Button.AnchorRight  = 0.5f;
		_unlockTier2Button.AnchorBottom = 1f;
		_unlockTier2Button.GrowHorizontal = Control.GrowDirection.Both;
		_unlockTier2Button.OffsetLeft   = -120f;
		_unlockTier2Button.OffsetTop    = -165f;
		_unlockTier2Button.OffsetRight  = 120f;
		_unlockTier2Button.OffsetBottom = -135f;
		_unlockTier2Button.Visible = false;
		_unlockTier2Button.Pressed += OnUnlockTier2Pressed;
		AddChild(_unlockTier2Button);
	}

	private void OnUnlockTier2Pressed()
	{
		int teamId = GetLocalTeamId();
		GameManager.Instance?.UnlockTier2(teamId);
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

	private bool ShouldUseRelayCommands()
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		return gameState?.IsOnline == true && NakamaService.Instance?.IsSocketConnected == true;
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

		if (ShouldUseRelayCommands())
		{
			NetworkCommandRouter.RequestBuyUnit(selectedCamp, unitType);
			return;
		}

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

		if (selectedCamp == null || !IsInstanceValid(selectedCamp))
		{
			if (_tierInfoLabel != null) _tierInfoLabel.Visible = false;
			return;
		}
		if (selectedCamp.GetTeamId() != GetLocalTeamId())
		{
			if (_tierInfoLabel != null) _tierInfoLabel.Visible = false;
			return;
		}

		bool queueFull = selectedCamp.GetQueueCount() >= selectedCamp.GetMaxQueueSize();
		int unlockedTier = GameManager.Instance?.GetUnlockedTier(GetLocalTeamId()) ?? 1;

		// Barre d'info palier
		if (_tierInfoLabel != null)
		{
			_tierInfoLabel.Visible = true;
			if (unlockedTier >= 3)
			{
				_tierInfoLabel.Text = "Palier 3/3 — Toutes les unités débloquées ✓";
				if (_unlockTier2Button != null) _unlockTier2Button.Visible = false;
			}
			else if (unlockedTier == 2)
			{
				string regionDesc = GetTier3RegionDescription(GetLocalTeamId());
				_tierInfoLabel.Text = $"Palier 2/3 — Débloquez le palier 3 : capturez tous les camps de {regionDesc}";
				if (_unlockTier2Button != null) _unlockTier2Button.Visible = false;
			}
			else
			{
				int gold = GameManager.Instance?.GetGold(GetLocalTeamId()) ?? 0;
				bool canAfford = gold >= GameManager.Tier2Cost;
				_tierInfoLabel.Text = canAfford
					? $"Palier 1/3 — Vous pouvez débloquer le Palier 2 !"
					: $"Palier 1/3 — Économisez {GameManager.Tier2Cost}g pour débloquer le Palier 2 ({gold}g)";
				if (_unlockTier2Button != null)
				{
					_unlockTier2Button.Visible = true;
					_unlockTier2Button.Disabled = !canAfford;
					_unlockTier2Button.Modulate = canAfford ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);
				}
			}
		}

		foreach (string unitType in UnitTypes)
		{
			if (!_unitButtons.TryGetValue(unitType, out var btn)) continue;

			int requiredTier = GameManager.GetUnitTier(unitType);
			bool locked = unlockedTier < requiredTier;
			bool canBuy = !locked && !queueFull && selectedCamp.CanBuyUnit(unitType);

			btn.Disabled = !canBuy;
			btn.Modulate = locked
				? new Color(0.35f, 0.35f, 0.35f, 0.55f)
				: canBuy ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);

			if (_lockLabels.TryGetValue(unitType, out var lbl))
				lbl.Text = locked ? $"🔒 P{requiredTier}" : "";

			if (locked)
				btn.TooltipText = requiredTier == 2
					? $"🔒 Palier 2 : achetez l'amélioration ({GameManager.Tier2Cost}g)"
					: "🔒 Palier 3 : capturez tous les camps de votre région de départ";
			else if (queueFull)
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

		int unlockedTier = GameManager.Instance?.GetUnlockedTier(GetLocalTeamId()) ?? 1;

		foreach (string shipType in ShipTypes)
		{
			if (!_shipButtons.TryGetValue(shipType, out var btn)) continue;

			int requiredTier = GameManager.GetShipTier(shipType);
			bool locked = unlockedTier < requiredTier;
			bool canBuy = !locked && selectedPort.CanBuyShip(shipType);

			btn.Disabled = !canBuy;
			btn.Modulate = locked
				? new Color(0.35f, 0.35f, 0.35f, 0.55f)
				: canBuy ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);

			if (locked)
				btn.TooltipText = "🔒 Capturez toute votre région de départ + construisez un port";
			else if (!canBuy)
			{
				int queueCount = selectedPort.GetShipQueueCount();
				int maxQueue = selectedPort.GetMaxShipQueueSize();
				if (queueCount >= maxQueue)
					btn.TooltipText = "File navale pleine !";
				else
					btn.TooltipText = $"Or insuffisant ({ShipStats.GetStats(shipType).Price}g requis)";
			}
			else
				btn.TooltipText = "";
		}
	}

	private string GetTier3RegionDescription(int teamId)
	{
		if (GameManager.Instance == null) return "contrôlez tous les camps de votre région de départ";

		int homeRegion = GameManager.Instance.GetHomeRegion(teamId);
		if (homeRegion < 0) return "contrôlez tous les camps de votre région de départ";

		var allCamps = GameManager.Instance.GetAllCamps();
		var homeCamps = allCamps.FindAll(c => c.RegionId == homeRegion);
		int total = homeCamps.Count;
		int owned = homeCamps.FindAll(c => c.GetTeamId() == teamId).Count;

		if (owned == total && total >= 2)
			return $"région de départ complète ✓ ({owned}/{total})";

		return $"contrôlez les {total} camps de votre région de départ ({owned}/{total})";
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
