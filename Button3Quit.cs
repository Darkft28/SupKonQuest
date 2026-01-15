using Godot;
using System;

public partial class Button3Quit : Button
{
	public void OnButtonPressed() 
	{
		GetTree().Quit();
	}
}
