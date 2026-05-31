using Godot;

public partial class CampSimple
{
	private Node2D _campUiRoot;

	private void EnsureCampUi()
	{
		_campUiRoot = GetNodeOrNull<Node2D>("CampUI");
		if (_campUiRoot == null)
			return;

		_campIdLabel = _campUiRoot.GetNodeOrNull<Label>("CampIdLabel");
		_healthBar = _campUiRoot.GetNodeOrNull<ProgressBar>("HealthBar");
	}

	private void CreateCampIdLabel()
	{
		EnsureCampUi();
		if (_campUiRoot == null)
			return;

		if (_campIdLabel == null)
		{
			_campIdLabel = new Label();
			_campIdLabel.Name = "CampIdLabel";
			_campUiRoot.AddChild(_campIdLabel);
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
		if (_campUiRoot == null)
			return;

		if (_healthBar == null)
		{
			_healthBar = new ProgressBar();
			_healthBar.Name = "HealthBar";
			_campUiRoot.AddChild(_healthBar);
		}

		_healthBar.Position = new Vector2(-HealthBarWidth / 2, -100);
		_healthBar.Size = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBar.CustomMinimumSize = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBar.ShowPercentage = false;
		_healthBar.FillMode = 0;
		_healthBar.MinValue = 0;
		_healthBar.MaxValue = MaxHealth;
		_healthBar.Value = GetCurrentHealth();

		_healthBarBackgroundStyle ??= new StyleBoxFlat();
		_healthBarBackgroundStyle.BgColor = new Color(0, 0, 0, 0.8f);
		_healthBarBackgroundStyle.CornerRadiusTopLeft = 2;
		_healthBarBackgroundStyle.CornerRadiusTopRight = 2;
		_healthBarBackgroundStyle.CornerRadiusBottomLeft = 2;
		_healthBarBackgroundStyle.CornerRadiusBottomRight = 2;

		_healthBarFillStyle ??= new StyleBoxFlat();
		_healthBarFillStyle.BgColor = GetTeamColor();
		_healthBarFillStyle.CornerRadiusTopLeft = 2;
		_healthBarFillStyle.CornerRadiusTopRight = 2;
		_healthBarFillStyle.CornerRadiusBottomLeft = 2;
		_healthBarFillStyle.CornerRadiusBottomRight = 2;

		_healthBar.AddThemeStyleboxOverride("background", _healthBarBackgroundStyle);
		_healthBar.AddThemeStyleboxOverride("fill", _healthBarFillStyle);
	}

	private void UpdateHealthBar()
	{
		if (_healthBar == null)
			return;

		_healthBar.MaxValue = MaxHealth;
		_healthBar.Value = GetCurrentHealth();
	}

	private void UpdateCampVisualTheme()
	{
		if (_campIdLabel != null)
		{
			_campIdLabel.AddThemeColorOverride("font_color", GetTeamColor());
			_campIdLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		}

		if (_healthBarFillStyle != null)
		{
			_healthBarFillStyle.BgColor = GetTeamColor();
		}
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
		return isBoss ? $"#{CampId} boss": $"#{CampId}";
	}

	public static Color GetTeamColor(int teamId)
	{
		if (teamId <= 0)
			return new Color(0.5f, 0.5f, 0.5f, 1f);

		return _teamColors[(teamId - 1) % _teamColors.Length];
	}

	private Color GetTeamColor() => GetTeamColor(TeamId);
}
