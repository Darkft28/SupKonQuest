using Godot;

public partial class ButtonPlay : Button
{
	public void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/GameModeMenu.tscn");
	}
}
