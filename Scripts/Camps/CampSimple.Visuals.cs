using Godot;

public partial class CampSimple
{
	private void CreateCampIdLabel()
	{
		_campIdLabel = new Label();
		_campIdLabel.Text = $"#{CampId}";
		_campIdLabel.Position = new Vector2(-20, -130);
		_campIdLabel.AddThemeFontSizeOverride("font_size", 20);
		_campIdLabel.AddThemeColorOverride("font_color", GetTeamColor());
		_campIdLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		_campIdLabel.AddThemeConstantOverride("outline_size", 3);
		AddChild(_campIdLabel);
	}

	private void CreateHealthBar()
	{
		//fond noir
		_healthBarBackground = new ColorRect();
		_healthBarBackground.Size = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBarBackground.Position = new Vector2(-HealthBarWidth / 2, -100);
		_healthBarBackground.Color = new Color(0, 0, 0, 0.8f);
		AddChild(_healthBarBackground);

		//bare de vie à la couleur de l'équipe
		_healthBarForeground = new ColorRect();
		_healthBarForeground.Size = new Vector2(HealthBarWidth, HealthBarHeight);
		_healthBarForeground.Position = new Vector2(-HealthBarWidth / 2, -100);
		_healthBarForeground.Color = GetTeamColor();
		AddChild(_healthBarForeground);
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

	private Color GetTeamColor()
	{
		if (TeamId <= 0)
			return new Color(0.5f, 0.5f, 0.5f, 1f);

		int colorIndex = (TeamId - 1) % _teamColors.Length;
		return _teamColors[colorIndex];
	}
}
