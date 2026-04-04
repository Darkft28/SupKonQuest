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
- `Scripts/Units/` - Unit.cs + partials (Combat, Movement, Healing, Transport, Visuals), UnitStats, Projectile
- `Scripts/Ships/` - Ship.cs + partials (Combat, Movement, Transport, Visuals), ShipStats, ShipProjectile
- `Scripts/Camps/` - CampSimple.cs + partials (Production, Naval, Defense, Visuals)
- `Scripts/Map/` - MapGenerator, TerrainGenerator (static), CampPlacer (static), TerritoryManager
- `Scripts/Selection/` - SelectionManager
- `Scripts/Camera/` - CameraController
- `Scripts/Economy/` - GameManager, VictoryManager
- `Scripts/Network/` - NetworkManager, GameState, NetworkSync, NetworkEntityRegistry
- `Scripts/AI/` - AIController (Utility AI, Easy/Medium/Hard, one per bot team)
- `Scripts/UI/` - GameHUD, LobbyUI, Minimap, MainMenu, GameModeMenu, LocalizationManager
- `AI-implementation.md` - Notes de conception IA (idées futures : boss IA, naval, stagger, personnalités)
- `Scenes/` - Godot scene files (.tscn)
- `Assets/` - Textures, sprites, unit characters

### Singleton Managers (AutoLoads)
Three autoloaded managers defined in project.godot:
- **NetworkManager** - ENet multiplayer (hosting, joining, peer communication on port 7777)
- **GameState** - Game flow management (seed sync, scene transitions)
- **LocalizationManager** - i18n support (FR/EN/ES)

### Core Systems

**GameManager** - Team gold economy with passive income (500 gold/sec), unit purchasing, capture bonuses (50 gold). Starting gold: 100. Manages a 3-tier unlock system: Tier 1 (default), Tier 2 (manual purchase: 1500 gold), Tier 3 (auto-unlock when controlling all camps in home region).

**Unit System** - CharacterBody2D-based units with UnitStatsData (MaxHealth, Attack, Defense, Speed, Range, Price, ProductionTime). 8 unit types available:
| Type | Price | HP | Attack | Defense | Speed | Range | Prod. Time |
|------|-------|-----|--------|---------|-------|-------|------------|
| Infantry | 50g | 100 | 15 | 10 | 150 | 100 | 2s |
| Support | 75g | 80 | 8 | 5 | 120 | 100 | 3s |
| Range | 80g | 70 | 20 | 5 | 100 | 300 | 3s |
| Heal | 100g | 60 | 0 | 3 | 100 | 150 | 3s |
| AntiArmor | 120g | 80 | 35 | 8 | 90 | 120 | 4s |
| Mortar | 130g | 50 | 40 | 3 | 60 | 400 | 4s |
| Heavy | 150g | 150 | 25 | 20 | 70 | 100 | 5s |
| Tank | 200g | 200 | 30 | 25 | 50 | 100 | 6s |

Damage formula: `damage * 100f / (100f + totalDefense)` (scalable reduction, NOT subtractive). Neutral camp units have 1.5x HP.
AntiArmor deals x2 damage vs Heavy. Mortar AoE splash 200px radius, 20 flat damage (ignores defense).

Unit tiers (affect production unlock):
- Tier 1: Infantry, Support, Range
- Tier 2: Heal, AntiArmor
- Tier 3: Mortar, Heavy, Tank

Ship tiers: Transport = Tier 1, Fregate + Destroyer = Tier 3.

**CampSimple** - Base camps with health (500 HP), capture mechanics, and production queue (max 7 units). Generates gold passively (500 gold/sec). Spawns units in circular pattern (525px radius). Capture requires killing all defending units first. Has a defensive turret (10 dmg/sec at 600px). Port built manually (500 gold via HUD button): player clicks a coastal tile to place it; orientation auto-detected from adjacent water. Port has its own production queue (max 5 ships). `TrySpawnPort()` exists but is not called (dead code). Each camp has a `RegionId` (variable per map: 3 regions on Irridium, 4 on Alabasta) used for regional bonuses and Tier 3 unlock.

**Ships** - CharacterBody2D naval units with own state machine. 3 types: Transport (200HP, capacity 10 units, 150g), Fregate (180HP, 20atk, 200g), Destroyer (250HP, 35atk, 300g). Ship.tscn at `Scenes/Ship.tscn`. Assets in `Assets/Units/Ships/`. Destroyer textures = `Destroyers_*.png`.

**SelectionManager** - Handles unit/camp/ship/port selection via click or box selection. Selection priority: port > camp > ship > unit. Selected units/ships moved with right-click. Right-click on allied Transport = auto-board. Stuck detection after 2s of no movement.

**MapGenerator** - Loads predefined map presets (IrridiumMap or AlabastaMap) on a 256x256 grid, 128px tiles. Camp positions are preset per map, shuffled deterministically via seed. No procedural camp placement. Biome tile IDs: Water=6, Sand=1, Grass=0, Forest=3, Rock=5, Snow=4. Deterministic seeded generation for multiplayer sync.

**CameraController** - Zoom (0.05x-2.0x), WASD/arrow pan, right-click drag, recenter with C/Home

**GameHUD** - Displays gold for selected camp, unit purchase buttons with prices, tier unlock button (1500 gold). Connected to SelectionManager for camp selection. Shows tier lock state per unit button.

### Key Patterns
- Signal-based communication for UI and networking
- Group-based team organization ("units", "team_1", "team_2", "camps")
- Deterministic systems via seeded random for network sync
- All network state changes use RPCs with Authority mode
- Production queue system with async unit spawning

## Known Bugs (from audit 2026-03-05)

- **OnDefenderDied() never called** (HIGH): Unit death does not signal the owning camp. Mutual-kill tracking (`_lastAttackerTeamId`) never triggered. Fix: call camp callback from `Unit.Combat.Die()`.
- **Support aura stacking unlimited** (MEDIUM): 10 Support units = +100 defense bonus, can make units unkillable. Fix: cap bonus at +40-50 in `Unit.Healing.GetSupportDefenseBonus()`.
- **HUD price mismatch** (HIGH): GameHUD.tscn displays wrong prices for AntiArmor (90 vs 120), Heavy (120 vs 150), Mortar (110 vs 130), Tank (150 vs 200). Fix: sync .tscn with UnitStats.cs values.
- **No gold refund on camp capture mid-production** (MEDIUM): Gold spent on queued units is lost if camp is captured. Fix: refund queue cost in SetTeam().
- **No multiplayer reconnection** (HIGH): Disconnect = end of game with no recovery path.
- **Gold desync risk in multiplayer** (MEDIUM): No RPC gold sync; diverges over time if a peer misses passive income ticks.

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
