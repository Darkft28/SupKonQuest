# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SupKonQuest is a strategy and conquest game built with Godot 4.5 and C# (.NET 8.0). It features predefined map presets (Irridium, Alabasta), ENet P2P multiplayer with Nakama relay fallback, a gold-based economy with unit/ship production queues, and a Utility AI system.

## Game Flow

1. Joueur spawn avec 1 camp + 100 or
2. GameManager donne 500 or/sec passif à chaque équipe ; chaque camp neutre génère 50 or/sec localement
3. Acheter des unités (Tier 1 par défaut) → file de production par camp (max 7)
4. Sélectionner unités → clic droit pour déplacer ou attaquer
5. Tuer les défenseurs d'un camp → attaquer le bâtiment → capture (+50 or bonus + 3 unités bonus)
6. Victoire : capturer tous les camps ennemis

## Key Scenes
- `Scenes/Game.tscn` - Scène de jeu principale (racine : `MapGenerator.cs`)
- `Scenes/Unit.tscn` - Prefab unité terrestre
- `Scenes/Ship.tscn` - Prefab navire
- `Scenes/camp_simple.tscn` - Prefab camp (Area2D, scale 4.5×)
- `Scenes/GameHUD.tscn` - HUD en surimpression (instancié dans CanvasLayer de Game.tscn)
- Point d'entrée : `Scenes/MainMenu.tscn` → `Scenes/GameModeMenu.tscn` → `Scenes/Lobby.tscn` → Game

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
- `Scripts/Units/` - Unit.cs + partials (Combat, Movement, Healing, Transport, Visuals, Audio), UnitStats, Projectile
- `Scripts/Ships/` - Ship.cs + partials (Combat, Movement, Transport, Visuals), ShipStats, ShipProjectile
- `Scripts/Camps/` - CampSimple.cs + partials (Production, Naval, Defense, Visuals)
- `Scripts/Map/` - MapGenerator, TerrainGenerator (static), CampPlacer (static), TerritoryManager, TerritoryConnectivity (static)
- `Scripts/Selection/` - SelectionManager
- `Scripts/Camera/` - CameraController
- `Scripts/Economy/` - GameManager, VictoryManager
- `Scripts/Network/` - NetworkManager, GameState, NetworkSync, NetworkEntityRegistry, NakamaService, NetworkCommandRouter
- `Scripts/AI/` - AIController (Utility AI, Easy/Medium/Hard, one instance per bot team)
- `Scripts/UI/` - GameHUD, LobbyUI, Minimap, MainMenu, GameModeMenu, LocalizationManager, AudioSettings, UIStyle
- `AI-implementation.md` - Notes de conception IA (naval et boss, stagger implémenté ; idées futures : personnalités)
- `Scenes/` - Godot scene files (.tscn)
- `Assets/` - Textures, sprites, unit characters

### Singleton Managers (AutoLoads in project.godot)
- **GameManager** - Gold economy, tiers, victory hooks (`GameManager.Instance`)
- **NetworkManager** - ENet multiplayer (hosting, joining, peer comm port 7777; LAN discovery UDP port 7778)
- **GameState** - Game flow (seed, `ActivePlayerCount`, `IsAIMode`, `IsOnline`, `IsFreeForAll`)
- **NakamaService** - Auth guest, matchmaking 2–8, lobby in-match, opcodes lobby relay (`4001`/`4002`), gameplay relay
- **LocalizationManager** - i18n (FR/EN/ES), signal `LanguageChanged`
- **AudioSettings** - Volume music/SFX with persistence

### Core Systems

**GameManager** - Gold economy per team. `PassiveGoldPerSecond = 500` given each second per team regardless of camp count. `CaptureBonus = 50` gold on capture. `StartingGold = 100`. `RegionBonusGold = 30` or/s if team controls all camps in a region. `RegionSpeedBonusPerRegion = 0.20f` (cumulative speed multiplier per full region). `MaxUnitsPerCamp = 10` (global cap = owned camps × 10). 3-tier unlock: Tier 1 default, Tier 2 manual purchase `Tier2Cost = 1500`, Tier 3 auto-unlock when controlling 100% of home region camps.

**Unit System** - CharacterBody2D with state machine (Idle, MovingToPoint, MovingToTarget, Attacking, AttackingCamp, Healing, MovingToTransport). Navigation layer 1 (ground only). Detection zone = `stats.Range + 400px`. Enemy search throttled to 0.5s. Stuck detection: 120 frames (≈2s) of insufficient movement.

| Type      | Price | HP  | Attack | Defense | Speed | Range | Prod. Time |
| --------- | ----- | --- | ------ | ------- | ----- | ----- | ---------- |
| Infantry  | 50g   | 100 | 15     | 10      | 150   | 100   | 2s         |
| Support   | 75g   | 80  | 8      | 5       | 120   | 100   | 3s         |
| Range     | 80g   | 70  | 20     | 5       | 100   | 300   | 3s         |
| Heal      | 100g  | 60  | 0      | 3       | 100   | 150   | 3s         |
| AntiArmor | 120g  | 80  | 35     | 8       | 90    | 120   | 4s         |
| Mortar    | 130g  | 50  | 40     | 3       | 60    | 400   | 4s         |
| Heavy     | 150g  | 150 | 25     | 20      | 70    | 100   | 5s         |
| Tank      | 200g  | 200 | 30     | 25      | 50    | 100   | 6s         |

Damage formula: `damage * 100f / (100f + totalDefense)` (scalable reduction, NOT subtractive). Neutral camp units have 1.5x HP. AntiArmor deals ×2 damage vs Heavy. Mortar AoE: 200px radius, 20 flat damage (ignores defense, skips shooter and allies). Support aura: +10 defense per Support ally within 200px, **capped at 40** (4 Supports max).

Unit tiers (affect production unlock):
- Tier 1: Infantry, Support, Range
- Tier 2: Heal, AntiArmor
- Tier 3: Mortar, Heavy, Tank

Ship tiers: Transport = Tier 1, Fregate + Destroyer = Tier 3.

**Unit.Audio.cs** - Pooled 2D spatial audio (`UnitSfxMaxDistance = 7500f`). Infantry: 4 random impact sounds, round-robin pool of 5 players. Range: arrow impact. Heal: magic sound. Support: war horn every 10s when in combat, max 2 simultaneous within 3200px. Uses `SFX` audio bus.

**CampSimple** - Area2D (scale 4.5×). `MaxHealth = 750f`. `MaxQueueSize = 7` units, `MaxShipQueueSize = 5` ships. Turret: `TurretDamage = 15f/s` at `TurretRange = 600f`, applied directly (no projectile). Territory alert timer (1.5s tick, 8s cooldown) sends Idle defenders toward intruders. Spawn radius 525px with deterministic angle `(CampId * 73856093) ^ (sequence * 19349663)`, spiral fallback if terrain blocked (Forest=3, Snow=4, Water=6). Port costs `PortCost = 500` gold, placed by clicking a coastal tile (auto-detects orientation from adjacent water count). `TrySpawnPort()` exists but is dead code (never called). Each camp has `RegionId` (3 on Irridium, 4 on Alabasta).

**Ships** - CharacterBody2D, navigation layer 2 (water only, tile id 6). 3 types:
| Type | HP | Attack | Defense | Speed | Capacity | Price | Prod |
|------|----|--------|---------|-------|----------|-------|------|
| Transport | 200 | 0 | 10 | 120 | 10 units | 150g | 5s |
| Fregate | 180 | 20 | 15 | 100 | — | 200g | 5s |
| Destroyer | 250 | 35 | 20 | 80 | — | 300g | 7s |

Transport is pacifist (never engages enemies). Destroyer textures: `Assets/Units/Ships/Destroyer/Destroyers_*.png`. Fregate: `Assets/Units/Ships/Frégate/frégate_*.png` (accented folder). Unload radius max 2000px, requires coastal tile with adjacent water in 3×3 grid.

**SelectionManager** - Click/box selection. Priority: port (<100px) → camp (<200px) → ship (<80px) → unit (<64px). Right-click: detects Transport within 150px (auto-board), enemy camp within 400px (AttackCamp), otherwise MoveTo. In Nakama relay mode, routes commands through `NetworkCommandRouter` instead of calling directly.

**MapGenerator** - Async load with 5-frame wait for NavigationServer2D to process nav regions. Decodes RLE presets (IrridiumMap/AlabastaMap). Builds 2 separate navmeshes (layer 1 = ground, layer 2 = water). `TerritoryGraph: Dictionary<int, HashSet<int>>` is the public region-adjacency graph used by unit AI to prioritize nearby camp targets. Walkable check: rejects tile ids 6 (water), 3 (forest), 5 (rock), 4 (snow), objects 100/101.

**TerritoryManager** - 256×256 grid of team ownership. Territory computed by BFS distance² from camp positions (radius 8 tiles default). Manual tile purchase: 200 gold/tile, BFS-connected to existing owned territory only (no isolated purchase). Orphan tiles on capture re-assigned via BFS connectivity. Visual: tint overlay (alpha 0.15) + golden borders (4px) drawn in `_Draw()`.

**TerritoryConnectivity** - Static class building bidirectional adjacency graph between regions (4-neighbor scan, land tiles only, BFS for transitive connectivity check `AreConnected()`).

**CameraController** - Zoom 0.05×–2.0× (min dynamic per map size). Pan: arrows/WASD at `PanSpeed=600f/Zoom`, right-drag. Zoom maintains mouse position. Intro animation: tween position + zoom from full map view to player camp (3–5s depending on map size, easing InOutCubic).

**GameHUD** - Leaderboard: collapsible (▼/▶ toggle), top 10, dynamic height, gold column added, rank colors (gold/silver/cream). Tier info label shows state and unlock cost. Lock overlay on unit buttons when tier not unlocked. Port button visible only when camp selected + no port + affordable.

### Networking Architecture

**ENet P2P (local/LAN):**
- Server = Team 1, Client = Team 2 (set via `GameState.LocalTeamId`)
- LAN discovery: client broadcasts `"SUPKONQUEST_DISCOVER:{CODE}"` UDP:7778, server replies `"SUPKONQUEST_FOUND:{CODE}:{PORT}"`
- `NetworkEntityRegistry`: global `string networkId → Node`, IDs = `"{peerId}_{counter}"`
- Deterministic defender IDs: `"camp_{campId}_unit_{index}"`, dynamic units: `"camp_{campId}_dyn_{sequence}"`
- Authority: server controls neutral camps and Team 1 entities; each peer controls their own team
- Reliable RPCs: spawn, death, damage, camp capture, transport board/unload, camp assignments
- Unreliable RPCs: 20Hz batched position/health/state sync (`SyncInterval = 0.05s`)
- Gold sync: `RpcSyncGold` every 10s (server→clients), tolerance 5 gold before applying correction

**Nakama relay (online matchmaking):**
- `NakamaService` autoload handles auth (persistent device ID at `user://nakama_device_id.txt`), matchmaking, socket
- `IsRelayMode()` = `GameState.IsOnline && NakamaService.IsSocketConnected`
- `NetworkCommandRouter` serializes commands as JSON with opcodes: BuyUnit=1001, MoveUnits=2001, AttackCamp=2002, CampCaptured=2003, GoldSnapshot=3001
- In relay mode, SelectionManager/GameHUD call `NetworkCommandRouter.Request*()` instead of direct camp calls
- Gold snapshot sent every 1s in relay mode for reconciliation

**Godot RPC limitation:** `bool[]` not supported as Variant; use `int[]` instead.

### AI System

**AIController** - One instance per bot team, created by `MapGenerator.InitAIController()`. Architecture: Utility AI scoring `ScoreCamp()` evaluated each tick.

Boss designation: MapGenerator finds the bot team most geographically distant from the player per region, assigns `difficulty = playerDifficulty + 1`. All other bots = Easy.

| Param | Easy | Medium | Hard |
|-------|------|--------|------|
| Tick (s) | 6.0 | 3.5 | 2.0 |
| MaxUnits | 8 | 16 | 28 |
| FirstAttackDelay (s) | 20 | 12 | 5 |
| ReactionDelay (s) | 5.0 | 1.5 | 0.3 |
| ErrorRate | 40% | 15% | 0% |
| ForcedAttackDelay (s) | 60 | 40 | 25 |
| DefenseRatio | 0 | 0.15 | 0.20 |

Composition targets: Easy=100% Infantry. Medium/Hard use mixed compositions (Infantry/Range/Support/Heal/AntiArmor). Hard counter-comp: if enemy has ≥3 Heavy → prioritize AntiArmor. Medium/Hard use rally-point strategy before attacking. Reactive defense: sends DefenseRatio% of idle units if a camp is at <60% HP or enemy within 700px.

Camp scoring: +2500 neutral, +(1-hpRatio)×1800 if damaged, +(5-defenders)×300, +3500 home region, −distance×0.4. Medium/Hard: port/ship production when a full region is controlled, naval offensives (Fregate/Destroyer). Land targets filtered via `TerritoryConnectivity.IsReachable`. Easy: infantry spam, nearest camp, no naval.

**Multijoueur en ligne (Nakama)** - Flux : `GameModeMenu` → `LobbyUI` → matchmaking → `JoinMatch` → lobby in-match (`MatchLobbyEntered`) → **`MatchStart` relay uniquement** → `StartOnlineGameFromMatch`. Pas d'IA (`IsAIMode`/`IsFreeForAll` remis à false via `ResetOnlineMatchFlags`). 1 camp/joueur, reste neutre (`GameManager.AssignCampsToPlayers`, `ActivePlayerCount`). Signaux : `MatchLobbyEntered`, `MatchLobbyTick`, `MatchStarting`. Module relay externe : countdown ~20s (+5s/join, start à 8) puis opcode `4002`.
Gestion déconnexion autoritaire serveur : `5002` (cleanup team) sur leave en partie. Le client applique l'événement serveur.
Test local : `--nakama-slot=1` / `2`.

### Key Patterns
- Partial classes by concern (Unit.Combat.cs, Unit.Movement.cs, etc.) — never mix concerns across partials
- Groups for entity lookup: `"units"`, `"ships"`, `"camps"`, `"team_<id>"`
- Deterministic seeding for all shuffles and spawn positions (Fisher-Yates with shared seed)
- Enemy search/pathfinding throttled (0.5s search, 64px nav recalc threshold, 3-frame cooldown)
- `IsLocalAuthority` bool on Unit/Ship controls which peer processes damage/death
- Relay mode detected at call site; same code paths, different dispatch (NetworkCommandRouter vs direct)
- Audio pooled with round-robin players to avoid create/destroy overhead
- Signal-based communication for UI and networking
- Production queue system with async unit spawning

## Known Bugs (from audit 2026-05-12)

- **HUD price mismatch** (HIGH): GameHUD.tscn hardcodes wrong prices: AntiArmor (90 vs 120), Heavy (120 vs 150), Mortar (110 vs 130), Tank (150 vs 200). Fix: update price Label nodes in GameHUD.tscn to match UnitStats.cs values.

## Fixed Bugs (for reference)

- **OnDefenderDied() never called** — `Unit.Combat.Die()` calls `OwnerCamp.OnDefenderDied()`.
- **No gold refund on camp capture mid-production** — `CampSimple.SetTeam()` calls `RefundProductionQueue()` and `RefundShipProductionQueue()`.
- **Support aura stacking unlimited** — `GetSupportDefenseBonus()` now caps at +40 (4 Supports × +10).
- **Gold desync in multiplayer** — `NetworkSync.RpcSyncGold()` runs every 10s server→clients with 5-gold tolerance.
- **Multi après solo : client avec IA vs humain** — Corrigé : `GameState.ResetOnlineMatchFlags()` + garde `InitAIController` si `IsOnline`.
- **Solo après multi : partie sans IA (PvP)** — Corrigé : `StartSoloGame()` atomique ; `ConfigureOfflineGame` ne remet plus `IsAIMode` à false.

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
