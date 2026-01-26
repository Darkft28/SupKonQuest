using Godot;
using System.Collections.Generic;

namespace SupKonQuest
{
	public partial class SelectionManager : Node2D
	{
		private Camera2D _camera;

		public override void _Ready()
		{
			_camera = GetParent().GetNodeOrNull<Camera2D>("Camera2D");
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			
		}
		
	}
}
