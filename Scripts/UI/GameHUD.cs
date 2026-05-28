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
	private Button _portButton;
	private Label _tierInfoLabel;
	private Button _unlockTier2Button;
	private HBoxContainer _abilitiesContainer;
	private Button _healUltimateButton;
	private Button _supportUltimateButton;

	private Panel _disconnectPanel;
	private Label _disconnectLabel;
	private Panel _leaderboardPanel;
	private VBoxContainer _leaderboardVBox;
	private Label _leaderboardTitle;
	private Label _leaderboardRows;
	private VBoxContainer _leaderboardRowsContainer;
	private Button _leaderboardToggleBtn;
	private HSeparator _leaderboardSeparator;
	private bool _leaderboardExpanded = true;
	private int  _leaderboardLastLineCount = 0;
	private float _leaderboardRefreshTimer = 0f;
	private const float LeaderboardRefreshInterval = 0.5f;
	private const float LeaderboardCollapsedHeight = 50f;

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
		CreateAbilityButtons();

		var nakama = GetNodeOrNull<NakamaService>("/root/NakamaService");
		if (nakama != null)
			nakama.Disconnected += OnNakamaDisconnected;

		var gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
		if (gameManager != null)
			gameManager.OnlinePlayerLeft += OnOnlinePlayerLeft;
	}

	private void BindSceneHudControls()
	{
		_quitButton = GetNode<Button>("QuitButton");
		_portButton = GetNode<Button>("PortButton");
		_disconnectPanel = GetNode<Panel>("DisconnectPanel");
		_disconnectLabel = GetNode<Label>("DisconnectPanel/DisconnectLabel");
		_leaderboardPanel = GetNodeOrNull<Panel>("LeaderboardPanel");
		_leaderboardVBox = GetNodeOrNull<VBoxContainer>("LeaderboardPanel/LeaderboardVBox");
		_leaderboardTitle = GetNodeOrNull<Label>("LeaderboardPanel/LeaderboardVBox/LeaderboardTitle");
		_leaderboardRows = GetNodeOrNull<Label>("LeaderboardPanel/LeaderboardVBox/LeaderboardRows");

		_quitButton.Text = "✕ Menu";
		UIStyle.ApplyStone(_quitButton);
		_quitButton.Pressed += OnQuitButtonPressed;

		_portButton.Text = $"⚓ Port ({CampSimple.PortCost}g)";
		UIStyle.ApplyStone(_portButton);
		_portButton.Pressed += OnPortButtonPressed;
		_portButton.Visible = false;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0f, 0f, 0f, 0.6f);
		_disconnectPanel.AddThemeStyleboxOverride("panel", style);
		_disconnectPanel.Visible = false;

		SetupLeaderboardUi();
	}

	private void SetupLeaderboardUi()
	{
		if (_leaderboardPanel == null || _leaderboardVBox == null || _leaderboardTitle == null || _leaderboardRows == null)
			return;

		_leaderboardPanel.AnchorLeft = 0f;
		_leaderboardPanel.AnchorTop = 0f;
		_leaderboardPanel.AnchorRight = 0f;
		_leaderboardPanel.AnchorBottom = 0f;
		_leaderboardPanel.OffsetLeft = 12f;
		_leaderboardPanel.OffsetTop = 12f;
		_leaderboardPanel.OffsetRight = 316f;
		_leaderboardPanel.OffsetBottom = _leaderboardPanel.OffsetTop + LeaderboardCollapsedHeight;

		// Fond sombre chaud + bordure dorée nette (sans texture étirée)
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.07f, 0.05f, 0.03f, 0.92f);
		panelStyle.BorderColor = new Color(1f, 0.88f, 0.42f, 1f);
		panelStyle.BorderWidthLeft   = 2;
		panelStyle.BorderWidthTop    = 2;
		panelStyle.BorderWidthRight  = 2;
		panelStyle.BorderWidthBottom = 2;
		panelStyle.CornerRadiusTopLeft     = 6;
		panelStyle.CornerRadiusTopRight    = 6;
		panelStyle.CornerRadiusBottomLeft  = 6;
		panelStyle.CornerRadiusBottomRight = 6;
		panelStyle.ContentMarginLeft   = 20f;
		panelStyle.ContentMarginTop    = 8f;
		panelStyle.ContentMarginRight  = 16f;
		panelStyle.ContentMarginBottom = 14f;
		_leaderboardPanel.AddThemeStyleboxOverride("panel", panelStyle);

		_leaderboardVBox.AnchorLeft = 0f;
		_leaderboardVBox.AnchorTop = 0f;
		_leaderboardVBox.AnchorRight = 1f;
		_leaderboardVBox.AnchorBottom = 1f;
		_leaderboardVBox.OffsetLeft = 0f;
		_leaderboardVBox.OffsetTop = 0f;
		_leaderboardVBox.OffsetRight = 0f;
		_leaderboardVBox.OffsetBottom = 0f;
		_leaderboardVBox.AddThemeConstantOverride("separation", 4);

		// Bouton-titre rétractable : transparent, hover gold subtil
		_leaderboardTitle.Visible = false;
		string titleText = LocalizationManager.Instance?.GetText("ranking_title") ?? "⚔  Classement";
		_leaderboardToggleBtn = new Button();
		_leaderboardToggleBtn.Text = "▼  " + titleText;
		_leaderboardToggleBtn.AddThemeFontSizeOverride("font_size", 15);
		_leaderboardToggleBtn.AddThemeColorOverride("font_color",         new Color(1f, 0.88f, 0.42f, 1f));
		_leaderboardToggleBtn.AddThemeColorOverride("font_hover_color",   new Color(1f, 0.97f, 0.75f, 1f));
		_leaderboardToggleBtn.AddThemeColorOverride("font_pressed_color", new Color(0.88f, 0.62f, 0.18f, 1f));
		var btnBase    = new StyleBoxFlat(); btnBase.BgColor    = new Color(0f, 0f, 0f, 0f);
		var btnHover   = new StyleBoxFlat(); btnHover.BgColor   = new Color(1f, 0.88f, 0.42f, 0.10f);
		var btnPressed = new StyleBoxFlat(); btnPressed.BgColor = new Color(1f, 0.88f, 0.42f, 0.18f);
		btnBase.ContentMarginLeft   = btnHover.ContentMarginLeft   = btnPressed.ContentMarginLeft   = 4f;
		btnBase.ContentMarginRight  = btnHover.ContentMarginRight  = btnPressed.ContentMarginRight  = 4f;
		btnBase.ContentMarginTop    = btnHover.ContentMarginTop    = btnPressed.ContentMarginTop    = 4f;
		btnBase.ContentMarginBottom = btnHover.ContentMarginBottom = btnPressed.ContentMarginBottom = 4f;
		_leaderboardToggleBtn.AddThemeStyleboxOverride("normal",   btnBase);
		_leaderboardToggleBtn.AddThemeStyleboxOverride("hover",    btnHover);
		_leaderboardToggleBtn.AddThemeStyleboxOverride("pressed",  btnPressed);
		_leaderboardToggleBtn.AddThemeStyleboxOverride("focus",    btnBase);
		_leaderboardToggleBtn.AddThemeStyleboxOverride("disabled", btnBase);
		_leaderboardVBox.AddChild(_leaderboardToggleBtn);
		_leaderboardVBox.MoveChild(_leaderboardToggleBtn, 0);
		_leaderboardToggleBtn.Pressed += OnLeaderboardTogglePressed;

		// Séparateur doré
		_leaderboardSeparator = new HSeparator();
		var sepStyle = new StyleBoxFlat();
		sepStyle.BgColor = new Color(1f, 0.88f, 0.42f, 0.8f);
		sepStyle.ContentMarginTop    = 1f;
		sepStyle.ContentMarginBottom = 1f;
		_leaderboardSeparator.AddThemeStyleboxOverride("separator", sepStyle);
		_leaderboardSeparator.AddThemeConstantOverride("separation", 1);
		_leaderboardVBox.AddChild(_leaderboardSeparator);
		_leaderboardVBox.MoveChild(_leaderboardSeparator, 1);

		// On cache le label original et on utilise un conteneur de lignes structurées
		_leaderboardRows.Visible = false;
		_leaderboardRowsContainer = new VBoxContainer();
		_leaderboardRowsContainer.AddThemeConstantOverride("separation", 3);
		_leaderboardVBox.AddChild(_leaderboardRowsContainer);
	}

	private void OnLeaderboardTogglePressed()
	{
		_leaderboardExpanded = !_leaderboardExpanded;
		_leaderboardRowsContainer.Visible = _leaderboardExpanded;
		_leaderboardSeparator.Visible     = _leaderboardExpanded;

		string titleText = LocalizationManager.Instance?.GetText("ranking_title") ?? "⚔  Classement";
		_leaderboardToggleBtn.Text = (_leaderboardExpanded ? "▼  " : "▶  ") + titleText;

		if (_leaderboardExpanded)
			UpdateLeaderboardPanelHeight(_leaderboardLastLineCount);
		else
			_leaderboardPanel.OffsetBottom = _leaderboardPanel.OffsetTop + LeaderboardCollapsedHeight;

		_leaderboardToggleBtn.ReleaseFocus();
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
		TerritoryManager.Instance?.CancelPortPlacement();
		GetTree().Paused = false;
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState != null)
			gameState.ReturnToMainMenu();
		else
			GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	private void OnPortButtonPressed()
	{
		var camp = _selectionManager?.GetSelectedCamp();
		if (camp == null || !IsInstanceValid(camp)) return;
		if (camp.BuyPort())
			TerritoryManager.Instance?.StartPortPlacement(camp);
	}

	private void OnNakamaDisconnected()
	{
		ShowDisconnectMessage("Connexion au serveur perdue\nRetour au menu dans 5s...");
	}

	private void OnOnlinePlayerLeft(int teamId)
	{
		if (teamId == GetLocalTeamId())
			return;

		ShowDisconnectMessage("Adversaire déconnecté\nRetour au menu dans 5s...");
	}

	private void ShowDisconnectMessage(string message)
	{
		if (_disconnectPanel == null || _disconnectLabel == null) return;
		_disconnectLabel.Text = message;
		_disconnectPanel.Visible = true;

		GetTree().CreateTimer(5.0).Timeout += () =>
		{
			if (IsInstanceValid(this))
				GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
		};
	}

	public override void _ExitTree()
	{
		var nakama = GetNodeOrNull<NakamaService>("/root/NakamaService");
		if (nakama != null)
			nakama.Disconnected -= OnNakamaDisconnected;

		var gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
		if (gameManager != null)
			gameManager.OnlinePlayerLeft -= OnOnlinePlayerLeft;
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

	private static bool ShouldUseRelayCommands() => GameState.IsOnlineMultiplayer;

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

		if (!selectedCamp.BuyUnit(unitType))
			GD.Print($"[HUD] Achat {unitType} refusé : {selectedCamp.GetBuyUnitDenyReason(unitType)}");
	}

	private void OnShipButtonPressed(string shipType)
	{
		if (_selectionManager == null) return;

		var selectedPort = _selectionManager.GetSelectedPort();
		if (selectedPort == null || !IsInstanceValid(selectedPort)) return;

		if (selectedPort.GetTeamId() != GetLocalTeamId()) return;

		if (ShouldUseRelayCommands())
		{
			NetworkCommandRouter.RequestBuyShip(selectedPort, shipType);
			return;
		}

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
		UpdateAbilityButtons();
		HandleAbilityHotkeys();
		UpdateLeaderboard(delta);
	}

	private void CreateAbilityButtons()
	{
		_abilitiesContainer = new HBoxContainer();
		_abilitiesContainer.AnchorLeft = 0.5f;
		_abilitiesContainer.AnchorTop = 1f;
		_abilitiesContainer.AnchorRight = 0.5f;
		_abilitiesContainer.AnchorBottom = 1f;
		_abilitiesContainer.OffsetLeft = -260f;
		_abilitiesContainer.OffsetTop = -210f;
		_abilitiesContainer.OffsetRight = 260f;
		_abilitiesContainer.OffsetBottom = -170f;
		_abilitiesContainer.Alignment = BoxContainer.AlignmentMode.Center;
		_abilitiesContainer.AddThemeConstantOverride("separation", 8);
		AddChild(_abilitiesContainer);

		_healUltimateButton = new Button();
		_healUltimateButton.Text = BuildUltimateReadyLabel("Heal Ult", "ultimate_heal");
		_healUltimateButton.Pressed += () => StartAbilityTargeting("heal_ultimate");
		_abilitiesContainer.AddChild(_healUltimateButton);

		_supportUltimateButton = new Button();
		_supportUltimateButton.Text = BuildUltimateReadyLabel("Support Ult", "ultimate_support");
		_supportUltimateButton.Pressed += () => StartAbilityTargeting("support_ultimate");
		_abilitiesContainer.AddChild(_supportUltimateButton);

		_abilitiesContainer.Visible = false;
	}

	private void HandleAbilityHotkeys()
	{
		if (_selectionManager == null)
			return;

		if (Input.IsActionJustPressed("ultimate_heal"))
			StartAbilityTargeting("heal_ultimate");
		else if (Input.IsActionJustPressed("ultimate_support"))
			StartAbilityTargeting("support_ultimate");
		else if (Input.IsActionJustPressed("ultimate_cancel"))
			_selectionManager.CancelAbilityTargeting();
	}

	private void StartAbilityTargeting(string abilityId)
	{
		if (_selectionManager == null || string.IsNullOrWhiteSpace(abilityId))
			return;
		if (GameManager.Instance != null && !GameManager.Instance.CanUseTeamUltimate(GetLocalTeamId(), abilityId))
			return;

		_selectionManager.BeginAbilityTargeting(abilityId);
	}

	private void UpdateAbilityButtons()
	{
		if (_abilitiesContainer == null || _selectionManager == null)
			return;

		_abilitiesContainer.Visible = true;

		int localTeamId = GetLocalTeamId();
		float healCooldown = GameManager.Instance?.GetTeamUltimateCooldownRemaining(localTeamId, "heal_ultimate") ?? 0f;
		float supportCooldown = GameManager.Instance?.GetTeamUltimateCooldownRemaining(localTeamId, "support_ultimate") ?? 0f;
		bool canHeal = healCooldown <= 0f;
		bool canSupport = supportCooldown <= 0f;

		_healUltimateButton.Visible = true;
		_healUltimateButton.Disabled = !canHeal;
		_healUltimateButton.Text = healCooldown > 0f
			? BuildUltimateCooldownLabel("Heal Ult", healCooldown)
			: BuildUltimateReadyLabel("Heal Ult", "ultimate_heal");

		_supportUltimateButton.Visible = true;
		_supportUltimateButton.Disabled = !canSupport;
		_supportUltimateButton.Text = supportCooldown > 0f
			? BuildUltimateCooldownLabel("Support Ult", supportCooldown)
			: BuildUltimateReadyLabel("Support Ult", "ultimate_support");
	}

	private static string BuildUltimateCooldownLabel(string label, float cooldown)
	{
		return $"{label} [{Mathf.CeilToInt(cooldown)}s]";
	}

	private static string BuildUltimateReadyLabel(string label, string actionName)
	{
		Key key = KeybindingsManager.Instance?.GetBindingForAction(actionName) ?? Key.None;
		string keyLabel = KeybindingsManager.KeyDisplayName(key);
		return $"{label} [{keyLabel}]";
	}

	private void UpdateLeaderboard(double delta)
	{
		if (_leaderboardRows == null || GameManager.Instance == null)
			return;

		_leaderboardRefreshTimer -= (float)delta;
		if (_leaderboardRefreshTimer > 0f)
			return;

		_leaderboardRefreshTimer = LeaderboardRefreshInterval;

		var allCamps = GameManager.Instance.GetAllCamps();
		if (allCamps == null || allCamps.Count == 0)
		{
			_leaderboardRows.Text = LocalizationManager.Instance?.GetText("ranking_no_data") ?? "Aucune donnée";
			return;
		}

		var teamStats = new Dictionary<int, (int camps, int territories)>();
		var campsByRegion = new Dictionary<int, List<CampSimple>>();

		foreach (var camp in allCamps)
		{
			if (camp == null || !IsInstanceValid(camp))
				continue;

			int teamId = camp.GetTeamId();
			if (teamId <= 0 || camp.IsNeutralCamp)
				continue;

			if (!teamStats.ContainsKey(teamId))
				teamStats[teamId] = (0, 0);

			var current = teamStats[teamId];
			teamStats[teamId] = (current.camps + 1, current.territories);

			if (camp.RegionId > 0)
			{
				if (!campsByRegion.ContainsKey(camp.RegionId))
					campsByRegion[camp.RegionId] = new List<CampSimple>();
				campsByRegion[camp.RegionId].Add(camp);
			}
		}

		foreach (var (_, regionCamps) in campsByRegion)
		{
			if (regionCamps.Count == 0)
				continue;

			int firstTeam = regionCamps[0].GetTeamId();
			if (firstTeam <= 0)
				continue;

			bool fullyControlled = true;
			foreach (var camp in regionCamps)
			{
				if (camp.IsNeutralCamp || camp.GetTeamId() != firstTeam)
				{
					fullyControlled = false;
					break;
				}
			}

			if (!fullyControlled || !teamStats.ContainsKey(firstTeam))
				continue;

			var current = teamStats[firstTeam];
			teamStats[firstTeam] = (current.camps, current.territories + 1);
		}

		var ranking = new List<(int teamId, int camps, int territories)>();
		foreach (var (teamId, stats) in teamStats)
			ranking.Add((teamId, stats.camps, stats.territories));

		ranking.Sort((a, b) =>
		{
			int cmp = b.camps.CompareTo(a.camps);
			if (cmp != 0) return cmp;
			cmp = b.territories.CompareTo(a.territories);
			if (cmp != 0) return cmp;
			return a.teamId.CompareTo(b.teamId);
		});

		var loc = LocalizationManager.Instance;
		string campSingular  = loc?.GetText("ranking_camps")         ?? "camp";
		string campPlural    = loc?.GetText("ranking_camps_plural")  ?? "camps";
		string regAbbr       = loc?.GetText("ranking_regions_abbr")  ?? "rég.";
		string goldAbbr      = loc?.GetText("ranking_gold_abbr")     ?? "or";

		// Vider les lignes précédentes
		foreach (var child in _leaderboardRowsContainer.GetChildren())
			child.QueueFree();

		int displayCount = Mathf.Min(ranking.Count, 10);
		for (int i = 0; i < displayCount; i++)
		{
			var entry   = ranking[i];
			string name = ResolveLeaderboardName(entry.teamId);
			string campLabel = entry.camps > 1 ? campPlural : campSingular;
			int gold = GameManager.Instance.GetGold(entry.teamId);

			// Couleur selon le rang
			Color rowColor = i == 0
				? new Color(1f,    0.88f, 0.42f, 1f)   // or — 1er
				: i == 1
					? new Color(0.88f, 0.88f, 0.88f, 1f) // argent — 2e
					: new Color(0.94f, 0.91f, 0.80f, 1f); // crème — reste

			var hbox = new HBoxContainer();
			hbox.AddThemeConstantOverride("separation", 0);

			// Colonne rang (largeur fixe) : médaille + numéro
			var rankLabel = new Label();
			rankLabel.CustomMinimumSize = new Vector2(30, 0);
			string medal = i == 0 ? "♛" : "";
			rankLabel.Text = $"{medal}{i + 1}.";
			rankLabel.HorizontalAlignment = HorizontalAlignment.Right;
			rankLabel.AddThemeFontSizeOverride("font_size", 13);
			rankLabel.AddThemeColorOverride("font_color", rowColor);
			hbox.AddChild(rankLabel);

			// Espace fixe entre rang et nom
			var gap = new Label();
			gap.CustomMinimumSize = new Vector2(8, 0);
			gap.Text = "";
			hbox.AddChild(gap);

			// Colonne nom + stats (flexible)
			var infoLabel = new Label();
			infoLabel.Text = $"{name}   {entry.camps} {campLabel}  ·  {entry.territories} {regAbbr}  ·  {gold} {goldAbbr}";
			infoLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			infoLabel.AddThemeFontSizeOverride("font_size", 13);
			infoLabel.AddThemeColorOverride("font_color", rowColor);
			hbox.AddChild(infoLabel);

			_leaderboardRowsContainer.AddChild(hbox);
		}

		_leaderboardLastLineCount = displayCount;
		if (_leaderboardExpanded)
			UpdateLeaderboardPanelHeight(displayCount);
	}

	// Hauteur dynamique : bouton-titre + séparateur + lignes + marges uniformes 12px
	private void UpdateLeaderboardPanelHeight(int lineCount)
	{
		if (_leaderboardPanel == null) return;
		// bouton ~34px, séparateur ~4px, chaque ligne HBox ~22px (font13 + padding), séparations 4px + 3px
		float contentHeight = 34f + 4f + 4f + 4f + lineCount * 22f + Mathf.Max(0, lineCount - 1) * 3f;
		float totalHeight   = contentHeight + 22f; // panel ContentMargin (8 top + 14 bottom)
		_leaderboardPanel.OffsetBottom = _leaderboardPanel.OffsetTop + totalHeight;
	}

	private string ResolveLeaderboardName(int teamId)
	{
		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		int localTeamId = GetLocalTeamId();
		var loc = LocalizationManager.Instance;
		string playerLabel = loc?.GetText("ranking_player") ?? "Joueur";
		string aiLabel     = loc?.GetText("ranking_ai")     ?? "IA";
		string aiBossLabel = loc?.GetText("ranking_ai_boss") ?? "IA Boss";

		if (teamId == localTeamId)
		{
			if (gameState?.IsOnline == true && !string.IsNullOrWhiteSpace(gameState.PlayerDisplayName))
				return gameState.PlayerDisplayName;
			return $"{playerLabel} {teamId}";
		}

		bool isAi = (gameState?.IsAIMode == true) || AIController.BossTeamIds.Contains(teamId);
		if (isAi)
		{
			if (AIController.BossTeamIds.Contains(teamId))
				return $"{aiBossLabel} {teamId}";
			return $"{aiLabel} {teamId}";
		}

		return $"{playerLabel} {teamId}";
	}

	private void UpdateUnitButtons()
	{
		if (_selectionManager == null) return;

		var selectedCamp = _selectionManager.GetSelectedCamp();

		int localTeam = GetLocalTeamId();

		if (selectedCamp == null || !IsInstanceValid(selectedCamp))
		{
			if (_tierInfoLabel != null) _tierInfoLabel.Visible = false;
			SetAllUnitButtonsDisabled(null);
			return;
		}

		if (selectedCamp.GetTeamId() != localTeam)
		{
			if (_tierInfoLabel != null) _tierInfoLabel.Visible = false;
			SetAllUnitButtonsDisabled("Sélectionnez un de vos camps");
			return;
		}

		bool queueFull = selectedCamp.GetQueueCount() >= selectedCamp.GetMaxQueueSize();
		int unlockedTier = GameManager.Instance?.GetUnlockedTier(localTeam) ?? 1;

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
			string denyReason = locked ? null : selectedCamp.GetBuyUnitDenyReason(unitType);
			bool canBuy = !locked && denyReason == null;

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
			else if (denyReason != null)
				btn.TooltipText = denyReason;
			else
				btn.TooltipText = "";
		}
	}

	private void SetAllUnitButtonsDisabled(string tooltip)
	{
		foreach (string unitType in UnitTypes)
		{
			if (!_unitButtons.TryGetValue(unitType, out var btn)) continue;
			btn.Disabled = true;
			btn.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.8f);
			btn.TooltipText = tooltip ?? "";
			if (_lockLabels.TryGetValue(unitType, out var lbl))
				lbl.Text = "";
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
				int activeShips = ShipStats.CountActiveShipsForTeam(GetLocalTeamId(), GetTree());
				if (activeShips >= ShipStats.MaxActiveShipsPerTeam)
					btn.TooltipText = $"Limite de flotte atteinte ({activeShips}/{ShipStats.MaxActiveShipsPerTeam})";
				else
				{
					int queueCount = selectedPort.GetShipQueueCount();
					int maxQueue = selectedPort.GetMaxShipQueueSize();
					if (queueCount >= maxQueue)
						btn.TooltipText = "File navale pleine !";
					else
						btn.TooltipText = $"Or insuffisant ({ShipStats.GetStats(shipType).Price}g requis)";
				}
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

		int gold = GameManager.Instance.GetGold(GetLocalTeamId());
		_goldLabel.Text = $"{gold}";
	}
}
