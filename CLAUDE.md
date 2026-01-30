# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SupKonQuest is a strategy and conquest game built with Godot 4.5 and C# (.NET 8.0). It features procedural map generation, multiplayer networking via ENet, and a gold-based economy system with unit production queues.

## Game Flow
1. Joueur spawn avec 1 camp + 100 or
2. Camp génère 50 or/sec, joueur reçoit 5 or/sec passif
3. Acheter des unités → file de production
4. Sélectionner unités → clic droit pour déplacer
5. Tuer les défenseurs d'un camp → attaquer le camp → capture
6. Victoire: capturer tous les camps ennemis

## Key Scenes
- `Scenes/Main.tscn` - Point d'entrée
- `Scenes/Game.tscn` - Scène de jeu principale
- `Scenes/Unit.tscn` - Prefab unité

## Build Commands

```bash
# Build the C# project
dotnet build SupKonQuest.csproj

# Run in Godot editor (requires Godot 4.5 with C# support)
godot --path .

# Run the game directly
godot --path . --run
```

## Architecture

### Directory Structure
- `Scripts/Game/` - Core game logic (map generation, units, camera, managers, selection)
- `Scripts/Network/` - Multiplayer networking and state sync
- `Scripts/UI/` - User interface, HUD, menus, minimap
- `Scenes/` - Godot scene files (.tscn)
- `Assets/` - Textures, sprites, unit characters

### Singleton Managers (AutoLoads)
Three autoloaded managers defined in project.godot:
- **NetworkManager** - ENet multiplayer (hosting, joining, peer communication on port 7777)
- **GameState** - Game flow management (seed sync, scene transitions)
- **LocalizationManager** - i18n support (FR/EN/ES)

### Core Systems

**GameManager** - Team gold economy with passive income (5 gold/sec), unit purchasing, capture bonuses (50 gold). Starting gold: 100.

**Unit System** - CharacterBody2D-based units with UnitStatsData (MaxHealth, Attack, Defense, Speed, Range, Price, ProductionTime). 8 unit types available:
| Type | Price | HP | Attack | Defense | Speed | Range | Prod. Time |
|------|-------|-----|--------|---------|-------|-------|------------|
| Infantry | 50g | 100 | 15 | 10 | 150 | 50 | 2s |
| Support | 75g | 80 | 8 | 5 | 120 | 100 | 3s |
| Range | 80g | 70 | 20 | 5 | 100 | 300 | 3s |
| Heal | 100g | 60 | 0 | 3 | 100 | 150 | 3s |
| AntiArmor | 120g | 80 | 35 | 8 | 90 | 120 | 4s |
| Mortar | 130g | 50 | 40 | 3 | 60 | 400 | 4s |
| Heavy | 150g | 150 | 25 | 20 | 70 | 60 | 5s |
| Tank | 200g | 200 | 30 | 25 | 50 | 100 | 6s |

Damage formula: `max(0, damage - defense)`. Neutral camp units have 1.5x HP.

**CampSimple** - Base camps with health (500 HP), capture mechanics, and production queue (max 7 units). Generates gold passively (50 gold/sec). Spawns units in circular pattern (350px radius). Capture requires killing all defending units first.

**SelectionManager** - Handles unit/camp selection via click or box selection. Selected units can be moved with right-click. Stuck detection after 2s of no movement.

**MapGenerator** - FastNoiseLite Perlin noise on 256x256 grid. Biomes by altitude: Water (<-0.2), Sand, Grass/Forest, Rock, Snow (>0.55). Deterministic seeded generation for multiplayer sync.

**CameraController** - Zoom (0.05x-2.0x), WASD/arrow pan, right-click drag, recenter with C/Home

**GameHUD** - Displays gold for selected camp, unit purchase buttons with prices. Connected to SelectionManager for camp selection.

### Key Patterns
- Signal-based communication for UI and networking
- Group-based team organization ("units", "team_1", "team_2", "camps")
- Deterministic systems via seeded random for network sync
- All network state changes use RPCs with Authority mode
- Production queue system with async unit spawning

## Code Conventions

From CONTRIBUTING.md:
- Classes/Methods: PascalCase
- Private variables: _camelCase
- Local variables: camelCase
- Constants: PascalCase
- Commits: `type: description` (feat:, fix:, refactor:, docs:, style:, test:)

## Git Workflow

- Never push directly to main
- Create feature branches from develop: `feature/feature-name`
- Merge features to develop, then develop to main for releases
- **Ne jamais inclure de traces de collaboration avec Claude** (pas de "Co-Authored-By: Claude", pas de mentions Claude dans les commits)
