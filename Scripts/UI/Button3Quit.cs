using Godot;

public partial class Button3Quit : Button
{
	public void OnButtonPressed() 
	{
		GetTree().Quit();
	}
}
