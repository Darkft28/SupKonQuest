# CLAUDE.md - Guide pour Claude Code

## Apercu du projet

**SupKonQuest** est un jeu de strategie/conquete developpe avec Godot 4.5 et C#.

## Stack technique

- **Moteur**: Godot 4.5
- **Langage**: C# (.NET 8.0)
- **IDE recommande**: Rider / VS Code
- **Rendu**: Forward Plus

## Structure du projet

```
SupKonQuest/
├── Assets/
│   ├── Map/           # Textures des tiles (herbe, eau, sable, etc.)
│   └── Objects/       # Sprites des objets (arbres, montagnes, camps)
├── Scenes/
│   ├── MainMenu.tscn  # Menu principal
│   └── Game.tscn      # Scene de jeu principale
├── Scripts/
│   ├── Game/
│   │   ├── TestSetup.cs        # Generation procedurale de la map
│   │   └── CameraController.cs # Controles camera (zoom, pan)
│   └── UI/
│       ├── ButtonPlay.cs       # Bouton jouer
│       └── Button3Quit.cs      # Bouton quitter
└── project.godot
```

## Architecture du code

### Generation de map (TestSetup.cs)
- Map de 256x256 tiles (de -128 a 127)
- Tiles de 128x128 pixels
- Generation procedurale avec FastNoiseLite
- Biomes: Eau, Sable, Herbe, Foret, Roche, Neige
- Objets: Arbres, Montagnes, Camps

### Camera (CameraController.cs)
- Zoom molette avec interpolation
- Deplacement clavier (ZQSD/fleches)
- Drag clic droit
- Centrage automatique sur la map

## Conventions de code

- **Namespaces**: `SupKonQuest` pour les scripts du jeu
- **Nommage**: PascalCase pour classes/methodes, _camelCase pour champs prives
- **Prefixe underscore**: Variables privees (`_camera`, `_tileMapSol`)

## Commandes Git

### Branches
```
main              # Version stable
develop           # Integration/test
feature/ui        # Interface utilisateur
feature/network   # Multijoueur
feature/gameplay  # Mecaniques de jeu
feature/map       # Generation de map
feature/camera    # Controles camera
```

### Workflow
1. Developper sur `feature/*`
2. Merger dans `develop` pour tester
3. Merger `develop` dans `main` quand stable

## Points d'attention

- Les TileMapLayer utilisent des coordonnees de tiles, pas des pixels
- La map est centree sur (0,0) en coordonnees de tiles
- Le TileSet utilise des tiles de 128x128 pixels
- Toujours tester le centrage camera apres modification de la map

## Commandes utiles

```bash
# Lancer le projet (depuis Godot)
# F5 ou bouton Play

# Build
dotnet build

# Changer de branche
git checkout feature/xxx
```
