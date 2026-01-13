using Godot;
using System;

public partial class TestSetup : Node
{
	public override void _Ready()
	{
		// Ce message apparaîtra en bas de ton écran Godot
		GD.Print("VICTOIRE ! Rider et Godot communiquent parfaitement !");
	}
}
