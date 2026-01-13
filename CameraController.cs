using Godot;
using System;

namespace SupKonQuest
{
    public partial class CameraController : Camera2D
    {
        [Export] public float Speed = 600.0f;
        [Export] public Vector2 ZoomStep = new Vector2(0.2f, 0.2f);
        [Export] public float MinZoom = 0.10f;
        [Export] public float MaxZoom = 3.0f;
        
        private bool _isDragging = false;

        public override void _Process(double delta)
        {
            // Déplacement au clavier (WASD / Flèches)
            Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
            Position += direction * Speed * (float)delta;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            
            if (@event is InputEventMouseButton mb)
            {
                // Click droit pour se déplacer
                if (mb.ButtonIndex == MouseButton.Right)
                {
                    _isDragging = mb.Pressed; 
                }
                else if (mb.Pressed)
                {
                    if (mb.ButtonIndex == MouseButton.WheelUp) ApplyZoom(ZoomStep);
                    else if (mb.ButtonIndex == MouseButton.WheelDown) ApplyZoom(-ZoomStep);
                }
            }
            // 2. Gestion du mouvement de la souris
            else if (@event is InputEventMouseMotion mm && _isDragging)
            {
                // IMPORTANT : On divise par le Zoom pour que la vitesse de glissement 
                // reste cohérente quel que soit le niveau de zoom.
                // On fait "-=" car si je tire la souris à gauche, je veux voir ce qu'il y a à droite.
                Position -= mm.Relative / Zoom;
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
