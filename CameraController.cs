using Godot;
using System;

namespace SupKonQuest
{
    public partial class CameraController : Camera2D
    {
        [Export] public float Speed = 600.0f;
        [Export] public Vector2 ZoomStep = new Vector2(0.1f, 0.1f);
        [Export] public float MinZoom = 0.5f;
        [Export] public float MaxZoom = 2.0f;

        public override void _Process(double delta)
        {
            // Déplacement au clavier (WASD / Flèches)
            Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
            Position += direction * Speed * (float)delta;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // Gestion du Zoom à la molette
            if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
            {
                if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
                {
                    Zoom = (Zoom + ZoomStep).Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));
                }
                else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
                {
                    Zoom = (Zoom - ZoomStep).Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));
                }
            }
        }
    }
}
