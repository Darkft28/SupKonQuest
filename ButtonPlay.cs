using Godot;
using System;

public partial class ButtonPlay : Button
{
	public void OnPlayPressed()
	{
		
		GetTree().ChangeSceneToFile("res://TestSetup.tscn");
	}
}
