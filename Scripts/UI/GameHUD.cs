using Godot;
using System.Collections.Generic;

public partial class GameHUD : Control
{
	private Label _goldLabel;
	private Control _goldPanel;
	private SelectionManager _selectionManager;
	private CampSimple _lastSelectedCampForHud;
	private CampSimple _lastSelectedPortForHud;
	private Dictionary<string, TextureButton> _unitButtons = new Dictionary<string, TextureButton>();
	private Dictionary<string, TextureButton> _shipButtons = new Dictionary<string, TextureButton>();
	private Dictionary<string, Label> _lockLabels = new Dictionary<string, Label>();
	private HBoxContainer _unitsContainer;
	private HBoxContainer _shipsContainer;
	private Button _quitButton;
	private Control _portPanel;
	private TextureButton _portTextureBtn;
	private Button _unlockTier2Button;
	private Control _unlockTier2Panel;
	private VBoxContainer _abilitiesContainer;
	private Button _healUltimateButton;
	private Button _supportUltimateButton;

	private Button _queueToggleBtn;
	private Control _queuePanel;
	private HBoxContainer _queueContent;
	private bool _queueOpen = false;
	private float _queueRefreshTimer = 0f;
	private Tween _queueTween;
	private const float QueuePanelHeight = 92f;
	private const float QueueRefreshInterval = 0.1f;

	private static readonly Dictionary<string, string> UnitTexturePaths = new()
	{
		{ "Infantry",  "res://Assets/Units/Characters/Infantry/Infantry_Front.png" },
		{ "Support",   "res://Assets/Units/Characters/Support/Support_Front.png" },
		{ "Heal",      "res://Assets/Units/Characters/Healer/healer_Front.png" },
		{ "Range",     "res://Assets/Units/Characters/Range/Range_Front.png" },
		{ "AntiArmor", "res://Assets/Units/Characters/Anti-armor/Anti-armor_front.png" },
		{ "Heavy",     "res://Assets/Units/Characters/Heavy/Heavy_Front.png" },
		{ "Mortar",    "res://Assets/Units/Characters/Mortar/Mortar_Front.png" },
		{ "Tank",      "res://Assets/Units/Characters/Tank/Tank_Front.png" },
		{ "Transport", "res://Assets/Units/Ships/Transport/Transport_Front.png" },
		{ "Fregate",   "res://Assets/Units/Ships/Frégate/frégate_Front.png" },
		{ "Destroyer", "res://Assets/Units/Ships/Destroyer/Destroyers_Front.png" },
	};

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
		SetupQueuePanel();

		var nakama = GetNodeOrNull<NakamaService>("/root/NakamaService");
		if (nakama != null)
			nakama.Disconnected += OnNakamaDisconnected;

		var gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
		if (gameManager != null)
		{
			gameManager.OnlinePlayerLeft += OnOnlinePlayerLeft;
			gameManager.LocalPlayerEliminated += OnLocalPlayerEliminated;
			gameManager.GameWon += OnGameWon;
			gameManager.OnTeamUnitCountChanged += OnTeamUnitCountChanged;
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

	private void OnTeamUnitCountChanged(int teamId, int newCount)
	{
		if (teamId != GetLocalTeamId())
			return;

		UpdateUnitButtons();
	}

	private void RefreshLocalizedHudTexts()
	{
		if (_quitButton != null)
			_quitButton.Text = L("hud_quit_menu");
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
		{
			string t = L("tier_unlock_button");
			int p = t.IndexOf('(');
			_unlockTier2Button.Text = p > 0 ? t[..p].Trim() : t;
		}
		RefreshLeaderboardTitle();
	}

	private void BindSceneHudControls()
	{
		_quitButton = GetNode<Button>("QuitButton");
		GetNode<Button>("PortButton").Visible = false; // remplacé par _portPanel
		_disconnectPanel = GetNode<Panel>("DisconnectPanel");
		_disconnectLabel = GetNode<Label>("DisconnectPanel/DisconnectLabel");
		_leaderboardPanel = GetNodeOrNull<Panel>("LeaderboardPanel");
		_leaderboardVBox = GetNodeOrNull<VBoxContainer>("LeaderboardPanel/LeaderboardVBox");
		_leaderboardTitle = GetNodeOrNull<Label>("LeaderboardPanel/LeaderboardVBox/LeaderboardTitle");
		_leaderboardRows = GetNodeOrNull<Label>("LeaderboardPanel/LeaderboardVBox/LeaderboardRows");

		_quitButton.Text = L("hud_quit_menu");
		UIStyle.ApplyStone(_quitButton);
		_quitButton.Pressed += OnQuitButtonPressed;

		SetupPortPanel();

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

	private void SetupPortPanel()
	{
		_portPanel      = GetNode<Control>("NinePatchRect/PortPanel");
		_portTextureBtn = GetNode<TextureButton>("NinePatchRect/PortPanel/BuyButton");
		var lbl = GetNodeOrNull<Label>("NinePatchRect/PortPanel/PriceRow/PriceLabel");
		if (lbl != null) lbl.Text = CampSimple.PortCost.ToString();
		_portTextureBtn.Pressed += OnPortButtonPressed;
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
			gameManager.OnTeamUnitCountChanged -= OnTeamUnitCountChanged;
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
		// Panneau Palier 2 : récupéré depuis la scène
		_unlockTier2Panel  = GetNode<Control>("NinePatchRect/Tier2Panel");
		_unlockTier2Button = GetNode<Button>("NinePatchRect/Tier2Panel/UnlockButton");
		string fullTierText = L("tier_unlock_button");
		int parenIdx = fullTierText.IndexOf('(');
		_unlockTier2Button.Text = parenIdx > 0 ? fullTierText[..parenIdx].Trim() : fullTierText;
		UIStyle.ApplyStone(_unlockTier2Button);
		foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
		{
			if (_unlockTier2Button.GetThemeStylebox(state) is StyleBoxTexture sb)
			{
				sb.ContentMarginLeft = 8f; sb.ContentMarginRight  = 8f;
				sb.ContentMarginTop  = 2f; sb.ContentMarginBottom = 2f;
			}
		}
		_unlockTier2Button.Pressed += OnUnlockTier2Pressed;
		var t2lbl = GetNodeOrNull<Label>("NinePatchRect/Tier2Panel/PriceRow/PriceLabel");
		if (t2lbl != null) t2lbl.Text = GameManager.Tier2Cost.ToString();
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
		UpdateShipButtons();
		UpdateAbilityButtons();
		HandleAbilityHotkeys();
		UpdateLeaderboard(delta);
		if (_queueOpen)
		{
			_queueRefreshTimer -= (float)delta;
			if (_queueRefreshTimer <= 0f)
			{
				_queueRefreshTimer = QueueRefreshInterval;
				UpdateQueueDisplay();
			}
		}
	}

	private void SetupQueuePanel()
	{
		_queueToggleBtn = GetNode<Button>("NinePatchRect/QueueToggleBtn");
		UIStyle.ApplyStone(_queueToggleBtn);
		_queueToggleBtn.Pressed += OnQueueTogglePressed;

		_queuePanel = GetNode<Panel>("NinePatchRect/QueuePanel");
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0f, 0f, 0f, 0.78f);
		panelStyle.SetCornerRadiusAll(4);
		((Panel)_queuePanel).AddThemeStyleboxOverride("panel", panelStyle);

		_queueContent = GetNode<HBoxContainer>("NinePatchRect/QueuePanel/QueueContent");
	}

	private void OnQueueTogglePressed()
	{
		_queueOpen = !_queueOpen;
		_queueToggleBtn.Text = _queueOpen ? "File d'attente ▼" : "File d'attente ▲";

		_queueTween?.Kill();
		_queueTween = CreateTween();
		float targetTop = _queueOpen ? -QueuePanelHeight : 0f;
		_queueTween.TweenProperty(_queuePanel, "offset_top", targetTop, 0.2f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		_queueTween.TweenProperty(_queuePanel, "offset_bottom", 0f, 0f); // fixe

		if (_queueOpen) UpdateQueueDisplay();
	}

	private void UpdateQueueDisplay()
	{
		foreach (var child in _queueContent.GetChildren())
			child.QueueFree();

		var camp = _selectionManager?.GetSelectedCamp();
		if (camp == null || !IsInstanceValid(camp) || camp.GetTeamId() != GetLocalTeamId())
			return;

		string current = camp.GetCurrentProduction();
		float progress = camp.GetProductionProgress();
		string[] queued = camp.GetQueuedUnits();

		float totalRemaining = 0f;

		if (current != null)
		{
			float prodTime = UnitStats.GetStats(current).ProductionTime;
			float remaining = prodTime * (1f - progress);
			totalRemaining += remaining;
			_queueContent.AddChild(BuildQueueCard(current, progress, remaining));
		}

		foreach (var unitType in queued)
		{
			float t = UnitStats.GetStats(unitType).ProductionTime;
			totalRemaining += t;
			_queueContent.AddChild(BuildQueueCard(unitType, 0f, t));
		}

		// Label "Total" à droite
		var totalLabel = new Label();
		totalLabel.Text = $"Total\n{FormatTime(totalRemaining)}";
		totalLabel.AddThemeFontSizeOverride("font_size", 11);
		totalLabel.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.42f, 1f));
		totalLabel.HorizontalAlignment = HorizontalAlignment.Center;
		totalLabel.VerticalAlignment   = VerticalAlignment.Center;
		totalLabel.SizeFlagsVertical   = Control.SizeFlags.Fill;
		_queueContent.AddChild(totalLabel);
	}

	private static string FormatTime(float seconds)
	{
		if (seconds >= 60f)
		{
			int m = (int)(seconds / 60f);
			float s = seconds % 60f;
			return $"{m}m {s:0.00}s";
		}
		return $"{seconds:0.00}s";
	}

	private static Control BuildQueueCard(string unitType, float progress, float timeSeconds)
	{
		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 2);

		var bar = new ProgressBar();
		bar.CustomMinimumSize = new Vector2(50, 8);
		bar.Value = progress * 100f;
		bar.ShowPercentage = false;
		card.AddChild(bar);

		var img = new TextureRect();
		img.CustomMinimumSize = new Vector2(50, 50);
		img.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		img.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		if (UnitTexturePaths.TryGetValue(unitType, out string path))
			img.Texture = GD.Load<Texture2D>(path);
		card.AddChild(img);

		var timeLabel = new Label();
		timeLabel.Text = FormatTime(timeSeconds);
		timeLabel.AddThemeFontSizeOverride("font_size", 10);
		timeLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 1f));
		timeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		card.AddChild(timeLabel);

		return card;
	}

	private void CreateAbilityButtons()
	{
		_abilitiesContainer  = GetNode<VBoxContainer>("NinePatchRect/AbilitiesContainer");
		_healUltimateButton  = GetNode<Button>("NinePatchRect/AbilitiesContainer/HealUlt");
		_healUltimateButton.Text = BuildUltimateReadyLabel("Heal Ult", "ultimate_heal");
		UIStyle.ApplyStone(_healUltimateButton);
		_healUltimateButton.Pressed += () => StartAbilityTargeting("heal_ultimate");

		_supportUltimateButton = GetNode<Button>("NinePatchRect/AbilitiesContainer/SupportUlt");
		_supportUltimateButton.Text = BuildUltimateReadyLabel("Support Ult", "ultimate_support");
		UIStyle.ApplyStone(_supportUltimateButton);
		_supportUltimateButton.Pressed += () => StartAbilityTargeting("support_ultimate");
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
			SetAllUnitButtonsDisabled(null);
			return;
		}

		var selectedCamp = _selectionManager.GetSelectedCamp();

		int localTeam = GetLocalTeamId();

		if (selectedCamp == null || !IsInstanceValid(selectedCamp))
		{
			SetAllUnitButtonsDisabled(null);
			return;
		}

		if (selectedCamp.GetTeamId() != localTeam)
		{
			SetAllUnitButtonsDisabled(L("hud_select_own_camp"));
			return;
		}

		bool queueFull = selectedCamp.GetQueueCount() >= selectedCamp.GetMaxQueueSize();
		int unlockedTier = GameManager.Instance?.GetUnlockedTier(localTeam) ?? 1;

		// Visibilité du panneau Palier 2
		if (unlockedTier >= 2)
		{
			if (_unlockTier2Panel != null) _unlockTier2Panel.Visible = false;
		}
		else
		{
			int gold = GameManager.Instance?.GetGold(GetLocalTeamId()) ?? 0;
			bool canAfford = gold >= GameManager.Tier2Cost;
			if (_unlockTier2Panel != null)
			{
				_unlockTier2Panel.Visible = true;
				_unlockTier2Button.Disabled = !canAfford;
				_unlockTier2Button.Modulate = canAfford ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);
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
			if (_portPanel != null)
				_portPanel.Visible = false;
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

		if (_portPanel != null)
		{
			bool showPort = selectedCamp != null && IsInstanceValid(selectedCamp)
				&& selectedCamp.GetTeamId() == GetLocalTeamId()
				&& !selectedCamp.HasPort
				&& !selectedCamp.IsNeutralCamp;
			_portPanel.Visible = showPort;
			if (showPort && _portTextureBtn != null)
				_portTextureBtn.Disabled = !selectedCamp.CanBuyPort();
		}

		bool selectionChanged = selectedCamp != _lastSelectedCampForHud || selectedPort != _lastSelectedPortForHud;
		if (selectionChanged)
		{
			_lastSelectedCampForHud = selectedCamp;
			_lastSelectedPortForHud = selectedPort;
			UpdateUnitButtons();
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
		UpdateUnitButtons();
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
