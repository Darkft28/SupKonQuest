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
	private Panel _defeatPanel;
	private Label _defeatLabel;
	private bool _defeatBannerShown;
	private Panel _victoryPanel;
	private Label _victoryLabel;
	private Label _victoryAutoReturnLabel;
	private Button _victoryMenuButton;
	private enum GameEndScreen { None, Defeat, Disconnect, Victory }
	private GameEndScreen _activeEndScreen;
	private bool _gameExitStarted;
	private string _disconnectMessageKey;
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
	private const float LeaderboardRefreshInterval = 2.5f;
	private const float LeaderboardCollapsedHeight = 50f;
	private float _goldRefreshTimer = 0f;
	private const float GoldRefreshInterval = 1.0f;

	private static string L(string key) =>
		LocalizationManager.Instance?.GetText(key) ?? key;

	private static string LF(string key, params object[] args) =>
		string.Format(L(key), args);

	private int _lastVictoryTeamId = -1;

	private static readonly string[] UnitTypes = new[]
	{
		"Infantry", "Support", "Heal", "Range",
		"AntiArmor", "Heavy", "Mortar", "Tank"};

	private static readonly string[] ShipTypes = new[]
	{
		"Destroyer", "Fregate", "Transport"};

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
		{
			gameManager.OnlinePlayerLeft += OnOnlinePlayerLeft;
			gameManager.LocalPlayerEliminated += OnLocalPlayerEliminated;
			gameManager.GameWon += OnGameWon;
		}

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;

		RefreshLocalizedHudTexts();
	}

	private void OnLanguageChanged()
	{
		RefreshLocalizedHudTexts();
		if (_selectionManager != null)
		{
			UpdateUnitButtons();
			UpdateShipButtons();
		}
	}

	private void RefreshLocalizedHudTexts()
	{
		if (_quitButton != null)
			_quitButton.Text = L("hud_quit_menu");
		if (_portButton != null)
			_portButton.Text = LF("hud_port", CampSimple.PortCost);
		if (_victoryMenuButton != null)
			_victoryMenuButton.Text = L("main_menu");
		if (_victoryAutoReturnLabel != null)
			_victoryAutoReturnLabel.Text = L("victory_auto_return");
		if (_activeEndScreen == GameEndScreen.Victory && _victoryLabel != null && _lastVictoryTeamId >= 0)
			_victoryLabel.Text = $"{L("victory")}\n{_lastVictoryTeamId}";
		if (_activeEndScreen == GameEndScreen.Disconnect && _disconnectLabel != null)
		{
			_disconnectLabel.Text = _disconnectMessageKey != null
				? L(_disconnectMessageKey)
				: _disconnectLabel.Text;
		}
		if (_defeatBannerShown && _defeatLabel != null)
			_defeatLabel.Text = L("defeat");
		if (_unlockTier2Button != null)
			_unlockTier2Button.Text = LF("tier_unlock_button", GameManager.Tier2Cost);
		RefreshLeaderboardTitle();
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

		_quitButton.Text = L("hud_quit_menu");
		UIStyle.ApplyStone(_quitButton);
		_quitButton.Pressed += OnQuitButtonPressed;

		_portButton.Text = LF("hud_port", CampSimple.PortCost);
		UIStyle.ApplyStone(_portButton);
		_portButton.Pressed += OnPortButtonPressed;
		_portButton.Visible = false;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0f, 0f, 0f, 0.6f);
		_disconnectPanel.AddThemeStyleboxOverride("panel", style);
		_disconnectPanel.Visible = false;

		_defeatPanel = GetNodeOrNull<Panel>("DefeatPanel");
		_defeatLabel = GetNodeOrNull<Label>("DefeatPanel/DefeatLabel");
		if (_defeatPanel != null)
		{
			_defeatPanel.AddThemeStyleboxOverride("panel", style);
			_defeatPanel.Visible = false;
			_defeatPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
		}

		_victoryPanel = GetNodeOrNull<Panel>("VictoryPanel");
		_victoryLabel = GetNodeOrNull<Label>("VictoryPanel/VBoxContainer/VictoryLabel");
		_victoryAutoReturnLabel = GetNodeOrNull<Label>("VictoryPanel/VBoxContainer/AutoReturnLabel");
		_victoryMenuButton = GetNodeOrNull<Button>("VictoryPanel/VBoxContainer/MenuButton");
		if (_victoryPanel != null)
		{
			_victoryPanel.AddThemeStyleboxOverride("panel", style);
			_victoryPanel.Visible = false;
		}
		if (_victoryMenuButton != null)
		{
			UIStyle.ApplyStone(_victoryMenuButton);
			_victoryMenuButton.Pressed += LeaveGameToMainMenu;
		}

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
		_leaderboardPanel.OffsetRight = 380f;
		_leaderboardPanel.OffsetBottom = _leaderboardPanel.OffsetTop + LeaderboardCollapsedHeight;

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0f, 0f, 0f, 0.45f);
		panelStyle.SetBorderWidthAll(0);
		panelStyle.SetCornerRadiusAll(4);
		_leaderboardPanel.AddThemeStyleboxOverride("panel", panelStyle);

		_leaderboardVBox.AnchorLeft = 0f;
		_leaderboardVBox.AnchorTop = 0f;
		_leaderboardVBox.AnchorRight = 1f;
		_leaderboardVBox.AnchorBottom = 1f;
		_leaderboardVBox.OffsetLeft   =  8f;
		_leaderboardVBox.OffsetTop    =  4f;
		_leaderboardVBox.OffsetRight  = -8f;
		_leaderboardVBox.OffsetBottom = -4f;
		_leaderboardVBox.AddThemeConstantOverride("separation", 4);
		_leaderboardPanel.ClipContents = true;
		_leaderboardVBox.ClipContents = true;

		// Bouton-titre rétractable : transparent, hover gold subtil
		_leaderboardTitle.Visible = false;
		_leaderboardToggleBtn = new Button();
		_leaderboardToggleBtn.AddThemeFontSizeOverride("font_size", 15);
		_leaderboardToggleBtn.AddThemeColorOverride("font_color",         new Color(1f, 0.88f, 0.42f, 1f));
		_leaderboardToggleBtn.AddThemeColorOverride("font_hover_color",   new Color(1f, 0.97f, 0.75f, 1f));
		_leaderboardToggleBtn.AddThemeColorOverride("font_pressed_color", new Color(0.88f, 0.62f, 0.18f, 1f));
		UIStyle.ApplyStone(_leaderboardToggleBtn);
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
		RefreshLeaderboardTitle();
	}

	private void RefreshLeaderboardTitle()
	{
		if (_leaderboardToggleBtn == null)
			return;

		string prefix = _leaderboardExpanded ? "v  ": ">  ";
		_leaderboardToggleBtn.Text = prefix + L("ranking_title");
	}

	private void OnLeaderboardTogglePressed()
	{
		_leaderboardExpanded = !_leaderboardExpanded;
		_leaderboardRowsContainer.Visible = _leaderboardExpanded;
		_leaderboardSeparator.Visible     = _leaderboardExpanded;

		RefreshLeaderboardTitle();

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
		LeaveGameToMainMenu();
	}

	private void LeaveGameToMainMenu()
	{
		if (_gameExitStarted)
			return;

		_gameExitStarted = true;
		GetTree().Paused = false;

		if (_victoryPanel != null)
			_victoryPanel.Visible = false;
		if (_disconnectPanel != null)
			_disconnectPanel.Visible = false;

		var gameState = GetNodeOrNull<GameState>("/root/GameState");
		if (gameState != null)
			gameState.ReturnToMainMenu();
		else
			GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	private bool ShouldIgnoreDisconnectSignal()
	{
		return _gameExitStarted
			|| _activeEndScreen == GameEndScreen.Victory
			|| GameState.Instance?.IsLeavingGame == true;
	}

	private void OnPortButtonPressed()
	{
		if (GameManager.Instance?.IsLocalPlayerEliminated() == true) return;
		var camp = _selectionManager?.GetSelectedCamp();
		if (camp == null || !IsInstanceValid(camp)) return;
		if (camp.BuyPort())
		{
			RefreshGoldDisplayNow();
			TerritoryManager.Instance?.StartPortPlacement(camp);
		}
	}

	private void OnNakamaDisconnected()
	{
		if (ShouldIgnoreDisconnectSignal())
			return;

		ShowDisconnectMessage("game_end_server_lost");
	}

	private void OnOnlinePlayerLeft(int teamId)
	{
		if (teamId == GetLocalTeamId() || ShouldIgnoreDisconnectSignal())
			return;

		ShowDisconnectMessage("game_end_opponent_left");
	}

	private void OnLocalPlayerEliminated() => ShowDefeatBanner();

	private void OnGameWon(int winningTeamId) => ShowVictoryScreen(winningTeamId);

	private void ShowVictoryScreen(int winningTeamId)
	{
		if (_gameExitStarted || _activeEndScreen == GameEndScreen.Victory
			|| _victoryPanel == null || _victoryLabel == null)
			return;

		_activeEndScreen = GameEndScreen.Victory;
		_lastVictoryTeamId = winningTeamId;
		_goldPanel.Visible = false;

		if (_disconnectPanel != null)
			_disconnectPanel.Visible = false;

		_victoryLabel.Text = $"{L("victory")}\n{winningTeamId}";

		if (_victoryAutoReturnLabel != null)
			_victoryAutoReturnLabel.Text = L("victory_auto_return");

		if (_victoryMenuButton != null)
			_victoryMenuButton.Text = L("main_menu");

		_victoryPanel.Visible = true;
		GetTree().Paused = true;

		GetTree().CreateTimer(5.0).Timeout += LeaveGameToMainMenu;
	}

	private void ShowDefeatBanner()
	{
		if (_defeatBannerShown || _defeatPanel == null || _defeatLabel == null)
			return;

		_defeatBannerShown = true;
		_activeEndScreen = GameEndScreen.Defeat;
		_goldPanel.Visible = false;

		_defeatLabel.Text = L("defeat");
		_defeatPanel.Visible = true;

		GetTree().CreateTimer(5.0).Timeout += () =>
		{
			if (IsInstanceValid(_defeatPanel))
				_defeatPanel.Visible = false;
		};
	}

	private void ShowDisconnectMessage(string messageKey)
	{
		if (_disconnectPanel == null || _disconnectLabel == null)
			return;
		if (_gameExitStarted || _activeEndScreen == GameEndScreen.Victory
			|| _activeEndScreen == GameEndScreen.Disconnect)
			return;

		_activeEndScreen = GameEndScreen.Disconnect;
		_disconnectMessageKey = messageKey;
		_disconnectLabel.Text = L(messageKey);
		_disconnectPanel.Visible = true;

		GetTree().CreateTimer(5.0).Timeout += LeaveGameToMainMenu;
	}

	public override void _ExitTree()
	{
		var nakama = GetNodeOrNull<NakamaService>("/root/NakamaService");
		if (nakama != null)
			nakama.Disconnected -= OnNakamaDisconnected;

		var gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
		if (gameManager != null)
		{
			gameManager.OnlinePlayerLeft -= OnOnlinePlayerLeft;
			gameManager.LocalPlayerEliminated -= OnLocalPlayerEliminated;
			gameManager.GameWon -= OnGameWon;
		}

		if (LocalizationManager.Instance != null)
			LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
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
		_unlockTier2Button.Text = LF("tier_unlock_button", GameManager.Tier2Cost);
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
		if (GameManager.Instance?.IsLocalPlayerEliminated() == true) return;
		int teamId = GetLocalTeamId();
		if (GameManager.Instance?.UnlockTier2(teamId) == true)
			RefreshGoldDisplayNow();
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
		if (GameManager.Instance?.IsLocalPlayerEliminated() == true) return;
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
			RefreshGoldDisplayNow();
			return;
		}

		if (selectedCamp.BuyUnit(unitType))
			RefreshGoldDisplayNow();
		else
			GD.Print($"[HUD] Achat {unitType} refusé : {selectedCamp.GetBuyUnitDenyReason(unitType)}");
	}

	private void OnShipButtonPressed(string shipType)
	{
		if (GameManager.Instance?.IsLocalPlayerEliminated() == true) return;
		if (_selectionManager == null) return;

		var selectedPort = _selectionManager.GetSelectedPort();
		if (selectedPort == null || !IsInstanceValid(selectedPort)) return;

		if (selectedPort.GetTeamId() != GetLocalTeamId()) return;

		if (ShouldUseRelayCommands())
		{
			NetworkCommandRouter.RequestBuyShip(selectedPort, shipType);
			RefreshGoldDisplayNow();
			return;
		}

		if (selectedPort.BuyShip(shipType))
			RefreshGoldDisplayNow();
	}

	public override void _Process(double delta)
	{
		if (_selectionManager == null)
		{
			FindSelectionManager();
		}

		UpdateGoldDisplayThrottled(delta);
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

		// Vider les lignes précédentes
		foreach (var child in _leaderboardRowsContainer.GetChildren())
			child.QueueFree();

		int displayCount = Mathf.Min(ranking.Count, 10);
		for (int i = 0; i < displayCount; i++)
		{
			var entry = ranking[i];
			string name = ResolveLeaderboardName(entry.teamId);
			int gold = GameManager.Instance.GetGold(entry.teamId);

			Color rowColor = i == 0
				? new Color(1f,    0.88f, 0.42f, 1f)
				: i == 1
					? new Color(0.88f, 0.88f, 0.88f, 1f)
					: new Color(0.94f, 0.91f, 0.80f, 1f);
			var dimColor = new Color(rowColor.R, rowColor.G, rowColor.B, 0.65f);

			var hbox = new HBoxContainer();
			hbox.AddThemeConstantOverride("separation", 3);
			hbox.SizeFlagsHorizontal = Control.SizeFlags.Fill;

			// Rang (fixe, aligné à droite)
			var rankLabel = new Label();
			rankLabel.CustomMinimumSize = new Vector2(22, 0);
			rankLabel.Text = $"{i + 1}.";
			rankLabel.HorizontalAlignment = HorizontalAlignment.Right;
			rankLabel.VerticalAlignment = VerticalAlignment.Center;
			rankLabel.AddThemeFontSizeOverride("font_size", 14);
			rankLabel.AddThemeColorOverride("font_color", rowColor);
			hbox.AddChild(rankLabel);

			// Nom (flexible, ellipsis si trop long)
			var nameLabel = new Label();
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			nameLabel.CustomMinimumSize = new Vector2(0, 0);
			nameLabel.AutowrapMode = TextServer.AutowrapMode.Off;
			nameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			nameLabel.Text = name;
			nameLabel.VerticalAlignment = VerticalAlignment.Center;
			nameLabel.AddThemeFontSizeOverride("font_size", 14);
			nameLabel.AddThemeColorOverride("font_color", rowColor);
			hbox.AddChild(nameLabel);

			// Camps (fixe, aligné à droite)
			var campsLabel = new Label();
			campsLabel.CustomMinimumSize = new Vector2(28, 0);
			campsLabel.HorizontalAlignment = HorizontalAlignment.Right;
			campsLabel.VerticalAlignment = VerticalAlignment.Center;
			campsLabel.Text = $"{entry.camps}c";
			campsLabel.AddThemeFontSizeOverride("font_size", 12);
			campsLabel.AddThemeColorOverride("font_color", dimColor);
			hbox.AddChild(campsLabel);

			// Régions (fixe, aligné à droite)
			var regsLabel = new Label();
			regsLabel.CustomMinimumSize = new Vector2(22, 0);
			regsLabel.HorizontalAlignment = HorizontalAlignment.Right;
			regsLabel.VerticalAlignment = VerticalAlignment.Center;
			regsLabel.Text = $"{entry.territories}r";
			regsLabel.AddThemeFontSizeOverride("font_size", 12);
			regsLabel.AddThemeColorOverride("font_color", dimColor);
			hbox.AddChild(regsLabel);

			// Or (fixe, aligné à droite, format abrégé)
			var goldLabel = new Label();
			goldLabel.CustomMinimumSize = new Vector2(46, 0);
			goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
			goldLabel.VerticalAlignment = VerticalAlignment.Center;
			goldLabel.Text = FormatGoldLeaderboard(gold);
			goldLabel.AddThemeFontSizeOverride("font_size", 12);
			goldLabel.AddThemeColorOverride("font_color", dimColor);
			hbox.AddChild(goldLabel);

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
		// bouton ~34px, séparateur ~4px, chaque ligne HBox ~24px (font14 + padding), séparations 4px + 3px
		float contentHeight = 34f + 4f + 4f + 4f + lineCount * 24f + Mathf.Max(0, lineCount - 1) * 3f;
		float totalHeight   = contentHeight;
		_leaderboardPanel.OffsetBottom = _leaderboardPanel.OffsetTop + totalHeight;
	}

	private static string FormatGoldLeaderboard(int gold)
	{
		if (gold >= 10000) return $"{gold / 1000}k";
		if (gold >= 1000)  return $"{gold / 1000f:0.#}k";
		return gold.ToString();
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

		if (GameManager.Instance?.IsLocalPlayerEliminated() == true)
		{
			if (_tierInfoLabel != null) _tierInfoLabel.Visible = false;
			SetAllUnitButtonsDisabled(null);
			return;
		}

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
			SetAllUnitButtonsDisabled(L("hud_select_own_camp"));
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
				_tierInfoLabel.Text = L("tier_info_3_unlocked");
				if (_unlockTier2Button != null) _unlockTier2Button.Visible = false;
			}
			else if (unlockedTier == 2)
			{
				string regionDesc = GetTier3RegionDescription(GetLocalTeamId());
				_tierInfoLabel.Text = LF("tier_info_2_progress", regionDesc);
				if (_unlockTier2Button != null) _unlockTier2Button.Visible = false;
			}
			else
			{
				int gold = GameManager.Instance?.GetGold(GetLocalTeamId()) ?? 0;
				bool canAfford = gold >= GameManager.Tier2Cost;
				_tierInfoLabel.Text = canAfford
					? L("tier_info_1_can_unlock")
					: LF("tier_info_1_save_gold", GameManager.Tier2Cost, gold);
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
				lbl.Text = locked ? LF("hud_lock_tier", requiredTier) : "";

			if (locked)
				btn.TooltipText = requiredTier == 2
					? LF("tier2_lock_tooltip", GameManager.Tier2Cost)
					: L("tier3_lock_tooltip");
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
				btn.TooltipText = L("ship_port_lock_tooltip");
			else if (!canBuy)
			{
				int activeShips = ShipStats.CountActiveShipsForTeam(GetLocalTeamId(), GetTree());
				if (activeShips >= ShipStats.MaxActiveShipsPerTeam)
					btn.TooltipText = LF("ship_fleet_limit", activeShips, ShipStats.MaxActiveShipsPerTeam);
				else
				{
					int queueCount = selectedPort.GetShipQueueCount();
					int maxQueue = selectedPort.GetMaxShipQueueSize();
					if (queueCount >= maxQueue)
						btn.TooltipText = L("ship_queue_full");
					else
						btn.TooltipText = LF("ship_insufficient_gold", ShipStats.GetStats(shipType).Price);
				}
			}
			else
				btn.TooltipText = "";
		}
	}

	private string GetTier3RegionDescription(int teamId)
	{
		if (GameManager.Instance == null) return L("tier_region_control_all");

		int homeRegion = GameManager.Instance.GetHomeRegion(teamId);
		if (homeRegion < 0) return L("tier_region_control_all");

		var allCamps = GameManager.Instance.GetAllCamps();
		var homeCamps = allCamps.FindAll(c => c.RegionId == homeRegion);
		int total = homeCamps.Count;
		int owned = homeCamps.FindAll(c => c.GetTeamId() == teamId).Count;

		if (owned == total && total >= 2)
			return LF("tier_region_complete", owned, total);

		return LF("tier_region_progress", total, owned);
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

		if (GameManager.Instance?.IsLocalPlayerEliminated() == true)
		{
			_unitsContainer.Visible = false;
			_shipsContainer.Visible = false;
			if (_portButton != null)
				_portButton.Visible = false;
			if (_tierInfoLabel != null)
				_tierInfoLabel.Visible = false;
			return;
		}

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

	private void UpdateGoldDisplayThrottled(double delta)
	{
		_goldRefreshTimer -= (float)delta;
		if (_goldRefreshTimer > 0f)
			return;

		_goldRefreshTimer = GoldRefreshInterval;
		UpdateGoldDisplay();
	}

	private void RefreshGoldDisplayNow()
	{
		_goldRefreshTimer = 0f;
		UpdateGoldDisplay();
	}

	private void UpdateGoldDisplay()
	{
		if (GameManager.Instance == null)
		{
			_goldPanel.Visible = false;
			_goldLabel.Text = "0";
			return;
		}

		if (GameManager.Instance.IsLocalPlayerEliminated())
		{
			_goldPanel.Visible = false;
			return;
		}

		_goldPanel.Visible = true;
		int gold = GameManager.Instance.GetGold(GetLocalTeamId());
		_goldLabel.Text = $"{gold}";
	}
}
