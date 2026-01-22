using Godot;
using System;

public partial class ButtonMultiplayer : Button
{
	public void OnMultiplayerPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Lobby.tscn");
	}
}
