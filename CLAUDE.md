# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SupKonQuest is a strategy and conquest game built with Godot 4.5 and C# (.NET 8.0). It features procedural map generation, multiplayer networking via ENet, and a gold-based economy system.

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
- `Scripts/Game/` - Core game logic (map generation, units, camera, managers)
- `Scripts/Network/` - Multiplayer networking and state sync
- `Scripts/UI/` - User interface and menu systems
- `Scenes/` - Godot scene files (.tscn)
- `Assets/` - Textures, sprites, and other resources

### Singleton Managers (AutoLoads)
Three autoloaded managers defined in project.godot:
- **NetworkManager** - ENet multiplayer (hosting, joining, peer communication on port 7777)
- **GameState** - Game flow management (seed sync, scene transitions)
- **LocalizationManager** - i18n support (FR/EN/ES)

### Core Systems

**GameManager** - Team gold economy with passive income (5 gold/sec), unit purchasing, capture bonuses (50 gold)

**Unit System** - CharacterBody2D-based units with UnitStatsData (MaxHealth, Attack, Defense, Speed, Range, Price). Unit types: Infantry (50g), Support (75g), Heal (100g), Range (80g). Damage formula: `max(0, damage - defense)`

**CampSimple** - Base camps with health/capture mechanics. Spawns units in circular pattern. Capture requires killing all defending units first.

**MapGenerator** - FastNoiseLite Perlin noise on 256x256 grid. Biomes by altitude: Water (<-0.2), Sand, Grass/Forest, Rock, Snow (>0.55). Deterministic seeded generation for multiplayer sync.

**CameraController** - Zoom (0.05x-2.0x), WASD/arrow pan, right-click drag, recenter with C/Home

### Key Patterns
- Signal-based communication for UI and networking
- Group-based team organization ("units", "team_1", "team_2", etc.)
- Deterministic systems via seeded random for network sync
- All network state changes use RPCs with Authority mode

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
