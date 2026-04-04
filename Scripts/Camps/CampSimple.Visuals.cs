using Godot;

public partial class CampSimple
{
	private Node2D _campUiRoot;

	private void EnsureCampUi()
	{
		_campUiRoot = GetNodeOrNull<Node2D>("CampUI");
		if (_campUiRoot == null)
		{
			var campUiScene = GD.Load<PackedScene>("res://Scenes/CampUI.tscn");
			_campUiRoot = campUiScene?.Instantiate<Node2D>();
			if (_campUiRoot == null)
			{
				_campUiRoot = new Node2D();
				_campUiRoot.Name = "CampUI";
			}
			AddChild(_campUiRoot);
		}

		_campIdLabel = _campUiRoot.GetNodeOrNull<Label>("CampIdLabel");
		_healthBarBackground = _campUiRoot.GetNodeOrNull<ColorRect>("HealthBarBackground");
		_healthBarForeground = _campUiRoot.GetNodeOrNull<ColorRect>("HealthBarForeground");
	}

	private void CreateCampIdLabel()
	{
		EnsureCampUi();
		if (_campIdLabel == null)
		{
			_campIdLabel = new Label();
			_campIdLabel.Name = "CampIdLabel";
			_campUiRoot?.AddChild(_campIdLabel);
		}

		_campIdLabel.Text = GetCampLabel();
		_campIdLabel.Position = new Vector2(-20, -130);
		_campIdLabel.AddThemeFontSizeOverride("font_size", 20);
		_campIdLabel.AddThemeColorOverride("font_color", GetTeamColor());
		_campIdLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		_campIdLabel.AddThemeConstantOverride("outline_size", 3);
	}

	private void CreateHealthBar()
	{
		EnsureCampUi();
		if (_healthBarBackground == null)
		{
			_healthBarBackground = new ColorRect();
			_healthBarBackground.Name = "HealthBarBackground";
			_campUiRoot?.AddChild(_healthBarBackground);
		}

		_healthBarBackground.Size = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBarBackground.Position = new Vector2(-HealthBarWidth / 2, -100);
		_healthBarBackground.Color = new Color(0, 0, 0, 0.8f);

		if (_healthBarForeground == null)
		{
			_healthBarForeground = new ColorRect();
			_healthBarForeground.Name = "HealthBarForeground";
			_campUiRoot?.AddChild(_healthBarForeground);
		}

		_healthBarForeground.Size = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBarForeground.Position = new Vector2(-HealthBarWidth / 2, -100);
		_healthBarForeground.Color = GetTeamColor();
	}

	private void UpdateHealthBar()
	{
		if (_healthBarForeground == null)
			return;

		float healthPercent = GetCurrentHealth() / MaxHealth;
		_healthBarForeground.Size = new Vector2(HealthBarWidth * healthPercent, HealthBarHeight);
	}

	private static readonly Color[] _teamColors = new Color[]
	{
		new Color(1, 0, 0, 1f),        // Rouge
		new Color(0, 0.5f, 1, 1f),     // Bleu
		new Color(0, 0.8f, 0, 1f),     // Vert
		new Color(1, 1, 0, 1f),        // Jaune
		new Color(1, 0, 1, 1f),        // Magenta
		new Color(0, 1, 1, 1f),        // Cyan
		new Color(1, 0.5f, 0, 1f),     // Orange
		new Color(0.5f, 0, 1, 1f),     // Violet
		new Color(0.6f, 0.3f, 0, 1f),  // Marron
		new Color(1, 0.4f, 0.7f, 1f),  // Rose
		new Color(0, 0.5f, 0, 1f),     // Vert foncé
		new Color(0.3f, 0.3f, 1, 1f),  // Bleu moyen
		new Color(1, 0.8f, 0, 1f),     // Or
		new Color(0, 0.8f, 0.6f, 1f),  // Turquoise
		new Color(0.8f, 0, 0.4f, 1f),  // Cramoisi
		new Color(0.5f, 0.8f, 0, 1f),  // Chartreuse
		new Color(1, 0.6f, 0.4f, 1f),  // Saumon
		new Color(0.4f, 0, 0.6f, 1f),  // Indigo
		new Color(0, 0.4f, 0.4f, 1f),  // Sarcelle
		new Color(0.8f, 0.8f, 0, 1f),  // Olive
		new Color(0.9f, 0.2f, 0.5f, 1f), // Framboise
		new Color(0.2f, 0.6f, 1, 1f),  // Azur
		new Color(0.7f, 1, 0.3f, 1f),  // Lime
		new Color(1, 0.3f, 0.3f, 1f),  // Corail
		new Color(0.6f, 0.4f, 1, 1f),  // Lavande
		new Color(0, 1, 0.5f, 1f),     // Menthe
		new Color(1, 0.5f, 0.5f, 1f),  // Pêche
		new Color(0.3f, 0, 0.3f, 1f),  // Prune
		new Color(0.4f, 0.7f, 0.4f, 1f), // Sauge
		new Color(0.9f, 0.6f, 0, 1f),  // Ambre
		new Color(0.5f, 0.5f, 1, 1f),  // Pervenche
		new Color(0.8f, 0.5f, 0.2f, 1f), // Cuivre
		new Color(0, 0.6f, 0.3f, 1f),  // Émeraude
		new Color(0.9f, 0, 0.9f, 1f),  // Fuchsia
		new Color(0.4f, 0.8f, 0.8f, 1f), // Aigue-marine
		new Color(0.7f, 0.2f, 0, 1f),  // Rouille
		new Color(0.5f, 1, 0.5f, 1f),  // Vert pâle
		new Color(0.3f, 0.5f, 0.7f, 1f), // Acier
		new Color(1, 0.9f, 0.4f, 1f),  // Crème
		new Color(0.6f, 0, 0.2f, 1f),  // Bordeaux
		new Color(0.2f, 0.8f, 0.4f, 1f), // Jade
		new Color(0.8f, 0.4f, 0.6f, 1f), // Mauve
	};

	public void RefreshCampLabel()
	{
		if (_campIdLabel != null)
			_campIdLabel.Text = GetCampLabel();
	}

	private string GetCampLabel()
	{
		bool isBoss = AIController.BossTeamIds.Contains(TeamId);
		return isBoss ? $"#{CampId} boss" : $"#{CampId}";
	}

	private Color GetTeamColor()
	{
		if (TeamId <= 0)
			return new Color(0.5f, 0.5f, 0.5f, 1f);

		int colorIndex = (TeamId - 1) % _teamColors.Length;
		return _teamColors[colorIndex];
	}
}
