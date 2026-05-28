# SupKonQuest — Audit technique, conformité et architecture

## Résumé exécutif

SupKonQuest est un RTS 2D de conquête construit sous Godot 4.5 en C#/.NET 8, avec une architecture très orientée scènes Godot, autoloads globaux et classes partielles pour séparer les responsabilités. Le cœur du jeu est bien en place : génération déterministe de cartes, économie par camps, unités/ships, capture de territoires, IA solo, UI de menus/HUD, et un mode multijoueur en ligne basé sur Nakama avec relay de commandes.

Le projet est cependant **incomplet par rapport au cahier des charges** sur plusieurs points importants : il n’y a que **2 cartes** au lieu de 3, certains aspects de la conquête “mutuelle” et du départ d’un joueur ne sont pas complètement implémentés, et le bonus “map editor / 3D isométrique / social / Elo” est absent. Le code est globalement cohérent, mais il reste des écarts de conception entre les données UI et les stats réelles, ainsi que quelques zones de dette technique et de coupling fort.

---

## Tableau de conformité fonctionnelle

Légende : **Validée** / **Partiellement implémentée** / **Manquante**

| Exigence du cahier des charges | Statut | Preuve technique | Écart / remarque |
|---|---|---|---|
| RTS 2D de conquête, objectif = contrôler la carte | **Validée** | `README.md`, `Scenes/Game.tscn`, `Scripts/Economy/VictoryManager.cs` | Boucle de conquête bien présente, victoire quand une équipe contrôle tous les camps non neutres. |
| Jeu stable, jouable, intégrable dans un exécutable Godot/C# | **Validée** | `SupKonQuest.csproj`, `project.godot`, scènes principales | Stack cohérente Godot 4.5 + C# .NET 8. |
| Démarrage avec 1 camp + 100 or | **Validée** | `Scripts/Economy/GameManager.cs` (`StartingGold = 100`, `AssignCampsToPlayers`) | En solo le joueur reçoit 1 camp et 100 or ; en multi, 1 camp par joueur humain. |
| Camps initialement attribués aléatoirement et de manière déterministe | **Validée** | `GameManager.AssignCampsToPlayers()`, `ShuffleList(seed)`, `MapGenerator` | Le shuffle est déterministe via seed partagée. |
| Au moins 3 types de cartes différentes | **Manquante** | `Scripts/Map/Presets/IrridiumMap.cs`, `AlabastaMap.cs`, `GameState.MapType` | Seulement 2 cartes réelles. Aucun 3e preset exploitable. |
| Les camps génèrent de l’or régulièrement | **Validée** | `CampSimple._Process()`, `GeneratePassiveGold()`, `GameManager._Process()` | Chaque camp détenu rapporte +50 or/s, avec +500 or/s passif global. |
| Bonus de région quand une région est contrôlée | **Validée** | `GameManager.CheckRegionBonuses()`, `TerritoryManager.ApplyRegionConquest()` | Bonus +30 or/s quand tous les camps d’une région sont détenus. |
| Un camp produit des unités | **Validée** | `CampSimple.Production.cs` (`BuyUnit`, `ProcessProductionQueue`, `SpawnPurchasedUnit`) | File de production présente, coût et temps de production gérés. |
| Les ports produisent des navires | **Validée** | `CampSimple.Naval.cs` (`BuyShip`, `ProcessShipProductionQueue`, `SpawnShip`) | File navale séparée, max 5 navires actifs par équipe. |
| Neutral camps sont plus difficiles et offrent des unités avancées | **Partiellement implémentée** | `CampSimple.SetTeam()`, `SpawnUnits()`, `GameManager.GetUnitTier()` | Les camps neutres ont 1.5x HP et 4 défenseurs initiaux, mais ils ne produisent pas d’unités “avancées” spécifiques. |
| Les camps doivent être gardés par au moins une troupe | **Partiellement implémentée** | `CampSimple.SpawnUnits()`, `CampSimple.Defense.CaptureCamp()` | Les camps démarrent avec 4 défenseurs, mais l’invariant “au moins une troupe en permanence” n’est pas formellement garanti. |
| Si le dernier défenseur meurt, le camp est capturé par l’unité attaquante | **Partiellement implémentée** | `Unit.Combat.Die()`, `CampSimple.OnDefenderDied()`, `CaptureCamp()` | Le flux de mort/capture existe, mais la logique de capture dépend du HP du camp et pas directement du “dernier défenseur mort”. |
| Si mort mutuelle, le camp redevient neutre | **Manquante** | `Unit.Combat.Die()`, `CampSimple.Defense.OnDefenderDied()` | Le booléen `mutualKill` est transmis mais non exploité pour remettre le camp à l’état neutre. |
| Le bâtiment camp inflige des dégâts aux ennemis | **Validée** | `CampSimple.Defense.cs` (`ProcessTurret`, `AttackEnemiesInRange`) | Tourelle périodique, portée 600px, dégâts 5. |
| Le jeu se termine quand une équipe possède tous les camps non neutres | **Validée** | `VictoryManager.CheckVictoryCondition()` | Condition de victoire simple et robuste. |
| Si un joueur quitte, ses camps et unités deviennent neutres | **Manquante** | `supkonquest-server/match_handler.ts`, `NetworkManager.OnServerDisconnected()` | Les clients quittent ou reviennent au menu, mais il n’y a pas de redistribution automatique en neutre. |
| Les camps/unités neutres sont passifs : pas de production ou mobilité | **Partiellement implémentée** | `CampSimple.GeneratePassiveGold()`, `UpdateCampTimers()`, `Unit` selection/movement | Les camps neutres ne produisent pas via les timers, mais l’absence de mobilité des unités neutres n’est pas explicitement verrouillée par une règle dédiée. |
| 7 types d’unités : Infantry, Support, Heal, Range, Heavy, Anti-Armor, Mortar | **Partiellement implémentée** | `UnitStats.cs`, `GameHUD.tscn`, `GameManager.GetUnitTier()` | Les 7 types demandés existent, mais le projet ajoute aussi `Tank`, ce qui le détourne du cahier des charges strict. |
| Les unités ont prix, HP, dégâts, vitesse, portée, temps de prod | **Validée** | `UnitStats.cs`, `ShipStats.cs`, HUD | Les stats sont centralisées et exploitées par la prod/HUD. |
| Formule de dégâts : `attaque * 100 / (100 + defense)` | **Validée** | `Unit.Combat.TakeDamageFrom()`, `Ship.Combat.TakeDamageFrom()` | Formule conforme et réutilisée partout. |
| Support = aura / bonus temporaire activable | **Partiellement implémentée** | `Unit.Healing.cs` (`GetSupportDefenseBonus()`), `Unit.Audio.cs` | Aura de défense présente et plafonnée, mais pas de vraie compétence activable avec cooldown. |
| Heal = soin activable avec cooldown | **Partiellement implémentée** | `Unit.Healing.cs` (`ProcessHealingState()`) | Le soin automatique existe, mais pas un “spell activable” par le joueur. |
| Mortar = dégâts de zone avec atténuation selon distance au centre | **Partiellement implémentée** | `Projectile.cs` (`ApplyMortarSplash()`) | Splash présent, mais dégâts constants 20 au lieu d’une atténuation progressive par distance au centre. |
| Units librement déplaçables avec pathfinding | **Validée** | `Unit.Movement.cs`, `NavigationAgent2D`, `CameraController`, `SelectionManager` | Déplacement pointé + pathfinding via navmesh terrestre. |
| Sélection de groupe à la souris + clic droit pour ordonner | **Validée** | `SelectionManager.cs` | Box selection, priorités d’objets, mouvement à droite-clic. |
| Macros / raccourcis avancés pour groupes d’unités | **Manquante** | `SelectionManager.cs`, `GameHUD.cs` | Pas de système de groupes, ni de hotkeys de macro. |
| Transports, embarquement / débarquement, traversée de l’eau | **Validée** | `Ship.Transport.cs`, `SelectionManager.cs`, `Unit.Transport.cs` | Transport à capacité 10, unload sur côte, click droit sur transport allié. |
| 3 types de navires : Transport, Frigate, Destroyer | **Validée** | `ShipStats.cs`, `GameHUD.tscn`, `Ship.cs` | Les 3 types sont présents et branchés au gameplay. |
| Multijoueur en ligne avec matchmaking 2–8 joueurs | **Validée** | `NakamaService.cs`, `LobbyUI.cs`, `supkonquest-server/match_rpc.ts`, `match_handler.ts` | Flow matchmaking/lobby/match/start présent. |
| Le mode en ligne ne doit pas casser l’offline | **Validée** | `GameState.cs`, `NetworkSync.cs`, `README.md` | Les chemins offline et online sont séparés ; le legacy ENet est conservé. |
| 3 niveaux d’IA | **Validée** | `AIController.cs` (Easy/Medium/Hard) | Trois niveaux réels avec comportements distincts. |
| IA stratégique/agressive / plus ou moins optimisée | **Partiellement implémentée** | `AIController.cs`, `AI-implementation.md` | Utility AI et naval présents, mais encore assez scriptés ; personnalités IA distinctes absentes. |
| Internationalisation minimum 3 langues | **Validée** | `LocalizationManager.cs` | FR/EN/ES, signal de refresh UI. |
| Leaderboard affichant camps / income / régions | **Validée** | `GameHUD.cs` (`UpdateLeaderboard`) | Classement par camps + territoires, affiché in-game. |
| Bonus : isometric 3D | **Manquante** | Aucune scène / script 3D | Le projet reste purement 2D. |
| Bonus : map editor | **Manquante** | Aucun outil d’édition de carte | Les cartes sont codées en RLE dans les presets. |
| Bonus : online social / friends / messaging / profile / Elo | **Manquante** | `NakamaService.cs` | Auth invité et matchmaking בלבד ; pas de social layer. |

---

## Architecture

### Pattern général

L’architecture est un **mix scène Godot + singletons autoload + classes partielles**. Le jeu est piloté par une scène principale (`Game.tscn`) qui instancie la carte, les unités, le HUD et un hub réseau (`NetworkSync`). Les systèmes transverses vivent en autoloads : économie (`GameManager`), état de partie (`GameState`), réseau legacy (`NetworkManager`), online Nakama (`NakamaService`), localisation (`LocalizationManager`), audio (`AudioSettings`).

### Sous-systèmes majeurs

- **Carte / territoires**  
  `MapGenerator` charge un preset RLE, construit les navmeshes terrestre et maritime, place les camps, puis initialise `TerritoryManager` et l’IA.
- **Gameplay camps**  
  `CampSimple` est découpé en plusieurs partials : défense, production, naval, visuals.
- **Gameplay unités / navires**  
  `Unit` et `Ship` sont aussi découpés en partials pour séparer combat, mouvement, transport, audio, visuels.
- **Réseau**  
  Deux couches coexistent :
  1. **legacy ENet** via `NetworkManager` / `NetworkSync`,
  2. **online Nakama relay** via `NakamaService` + `NetworkCommandRouter`.
- **UI**  
  Menus, HUD, mini-map, sélection, localisation et réglages audio sont isolés dans `Scripts/UI`.

### Stack technique

- **Moteur** : Godot 4.5
- **Langage client** : C# (.NET 8.0)
- **Serveur online** : TypeScript Nakama
- **Modèle réseau** : relay de commandes + synchronisation légère des états
- **Données de map** : RLE statique embarqué dans le code

### Démarrage de l’exécution

1. Le jeu démarre sur `Scenes/MainMenu.tscn`.
2. Le joueur choisit solo ou multi via `GameModeMenu`.
3. `GameState` configure le mode, la seed, la carte et l’IA.
4. `Game.tscn` est chargée.
5. `MapGenerator._Ready()` construit la carte, puis `GameManager.OnMapGenerationComplete()` assigne les camps.
6. En solo, `AIController` est instancié. En multi, `NakamaService`/`NetworkSync` prennent le relais.

---

## Structure du projet

```text
SupKonQuest/
├── Scenes/
│   ├── MainMenu.tscn          — point d’entrée
│   ├── GameModeMenu.tscn      — choix solo/multi + paramètres
│   ├── Lobby.tscn             — lobby Nakama
│   ├── Game.tscn              — scène principale de jeu
│   ├── GameHUD.tscn           — HUD gameplay / leaderboard / port / tier
│   ├── camp_simple.tscn       — prefab camp
│   ├── Unit.tscn              — prefab unité terrestre
│   ├── Ship.tscn              — prefab navire
│   ├── Projectile.tscn        — projectile terrestre
│   └── ShipProjectile.tscn    — projectile naval
├── Scripts/
│   ├── Economy/
│   │   ├── GameManager.cs     — économie, camps, tiers, bonus, victoire
│   │   └── VictoryManager.cs  — condition de victoire / écran victoire
│   ├── Network/
│   │   ├── GameState.cs       — état de partie, seed, mode, carte
│   │   ├── NetworkManager.cs  — legacy ENet + découverte LAN
│   │   ├── NetworkSync.cs     — RPC sync gameplay
│   │   ├── NetworkCommandRouter.cs — relay Nakama gameplay
│   │   ├── NakamaService.cs   — auth, matchmaking, lobby, relay
│   │   └── NetworkEntityRegistry.cs — map NetworkId -> Node
│   ├── Map/
│   │   ├── MapGenerator.cs    — génération carte, navmesh, IA
│   │   ├── TerrainGenerator.cs — altérations terrain / sprites objets
│   │   ├── TerritoryManager.cs — territoire visuel / port placement
│   │   ├── TerritoryConnectivity.cs — graphe de régions
│   │   ├── CampPlacer.cs      — pose des camps depuis presets
│   │   └── Presets/
│   │       ├── IrridiumMap.cs
│   │       └── AlabastaMap.cs
│   ├── Camps/
│   │   ├── CampSimple.cs
│   │   ├── CampSimple.Defense.cs
│   │   ├── CampSimple.Production.cs
│   │   ├── CampSimple.Naval.cs
│   │   └── CampSimple.Visuals.cs
│   ├── Units/
│   │   ├── Unit.cs
│   │   ├── Unit.Combat.cs
│   │   ├── Unit.Movement.cs
│   │   ├── Unit.Transport.cs
│   │   ├── Unit.Healing.cs
│   │   ├── Unit.Visuals.cs
│   │   ├── Unit.Audio.cs
│   │   ├── UnitStats.cs
│   │   └── Projectile.cs
│   ├── Ships/
│   │   ├── Ship.cs
│   │   ├── Ship.Combat.cs
│   │   ├── Ship.Movement.cs
│   │   ├── Ship.Transport.cs
│   │   ├── Ship.Visuals.cs
│   │   ├── ShipStats.cs
│   │   └── ShipProjectile.cs
│   ├── AI/
│   │   └── AIController.cs   — Utility AI Easy/Medium/Hard
│   ├── UI/
│   │   ├── MainMenu.cs
│   │   ├── GameModeMenu.cs
│   │   ├── LobbyUI.cs
│   │   ├── GameHUD.cs
│   │   ├── LocalizationManager.cs
│   │   ├── Minimap.cs
│   │   └── AudioSettings.cs
│   ├── Selection/
│   │   └── SelectionManager.cs
│   └── Camera/
│       └── CameraController.cs
└── supkonquest-server/
    ├── main.ts               — init runtime Nakama
    ├── lobby.ts              — countdown lobby + start match
    ├── match_handler.ts      — match lifecycle + relay validation
    ├── match_rpc.ts          — RPC stub de matchmaking
    ├── ai.ts                 — ancienne expérimentation, aujourd’hui non utilisée
    └── NAKAMA_SERVER_TS.md   — consignes serveur
```

---

## Data flow principal

### 1) Solo / offline

1. `MainMenu` → `GameModeMenu`
2. `GameModeMenu.OnLaunchPressed()` appelle `GameState.StartSoloGame()`
3. `GameState` génère la seed et charge `Game.tscn`
4. `MapGenerator._Ready()` charge le preset, construit les navmeshes et place les camps
5. `GameManager.OnMapGenerationComplete()` assigne les camps et initialise les équipes
6. `MapGenerator.InitAIController()` crée un `AIController` par équipe bot
7. Le joueur sélectionne des entités via `SelectionManager`, puis `GameHUD` déclenche achats / tiers / port
8. `Unit` / `Ship` exécutent déplacement, combat, capture et transport

### 2) Multijoueur relay Nakama

1. `LobbyUI` appelle `NakamaService.AuthenticateGuestAsync()`
2. `NakamaService.StartMatchmakingAsync()` envoie un ticket de matchmaker
3. Le runtime serveur (`match_rpc.ts`, `main.ts`) crée un match relay
4. `NakamaService.OnReceivedMatchmakerMatched()` rejoint le match
5. Le lobby in-match attend `LobbyTick` puis `MatchStart`
6. `GameState.StartOnlineGameFromMatch()` charge `Game.tscn`
7. Le client envoie ses commandes via `NetworkCommandRouter`
8. Le serveur valide l’enveloppe, séquence, type et structure JSON, puis relaie aux autres clients
9. Les entités locales appliquent les états réseau via `NetworkSync` / `NetworkEntityRegistry`

### 3) Capture de camp

1. Une unité cible un camp via `Unit.AttackCamp()`
2. Le camp vérifie ses défenseurs via `CampSimple.AreAllUnitsDefeated()`
3. Les défenseurs meurent ou sont supprimés du groupe
4. Le camp subit ensuite des dégâts
5. `CampSimple.Defense.CaptureCamp()` change d’équipe, rembourse la file de prod, donne le bonus d’or, spawne les bonus units
6. `TerritoryManager.RefreshTerritory()` recalcule le tint et les frontières
7. `VictoryManager` vérifie si la partie est terminée

---

## Abstractions clés

### `GameManager`
- **Fichier** : `Scripts/Economy/GameManager.cs`
- **Rôle** : source d’autorité pour l’or, les bonus de région, les tiers et l’assignation initiale des camps
- **Interface** : `InitializeTeam()`, `SpendGold()`, `AddGold()`, `UnlockTier2()`, `GetUnlockedTier()`, `GetSpeedMultiplier()`
- **Cycle de vie** : autoload persistant ; réinitialisé à chaque nouvelle map
- **Utilisé par** : camps, HUD, IA, victoire, capture

### `GameState`
- **Fichier** : `Scripts/Network/GameState.cs`
- **Rôle** : état de session, seed, carte, mode offline/online, difficulté IA, team locale
- **Interface** : `StartSoloGame()`, `ConfigureOnlineLobby()`, `StartOnlineGameFromMatch()`, `GetEffectiveMapSeed()`
- **Cycle de vie** : autoload persistant, reconfiguré à chaque session
- **Utilisé par** : `MapGenerator`, `NakamaService`, `GameHUD`, `SelectionManager`, `CameraController`

### `MapGenerator`
- **Fichier** : `Scripts/Map/MapGenerator.cs`
- **Rôle** : génération et assemblage de la carte, des navmeshes et des camps
- **Interface** : `_Ready()`, `GenererMap()`, `BuildNavigationMesh()`, `BuildWaterNavigationMesh()`, `InitAIController()`
- **Cycle de vie** : instancié dans `Game.tscn`
- **Utilisé par** : gameplay global, IA, navigation, territoire

### `CampSimple`
- **Fichier** : `Scripts/Camps/CampSimple.cs` + partials
- **Rôle** : entité centrale de conquête, économie locale, production, défense, port, réseau
- **Interface** : `BuyUnit()`, `BuyShip()`, `PlacePortAt()`, `CaptureCamp()`, `SetTeam()`, `ApplyRemoteCapture()`, `RefundProductionQueue()`
- **Cycle de vie** : camp persistant pendant la partie, créé depuis les presets
- **Utilisé par** : unités, IA, HUD, territoire, réseau

### `Unit`
- **Fichier** : `Scripts/Units/Unit.cs` + partials
- **Rôle** : logique d’unité terrestre, combat, heal, transport, pathfinding, visuels, audio
- **Interface** : `MoveTo()`, `AttackCamp()`, `TakeDamageFrom()`, `MoveToTransport()`, `Heal()`, `GetSupportDefenseBonus()`
- **Cycle de vie** : instanciée depuis camp, parfois détruite ou embarquée
- **Utilisé par** : camp, sélection, IA, réseau, projectiles

### `Ship`
- **Fichier** : `Scripts/Ships/Ship.cs` + partials
- **Rôle** : logique navale, combat, transport, pathfinding maritime
- **Interface** : `MoveTo()`, `MoveToUnload()`, `BoardUnit()`, `UnloadUnits()`, `TakeDamageFrom()`
- **Cycle de vie** : instancié par un port, peut embarquer des unités
- **Utilisé par** : sélection, réseau, gameplay naval

### `AIController`
- **Fichier** : `Scripts/AI/AIController.cs`
- **Rôle** : IA solo utility-based, avec 3 difficultés et comportements navals
- **Interface** : `Initialize()`, `_Process()`, `ManageProduction()`, `ManageCombat()`, `ChooseTarget()`
- **Cycle de vie** : créé par équipe bot dans `MapGenerator.InitAIController()`
- **Utilisé par** : solo, debug, leaderboard

### `NakamaService`
- **Fichier** : `Scripts/Network/NakamaService.cs`
- **Rôle** : auth invité, matchmaking, lobby in-match, relay Nakama, gestion des presences
- **Interface** : `AuthenticateGuestAsync()`, `StartMatchmakingAsync()`, `SendMatchCommandAsync()`, `Disconnect()`
- **Cycle de vie** : autoload persistant, connecté/déconnecté selon session
- **Utilisé par** : lobby, HUD, gameplay relay

### `NetworkSync`
- **Fichier** : `Scripts/Network/NetworkSync.cs`
- **Rôle** : RPC gameplay legacy / sync d’état
- **Interface** : `SendSpawnUnit()`, `SendEntityDied()`, `SendUnitDamage()`, `SendCampCaptured()`, `SendSyncCampAssignments()`
- **Cycle de vie** : node enfant de `Game.tscn`
- **Utilisé par** : unités, navires, camps, serveur ENet legacy

### `SelectionManager`
- **Fichier** : `Scripts/Selection/SelectionManager.cs`
- **Rôle** : sélection souris, box selection, ordres de déplacement, embarquement
- **Interface** : `GetSelectedCamp()`, `GetSelectedPort()`, `GetSelectedUnits()`, `HandleRightClick()`
- **Cycle de vie** : node de la scène de jeu
- **Utilisé par** : HUD, gameplay direct

### `GameHUD`
- **Fichier** : `Scripts/UI/GameHUD.cs`
- **Rôle** : affichage de l’or, boutons d’achat, port, tiers, leaderboard, déconnexion
- **Interface** : `_Process()`, `UpdateUnitButtons()`, `UpdateShipButtons()`, `UpdateLeaderboard()`
- **Cycle de vie** : instance UI de `Game.tscn`
- **Utilisé par** : joueur, sélection, réseau

---

## Analyse technique Godot C# : points forts et risques

### Points solides

- **Séparation en partial classes** : très bonne décision pour `Unit`, `Ship`, `CampSimple`. Cela limite la taille de chaque fichier et isole les comportements métier.
- **Utilisation des groupes Godot** : `camps`, `units`, `ships`, `team_X` simplifie les recherches, au prix d’une dépendance implicite.
- **NavigationAgent2D** : le pathfinding est bien utilisé et la carte construit deux navmeshes distincts, ce qui est propre pour terre / eau.
- **`CallDeferred` dans les chemins réseau sensibles** : `GameState`, `NakamaService` et les callbacks Nakama l’utilisent là où c’est nécessaire.
- **`IsInstanceValid` partout** : bonne hygiène pour les entités détruites ou déconnectées.

### Risques / problèmes Godot spécifiques

#### 1) `CampSimple` repose beaucoup sur la présence de nœuds enfants précis
`CampSimple` cherche `TurretTimer`, `TerritoryAlertTimer`, `TerritoryAlertCooldownTimer`, `CampUI`, etc.  
Si la scène diverge de `Scenes/camp_simple.tscn`, la logique se dégrade silencieusement. C’est classique dans Godot, mais cela augmente la fragilité des scènes.

#### 2) Couplage direct aux autoloads
Beaucoup de scripts accèdent directement à `GameManager.Instance`, `GameState`, `NakamaService.Instance`, `TerritoryManager.Instance`.  
C’est pratique, mais ça remplace des contrats explicites par des singletons globaux, ce qui augmente :
- le couplage,
- la difficulté de test,
- les effets de bord cachés.

#### 3) Risque d’états non cohérents entre scènes
`GameManager` et `GameState` sont persistants, mais les scènes sont recréées.  
Le code réinitialise bien plusieurs choses, mais il reste des dépendances à l’ordre d’initialisation :
- `GameManager.InitializeCamps()` dépend du groupe `camps`,
- `MapGenerator` dépend du `GameState`,
- `HUD` dépend de `SelectionManager`.

#### 4) `Unit` et `Ship` ont des machines d’état fortement imbriquées
C’est efficace, mais on voit des transitions codées en dur, avec beaucoup de booléens et de flags de pathfinding.  
C’est robuste pour un prototype avancé, mais difficile à étendre sans régression.

#### 5) `SelectionManager` n’a pas de système de désélection “macro”
Le flux de sélection est clair, mais il n’y a aucun système de groupes numérotés, de raccourcis de sélection, ni de gestion de macros demandée par le cahier des charges.

#### 6) `MapSizePreset` est trompeur
`GameState.MapSize` existe, et `Minimap` l’utilise, mais `MapGenerator` génère une carte fixe 256x256.  
Autrement dit, le “preset de taille” n’influence pas réellement la génération de la map de jeu.

#### 7) Surcharge de travail dans `_Process`
Plusieurs systèmes recalculent en continu :
- `GameManager._Process()` pour l’économie et la victoire,
- `GameHUD._Process()` pour les boutons / leaderboard,
- `Minimap._Process()` qui redessine chaque frame,
- `AIController._Process()`.

C’est acceptable sur une RTS 2D modeste, mais il faut surveiller la charge si la densité d’unités augmente.

---

## Design, qualité de code et Clean Code

### Ce qui est bien

- **Nommage globalement cohérent** : PascalCase pour types/méthodes, `_camelCase` pour champs privés.
- **Séparation par domaine** : `Map`, `Camps`, `Units`, `Ships`, `Network`, `UI`, `AI`.
- **Données métier centralisées** : `UnitStats` et `ShipStats` évitent de disperser les valeurs.
- **Côté serveur Nakama** : validation des payloads, séquence monotone, refus des opcodes inattendus, matchmaking lisible.
- **Commentaires utiles** : certains fichiers expliquent vraiment la raison des choix, pas juste le code.

### Ce qui pose problème

#### Couplage élevé
Le projet est fortement dépendant :
- des autoloads,
- des groupes Godot,
- des noms de nœuds de scène,
- des chaînes de type (`"Infantry"`, `"Fregate"`, etc.).

Cela marche, mais la maintenabilité à long terme dépendra d’une discipline stricte.

#### Cohérence UI / stats pas toujours respectée
Dans `Scenes/GameHUD.tscn`, plusieurs prix affichés ne correspondent pas aux valeurs de `UnitStats.cs` :
- AntiArmor : HUD = 90, code = 120
- Heavy : HUD = 120, code = 150
- Mortar : HUD = 110, code = 130
- Tank : HUD = 150, code = 200

C’est un défaut important car le HUD ment au joueur.

#### Surcharges de responsabilité
- `GameHUD` fait : économie, leaderboard, déconnexion, tiers, ports, prix, affichage.
- `GameManager` fait : économie, assignation, bonus, tiers, victoire.
- `CampSimple` fait : prod, défense, port, capture, réseau, visual, loot.

Ces objets sont fonctionnels, mais trop “gros”. Le code reste lisible grâce aux partials, mais la responsabilité métier reste très concentrée.

#### Manque de contrats typés forts
Une grande partie du gameplay dépend de chaînes de caractères pour les types d’unités et de navires.  
Un `enum` ou un objet de configuration typé serait plus sûr, plus testable et moins fragile.

---

## Non-obvious behaviors & design decisions

### 1) Le système de cartes est déterministe
La seed de match est dérivée du `matchId` ou transmise explicitement.  
C’est indispensable pour la cohérence réseau : même carte, mêmes placements, mêmes camps. Le code le fait correctement.

### 2) Le mode online repose sur du relay, pas sur un serveur gameplay autoritaire
Le serveur Nakama :
- valide l’enveloppe et la séquence,
- rejette les payloads incohérents,
- relaie les commandes,
mais ne simule pas toute la logique du gameplay.  
C’est un choix de simplicité, compatible avec l’architecture actuelle.

### 3) Les camps neutres sont simulés différemment selon le mode réseau
Le code distingue :
- offline,
- legacy ENet,
- relay Nakama.

Cette diversité est utile, mais elle rend les chemins de capture et de synchronisation faciles à casser si on modifie une seule branche.

### 4) La carte possède deux couches de navigation
Une couche terrestre pour les unités et une couche maritime pour les bateaux.  
C’est un très bon choix structurel pour un RTS avec contraintes d’eau.

### 5) Les bonus de territoire ne sont pas des “tiles achetables”
Le territoire visuel autour des camps est purement dérivé de la présence des camps.  
On ne conquiert pas des cases directement ; on conquiert des camps, puis le territoire suit. Cela simplifie le gameplay et le réseau.

### 6) La capture de port est géométrique, pas contextuelle
Le port est placé en bord de côte via calcul de tuiles adjacentes à l’eau.  
Le code évite de dépendre du camp lui-même, ce qui permet à l’IA de placer un port le long de n’importe quelle portion du littoral contrôlé.

---

## Module reference

| Fichier | Rôle |
|---|---|
| `Scripts/Economy/GameManager.cs` | économie, gold, bonus de région, tiers, victoire hooks |
| `Scripts/Economy/VictoryManager.cs` | victoire et écran de fin |
| `Scripts/Network/GameState.cs` | état de partie, mode, seed, carte, joueur local |
| `Scripts/Network/NetworkManager.cs` | legacy ENet + découverte LAN |
| `Scripts/Network/NetworkSync.cs` | RPC gameplay legacy |
| `Scripts/Network/NakamaService.cs` | auth/matchmaking/lobby/relais Nakama |
| `Scripts/Network/NetworkCommandRouter.cs` | enveloppes de commandes relay et application locale |
| `Scripts/Network/NetworkEntityRegistry.cs` | résolution NetworkId → Node |
| `Scripts/Map/MapGenerator.cs` | génération de carte, navmesh, IA |
| `Scripts/Map/TerrainGenerator.cs` | variantes de terrain et sprites objets |
| `Scripts/Map/TerritoryManager.cs` | territoire visuel et placement port |
| `Scripts/Map/TerritoryConnectivity.cs` | graphe des régions terrestres |
| `Scripts/Map/CampPlacer.cs` | placement des camps depuis le preset |
| `Scripts/Map/Presets/IrridiumMap.cs` | preset carte 1 |
| `Scripts/Map/Presets/AlabastaMap.cs` | preset carte 2 |
| `Scripts/Camps/CampSimple.cs` | camp central + état, capture, équipe |
| `Scripts/Camps/CampSimple.Defense.cs` | tourelle, réaction à l’intrusion, capture |
| `Scripts/Camps/CampSimple.Production.cs` | production unités, file, refund |
| `Scripts/Camps/CampSimple.Naval.cs` | ports, navires, placement, production navale |
| `Scripts/Camps/CampSimple.Visuals.cs` | UI du camp, labels, couleurs |
| `Scripts/Units/Unit.cs` | logique noyau des unités terrestres |
| `Scripts/Units/Unit.Combat.cs` | attaque, dégâts, projectiles, mort |
| `Scripts/Units/Unit.Movement.cs` | mouvement, navigation, blocage |
| `Scripts/Units/Unit.Transport.cs` | embarquement transport |
| `Scripts/Units/Unit.Healing.cs` | heal + aura support |
| `Scripts/Units/Unit.Visuals.cs` | sprite, collision, dessin |
| `Scripts/Units/Unit.Audio.cs` | SFX combat |
| `Scripts/Units/UnitStats.cs` | stats des unités |
| `Scripts/Ships/Ship.cs` | noyau des navires |
| `Scripts/Ships/Ship.Combat.cs` | attaque navale, dégâts |
| `Scripts/Ships/Ship.Movement.cs` | mouvement maritime |
| `Scripts/Ships/Ship.Transport.cs` | embarquement / débarquement |
| `Scripts/Ships/ShipStats.cs` | stats navires |
| `Scripts/Ships/ShipProjectile.cs` | projectiles navals |
| `Scripts/AI/AIController.cs` | IA solo utility-based |
| `Scripts/UI/GameHUD.cs` | HUD, achat, leaderboard, tiers |
| `Scripts/UI/LocalizationManager.cs` | FR/EN/ES, signaux |
| `Scripts/UI/LobbyUI.cs` | lobby Nakama |
| `Scripts/UI/MainMenu.cs` | menu principal |
| `Scripts/UI/GameModeMenu.cs` | choix solo/multi + settings |
| `Scripts/UI/Minimap.cs` | minimap interactive |
| `Scripts/UI/AudioSettings.cs` | musique / SFX |
| `Scripts/Selection/SelectionManager.cs` | sélection et ordres |
| `Scripts/Camera/CameraController.cs` | caméra, zoom, clamp |
| `supkonquest-server/main.ts` | init Nakama runtime |
| `supkonquest-server/lobby.ts` | countdown lobby |
| `supkonquest-server/match_handler.ts` | validation relay et match loop |
| `supkonquest-server/match_rpc.ts` | RPC matchmaking |
| `supkonquest-server/NAKAMA_SERVER_TS.md` | consignes serveur |

---

## Analyse du réseau et du serveur Nakama

### Points bien faits

- **Matchmaking adapté au client** : le client demande un matchmaker avec `game=supkonquest` et `mode=relay`.
- **Lobby in-match** : la partie n’est lancée que sur `MatchStart`, pas directement au join.
- **Validation serveur** :
  - enveloppe (`senderUserId`, `sequence`) obligatoire,
  - séquence strictement croissante,
  - vérification des types,
  - rejet des opcodes inconnus,
  - rejet tant que le match n’a pas démarré.
- **Format des payloads simple** : JSON camelCase, lisible côté Godot.
- **Conservation du legacy** : le mode ENet ancien n’est pas supprimé.

### Limites

- **Le serveur reste un relais, pas un serveur autoritaire complet**.
- **Pas de reconnexion mid-game**.
- **Pas de layer social** (amis, messages, profil, Elo).
- **Le runtime TS externe doit rester synchronisé avec le client** : l’architecture dépend d’un contrat strict.

---

## Suggestion de lecture pour un nouveau développeur

1. `README.md` — comprendre le produit, le mode solo/multi et les promesses fonctionnelles.
2. `Scripts/Network/GameState.cs` — comprendre le cycle de vie des parties.
3. `Scripts/Map/MapGenerator.cs` — voir comment la carte et les camps apparaissent.
4. `Scripts/Economy/GameManager.cs` — comprendre économie, bonus et tiers.
5. `Scripts/Camps/CampSimple.cs` + partials — voir capture, prod, défense, port.
6. `Scripts/Network/NakamaService.cs` — comprendre le flux online.

---

## Plan d’action priorisé

### Critique
1. **Ajouter un 3e preset de carte jouable**
   - Le cahier des charges exige 3 maps.
   - Aujourd’hui, seulement `Irridium` et `Alabasta` sont exploitables.

2. **Corriger les écarts de conformité capture / neutralité**
   - Implémenter la logique “mort mutuelle => camp neutre”.
   - Implémenter la neutralisation des camps et unités quand un joueur quitte.
   - Vérifier que le dernier défenseur réellement vivant conditionne la capture comme demandé.

3. **Aligner le HUD avec les vraies stats**
   - Les prix de `GameHUD.tscn` sont faux pour plusieurs unités.
   - Le joueur voit des prix différents de la logique métier.

4. **Supprimer ou intégrer proprement l’unité `Tank`**
   - Le cahier des charges ne la prévoit pas.
   - Soit on l’assume comme extension de design, soit on la retire pour coller à la spec.

### Majeur
5. **Rendre les “spells” support/heal réellement activables**
   - Aujourd’hui, ce sont des effets automatiques ou passifs.
   - Le cahier des charges attend des activations avec cooldown.

6. **Corriger le mortar pour respecter le modèle de dégâts demandé**
   - Le splash est présent, mais pas l’atténuation par distance au centre.

7. **Rendre le mode réseau plus robuste sur le cycle de session**
   - Gérer proprement déconnexion, retour menu, nettoyage d’état, et éventuellement reconnexion.

8. **Introduire une vraie configuration de cartes / presets**
   - Les `MapSizePreset` existent mais ne pilotent pas la génération réelle.
   - La génération devrait être un vrai paramètre de session.

### Moyenne
9. **Réduire le couplage aux singletons**
   - Introduire davantage de signaux/contrats dédiés.
   - Cela améliorera testabilité et maintenabilité.

10. **Factoriser les types d’unités et navires**
    - Remplacer les chaînes par des enums / objets de config typés.
    - Réduire les erreurs de frappe et les incohérences UI/code.

11. **Ajouter les macros / groupes de sélection**
    - Requis par le cahier des charges pour le micro-management.
    - Très utile pour la jouabilité RTS.

12. **Documenter le contrat exact du relay Nakama**
    - Payloads, séquence, transitions de lobby, règles de refus.
    - Cela évitera les divergences entre client et serveur TS.

### Mineure
13. **Nettoyer le code mort / variables inutilisées**
    - Exemple : variables calculées mais non exploitées dans l’HUD.
    - Exemple : certains chemins legacy ou helpers peu utilisés.

14. **Standardiser les commentaires et les textes métier**
    - La base est très francophone, mais certains noms techniques restent hybrides FR/EN.
    - Harmoniser aiderait l’onboarding.

15. **Améliorer les tests de validation**
    - Même sans suite automatisée complète, prévoir des scénarios reproductibles pour :
      - capture,
      - port,
      - matchmaking,
      - déconnexion,
      - victoire,
      - neutralité.

---

## Conclusion

SupKonQuest a déjà une base technique solide et cohérente avec son objectif de RTS 2D Godot/C#. La boucle principale, l’économie, la conquête, les unités, les navires, l’IA et le multi relay sont réellement présents, pas seulement esquissés. En revanche, le projet n’est **pas encore conforme à 100%** au cahier des charges : il manque une 3e map, la gestion de la neutralité au départ d’un joueur, la capture mutuelle, les vrais activables support/heal, et plusieurs bonus demandés ne sont pas présents.

Si l’objectif est d’atteindre une version “parfaitement conforme”, les priorités sont claires : **compléter la conformité gameplay**, **fiabiliser les écarts UI/logique**, puis **réduire le couplage et formaliser les contrats réseau**.
