using Godot;

/// <summary>
/// Style bouton pierre unifié pour toutes les scènes.
/// Trois états visuels : normal (pierre), hover (doré), pressed (ambre foncé).
/// </summary>
public static class UIStyle
{
    private const string StoneTexturePath = "res://Assets/Menu/Texture/Button_stone.png";

    // Teintes par état
    private static readonly Color ColorNormal  = new Color(1.00f, 1.00f, 1.00f, 1f);
    private static readonly Color ColorHover   = new Color(1.00f, 0.88f, 0.42f, 1f); // doré
    private static readonly Color ColorPressed = new Color(0.68f, 0.48f, 0.18f, 1f); // ambre foncé

    private static StyleBoxTexture Make(Color tint)
    {
        var style = new StyleBoxTexture();
        style.Texture = GD.Load<Texture2D>(StoneTexturePath);
        style.ModulateColor = tint;
        // Marges pour que le texte ne touche pas les bords de la texture
        style.ContentMarginLeft   = 14f;
        style.ContentMarginRight  = 14f;
        style.ContentMarginTop    = 8f;
        style.ContentMarginBottom = 8f;
        return style;
    }

    /// <summary>Applique les trois états pierre (normal / hover / pressed) à un Button.</summary>
    public static void ApplyStone(Button btn)
    {
        btn.AddThemeStyleboxOverride("normal",   Make(ColorNormal));
        btn.AddThemeStyleboxOverride("hover",    Make(ColorHover));
        btn.AddThemeStyleboxOverride("pressed",  Make(ColorPressed));
        btn.AddThemeStyleboxOverride("focus",    Make(ColorHover));
        btn.AddThemeStyleboxOverride("disabled", Make(new Color(0.5f, 0.5f, 0.5f, 0.6f)));
    }
}
