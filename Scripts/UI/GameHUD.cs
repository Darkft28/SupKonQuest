using Godot;

public partial class GameHUD : Control
{
	private Label _goldLabel;
	private Control _goldPanel;
	private SelectionManager _selectionManager;

	public override void _Ready()
	{
		// Récupérer les éléments UI
		_goldPanel = GetNode<Control>("GoldPanel");
		_goldLabel = GetNode<Label>("GoldPanel/HBoxContainer/GoldLabel");

		// Toujours visible
		_goldPanel.Visible = true;
		_goldLabel.Text = "0";

		GD.Print("[HUD] GameHUD initialisé");
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
