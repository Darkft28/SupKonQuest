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
│   ├── Lobby.tscn     # Lobby multijoueur (host/join)
│   └── Game.tscn      # Scene de jeu principale
├── Scripts/
│   ├── Game/
│   │   ├── TestSetup.cs        # Generation procedurale de la map
│   │   └── CameraController.cs # Controles camera (zoom, pan, limites)
│   ├── Network/
│   │   ├── NetworkManager.cs   # Gestion connexion ENet (host/client)
│   │   └── GameState.cs        # Etat de jeu partage (seed, joueurs)
│   └── UI/
│       ├── ButtonPlay.cs       # Bouton jouer (solo)
│       ├── ButtonMultiplayer.cs # Bouton multijoueur
│       ├── Button3Quit.cs      # Bouton quitter
│       └── LobbyUI.cs          # Interface du lobby
└── project.godot
```

## Architecture du code

### Generation de map (TestSetup.cs)
- Map de 256x256 tiles (de -128 a 127, centree sur 0,0)
- Tiles de 128x128 pixels
- Generation procedurale avec FastNoiseLite
- Biomes: Eau, Sable, Herbe, Foret, Roche, Neige
- Objets: Arbres, Montagnes, Camps
- Support seed reseau pour synchronisation multijoueur

### Camera (CameraController.cs)
- Zoom molette avec interpolation (centree sur souris)
- Deplacement clavier (ZQSD/fleches)
- Drag clic droit
- Limites de map avec clamping manuel (evite bugs aux bords)
- Raccourci C ou Home pour recentrer
- Parametres exportes: ZoomSensitivity, MinZoom, MaxZoom, PanSpeed, etc.

### Reseau (NetworkManager.cs + GameState.cs)
- ENet pour connexion peer-to-peer
- Port par defaut: 7777
- Max 4 joueurs
- Signaux: PlayerConnected, PlayerDisconnected, ConnectionFailed, etc.
- GameState gere la seed et le lancement de partie
- RPC pour synchronisation (seed, liste joueurs)

### UI Lobby (LobbyUI.cs)
- Input IP et port
- Boutons Host/Join/Start/Back
- Liste des joueurs connectes
- Statut de connexion

## Conventions de code

- **Namespaces**: `SupKonQuest` pour scripts Game, global pour Network/UI
- **Nommage**: PascalCase pour classes/methodes, _camelCase pour champs prives
- **Prefixe underscore**: Variables privees (`_camera`, `_tileMapSol`)
- **Commentaires**: Simples `//` (pas de XML)

## Autoloads (Singletons Godot)

Les scripts suivants sont charges automatiquement:
- `/root/NetworkManager` - Gestion reseau
- `/root/GameState` - Etat de jeu partage

## Coordonnees importantes

```
Map tiles:     -128 a +127 (256x256)
Map pixels:    -16384 a +16384
Tile size:     128x128 pixels
Centre map:    (0, 0) en tiles et pixels
```

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

### Regles pour Claude
- Ne JAMAIS ajouter "Co-Authored-By: Claude" dans les commits
- Les commits doivent apparaitre comme faits uniquement par l'utilisateur
- Ne pas pusher automatiquement sans demande explicite
- Toujours commit sur la branche demandee (generalement develop)

## Points d'attention

- Les TileMapLayer utilisent des coordonnees de tiles, pas des pixels
- La map est centree sur (0,0) en coordonnees de tiles
- Le TileSet utilise des tiles de 128x128 pixels
- Camera: utilise clamping manuel, pas les limites built-in de Godot
- Reseau: la seed doit etre synchronisee AVANT de charger Game.tscn

## Commandes utiles

```bash
# Build
dotnet build

# Changer de branche
git checkout develop
git checkout feature/xxx

# Voir les changements
git diff
git status
```

## Flux de jeu

```
MainMenu.tscn
    ├── [Jouer] -> Game.tscn (solo)
    ├── [Multijoueur] -> Lobby.tscn
    │       ├── [Host] -> attend joueurs -> [Start] -> Game.tscn
    │       └── [Join] -> connecte -> attend host -> Game.tscn
    └── [Quitter] -> ferme le jeu
```
