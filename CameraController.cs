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
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    ApplyZoom(ZoomStep);
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    ApplyZoom(-ZoomStep);
                }
            }
        }

        private void ApplyZoom(Vector2 step)
        {
            // 1. On mémorise où est la souris dans le MONDE avant de zoomer
            Vector2 mousePosBefore = GetGlobalMousePosition();

            // 2. On applique le zoom
            Zoom = (Zoom + step).Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));

            // 3. On calcule où est la souris dans le MONDE après le zoom
            // (La position a changé car l'échelle a changé)
            Vector2 mousePosAfter = GetGlobalMousePosition();

            // 4. On déplace la caméra de la différence pour que le point sous la souris reste fixe
            Position += mousePosBefore - mousePosAfter;
        }
        
    }
}
