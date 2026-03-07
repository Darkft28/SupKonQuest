# SupKonQuest

Jeu de strategie et de conquete en temps reel developpe avec Godot 4.5 et C# (.NET 8.0).

## Description

SupKonQuest est un RTS (Real-Time Strategy) ou le joueur doit capturer des camps sur une carte generee proceduralement. On commence avec un camp et un peu d'or, on produit des unites terrestres et navales, et on part a la conquete des camps adverses. Le jeu propose un mode solo, un mode contre IA (3 niveaux) et un mode multijoueur en reseau local.

Le principe : chaque camp genere de l'or passivement, cet or permet d'acheter des unites, et ces unites servent a capturer d'autres camps. Controler une region entiere rapporte un bonus. Le joueur qui controle tous les camps gagne.

## Technologies

**Godot 4.5** : moteur open-source, leger et bien adapte au 2D. Son systeme de scenes et de noeuds permet de structurer le projet de maniere modulaire. Chaque element (unite, camp, carte) est une scene reutilisable.

**C# (.NET 8.0)** : typage statique, structures de donnees .NET (`Dictionary`, `Queue`, `List`). Aucune dependance NuGet externe.

**ENet** : protocole reseau UDP fiable integre dans Godot (retransmission des paquets perdus, ordonnancement). Port 7777 pour le jeu, 7778 pour la decouverte LAN via UDP broadcast.

## Prerequis

- [Godot 4.5](https://godotengine.org/) avec le support .NET (C#)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)

## Installation

```bash
git clone https://github.com/Darkft28/SupKonQuest.git
cd SupKonQuest
dotnet build SupKonQuest.csproj
```

Ensuite ouvrir le projet dans Godot 4.5 et lancer avec F5.

## Structure du projet

```
SupKonQuest/
├── Assets/
│   ├── Map/                    # Textures des biomes
│   ├── Objects/                # Sprites camps, arbres, montagnes
│   └── Units/
│       ├── Characters/         # 8 types d'unites (4 directions chacun)
│       └── Ships/              # 3 types de navires (Fregate, Destroyer, Transport)
├── Scenes/
│   ├── MainMenu.tscn
│   ├── GameModeMenu.tscn       # Choix mode (Solo / Multi / IA)
│   ├── Lobby.tscn              # Lobby multijoueur LAN
│   ├── Game.tscn               # Scene principale du jeu
│   ├── GameHUD.tscn            # Interface HUD (or, boutons d'achat)
│   ├── Unit.tscn               # Prefab unite terrestre
│   ├── Ship.tscn               # Prefab navire
│   └── camp_simple.tscn        # Prefab camp
├── Scripts/
│   ├── Units/                  # Unit.cs + partials (Combat, Movement, Healing, Transport, Visuals)
│   ├── Ships/                  # Ship.cs + partials (Combat, Movement, Transport, Visuals)
│   ├── Camps/                  # CampSimple.cs + partials (Production, Naval, Defense, Visuals)
│   ├── Map/                    # MapGenerator, TerrainGenerator, CampPlacer, TerritoryManager
│   ├── Selection/              # SelectionManager
│   ├── Camera/                 # CameraController
│   ├── Economy/                # GameManager, VictoryManager
│   ├── Network/                # NetworkManager, GameState, NetworkSync, NetworkEntityRegistry
│   ├── AI/                     # AIController (Easy/Medium/Hard)
│   └── UI/                     # GameHUD, LobbyUI, Minimap, MainMenu, GameModeMenu, LocalizationManager
└── project.godot               # Config Godot (autoloads, inputs)
```

## Architecture

### Scene Tree

```
Root
├── Singletons (AutoLoads)
│   ├── NetworkManager          # ENet P2P (hosting, connexion, decouverte LAN)
│   ├── GameState               # Flux de jeu (seed, LocalTeamId, IsAIMode)
│   ├── LocalizationManager     # i18n FR/EN/ES
│   └── GameManager             # Economie or + bonus region + victoire
└── Game.tscn
    ├── MapGenerator
    │   ├── Sol (TileMapLayer)
    │   ├── Objets (TileMapLayer)
    │   ├── Camera2D (CameraController)
    │   ├── SelectionManager
    │   ├── NetworkSync          # Hub RPCs (ajoute au runtime en multi)
    │   └── Units               # Conteneur des camps, unites, navires
    ├── GameHUD
    └── Minimap (SubViewport)
```

### Singletons (AutoLoad)

- **GameManager** : economie or par equipe, bonus region, conditions de victoire via VictoryManager. Accessible via `GameManager.Instance`.
- **NetworkManager** : connexion ENet P2P, decouverte UDP (port 7778), code salon 6 caracteres.
- **GameState** : seed de carte, LocalTeamId (1=serveur, 2=client), IsAIMode, AILevel.
- **LocalizationManager** : 166 cles traduites en FR/EN/ES, signal `LanguageChanged`.

### Signaux (Observer Pattern)

Godot utilise des signaux pour la communication entre noeuds. Exemple :

```csharp
[Signal] public delegate void PlayerConnectedEventHandler(long id);

EmitSignal(SignalName.PlayerConnected, id);
```

`NetworkManager` emet `PlayerConnected`, `LobbyUI` s'abonne pour mettre a jour la liste sans dependance directe.

## Generation procedurale de la carte

**Scripts :** `Scripts/Map/MapGenerator.cs`, `TerrainGenerator.cs` (statique), `CampPlacer.cs` (statique)

### Deux couches FastNoiseLite

```
Couche elevation  : seed = baseSeed,       frequence 0.008, FBM 5 octaves
Couche foret      : seed = baseSeed+1000,  frequence 0.05
RNG placement     : seed = baseSeed+2000   (System.Random deterministe)
```

Le serveur genere un seed aleatoire, l'envoie via RPC aux clients. Chaque peer regenere la meme carte localement — pas de transfert des 65 536 tuiles.

### Biomes

| Altitude | Biome | Praticable |
|----------|-------|-----------|
| < -0.2 | Eau | Non (navires seulement) |
| -0.2 a -0.15 | Sable | Oui |
| -0.15 a 0.4 | Herbe / Foret | Oui |
| 0.4 a 0.55 | Roche | Non |
| > 0.55 | Neige | Non |

Carte : 256x256 tuiles de 128px = ~32 000 x 32 000 px.

### Placement des camps

- Probabilite 0.1% par tuile d'herbe (sans foret)
- Distance minimale 3500px entre deux camps
- Chaque camp reçoit un `RegionId` selon son quadrant (1=NW, 2=NE, 3=SW, 4=SE)
- Chaque camp neutre spawne 4 defenseurs initiaux (Infantry, Support, Heal, Range) avec HP x1.5

### Territoire visuel

`TerritoryManager` colore les tuiles autour des camps (rayon 8 tuiles) selon l'equipe proprietaire. Mise a jour en temps reel.

## Systeme d'unites

**Scripts :** `Scripts/Units/`

### Machine a etats (7 etats)

```
Idle
├─ detecte ennemi → MovingToTarget
├─ detecte camp sans defenseurs → AttackingCamp
└─ reprend destination sauvegardee si existe

MovingToTarget  → arrive a portee → Attacking
Attacking       → cible morte ou hors portee → Idle / MovingToTarget
MovingToPoint   → destination atteinte (< 20px) → Idle
Healing         → soigne allie blesse (Heal uniquement)
MovingToTransport → embarque sur navire → QueueFree()
AttackingCamp   → attaque camp quand defenseurs elimines
```

Detectection de blocage : si vitesse reelle < 10% de la vitesse attendue pendant 2 secondes → retour Idle.

### Stats des unites

| Type | Prix | PV | Attaque | Defense | Vitesse | Portee | Production |
|------|------|----|---------|---------|---------|--------|------------|
| Infantry | 50g | 100 | 15 | 10 | 150 | 50 | 2s |
| Support | 75g | 80 | 8 | 5 | 120 | 100 | 3s |
| Range | 80g | 70 | 20 | 5 | 100 | 300 | 3s |
| Heal | 100g | 60 | 0 | 3 | 100 | 150 | 3s |
| AntiArmor | 120g | 80 | 35 | 8 | 90 | 120 | 4s |
| Mortar | 130g | 50 | 40 | 3 | 60 | 400 | 4s |
| Heavy | 150g | 150 | 25 | 20 | 70 | 60 | 5s |
| Tank | 200g | 200 | 30 | 25 | 50 | 100 | 6s |

### Formule de degats

```
degatsReels = attaque * 100 / (100 + defense)
```

Formule scalaire — la defense reduit progressivement (100 defense = 50% reduction).

### Interactions speciales

- **AntiArmor vs Heavy** : degats x2
- **Mortar** : projectile avec splash 200px de rayon, 20 degats plats (ignore la defense)
- **Support** : aura defensive +10 defense pour les allies a moins de 200px (cumulable)
- **Heal** : soigne +12 PV/sec a l'allie le plus endommage dans sa portee, ne combat jamais

## Systeme de navires

**Scripts :** `Scripts/Ships/`

| Type | PV | Attaque | Defense | Vitesse | Portee | Prix | Production | Capacite |
|------|----|---------|---------|---------|--------|------|-----------|---------|
| Transport | 200 | 0 | 10 | 120 | - | 150g | 5s | 10 unites |
| Fregate | 180 | 20 | 15 | 100 | 250 | 200g | 5s | - |
| Destroyer | 250 | 35 | 20 | 80 | 350 | 300g | 7s | - |

Les camps adjacents a l'eau obtiennent automatiquement un **port** (detection smart de la cote, orientation selon la direction vers l'eau). Le port dispose de sa propre file de production (max 5 navires). Le Transport peut embarquer jusqu'a 10 unites terrestres et les debarquer sur une cote.

## Systeme de camps

**Script :** `Scripts/Camps/CampSimple.cs` + partials

- 500 PV, genere 50 or/sec
- **Tourelle defensive** : 10 degats/sec a 600px (active contre les ennemis)
- **File de production** : `Queue<string>` max 7 unites, un seul type produit a la fois

### Capture (deux phases)

1. Eliminer tous les defenseurs du camp
2. Reduire les PV du camp a 0

Recompenses : +50 or instantane, 3 unites bonus spawnees (Infantry, Range, Infantry). Mort mutuelle : si le dernier attaquant meurt au meme moment que le dernier defenseur, le camp redevient neutre.

## Economie

### Sources d'or

| Source | Montant |
|--------|---------|
| Passif joueur | +5 or/sec |
| Par camp possede | +50 or/sec |
| Capture d'un camp | +50 or instantane |
| Or stocke dans camp neutre | Transfere au moment de la capture |
| Bonus region (region entiere controlee) | +30 or/sec |

Or de depart : 100 or.

### Regions economiques

La carte est divisee en 4 quadrants (NW, NE, SW, SE). Si une equipe controle tous les camps non-neutres d'un quadrant, elle reçoit +30 or/sec. Verifie chaque seconde.

### Victoire

Controler 100% des camps non-neutres. Verifie chaque seconde par `VictoryManager`.

## Intelligence Artificielle

**Script :** `Scripts/AI/AIController.cs`

L'IA controle l'equipe 2 en mode solo. Elle achete des unites et les envoie vers les cibles prioritaires (camps neutres favorises, puis camps ennemis).

| Niveau | Tick | Max unites/ordre | Types autorises |
|--------|------|-----------------|-----------------|
| Easy | 5s | 3 | Infantry, Range |
| Medium | 3s | 6 | +Support, AntiArmor, Heavy |
| Hard | 1.5s | Illimite | Tous |

Activee via `GameState.IsAIMode = true`, initialisee par `MapGenerator.InitAIController()`.

## Systeme de selection

**Script :** `Scripts/Selection/SelectionManager.cs`

- **Clic simple** : selectionne l'entite la plus proche (priorite : port > camp > navire > unite)
- **Rectangle** : toutes les unites/navires dans la zone sont selectionnees
- Restriction reseau : un joueur ne peut selectionner que les entites de son equipe
- Feedback visuel : unites selectionnees en jaune, navires en cyan, camp en jaune dore

Un clic droit deplace les unites selectionnees. Clic droit sur un Transport allie → embarquement automatique.

## Camera

**Script :** `Scripts/Camera/CameraController.cs`

- **WASD/Fleches** : pan (vitesse adaptee au zoom)
- **Molette** : zoom smooth via Lerp (0.05x a 2.0x)
- **Clic droit maintenu** : pan a la souris
- **C / Home** : recentrer sur la carte
- Limites clampees aux bords de la carte

## Minimap

**Script :** `Scripts/UI/Minimap.cs`

`SubViewport` partageant le meme `World2D` que le viewport principal. Rectangle rouge indiquant la zone visible. Clic/drag sur la minimap teleporte la camera.

## Multijoueur

**Scripts :** `Scripts/Network/`

### Architecture P2P

- Serveur = Team 1, Client = Team 2
- Connexion via code salon 6 caracteres (decouverte UDP broadcast sur port 7778)
- `NetworkEntityRegistry` : dictionnaire statique `NetworkId → Node`
  - IDs dynamiques : `"{peerId}_{counter}"`
  - IDs deterministesd'efenseurs initiaux : `"camp_{campId}_unit_{index}"`

### RPCs

| RPC | Mode | Fiabilite | Usage |
|-----|------|-----------|-------|
| RpcReceiveSeedAndStart | Authority | Reliable | Serveur → Clients : seed + debut |
| RpcSyncCampAssignments | Authority | Reliable | Attribution camps/equipes |
| RpcSpawnUnit / RpcSpawnShip | AnyPeer | Reliable | Creation entite distante |
| RpcEntityDied | AnyPeer | Reliable | Destruction puppet |
| RpcApplyUnitDamage / Camp / Ship | AnyPeer | Reliable | Degats (appliques uniquement par le peer proprietaire) |
| RpcCampCaptured | AnyPeer | Reliable | Synchronisation capture |
| RpcUnitBoarded / RpcTransportUnloaded | AnyPeer | Reliable | Transport naval |
| RpcSyncEntityStates | AnyPeer | Unreliable | 20Hz : positions/sante/etats |

### Determinisme

- Meme seed → meme terrain, memes positions de camps, meme distribution initiale
- L'economie de chaque equipe est calculee localement (pas de sync or)
- Limitation Godot : `bool[]` non supportee en RPC Variant → convertie en `int[]`

## Localisation

**Script :** `Scripts/UI/LocalizationManager.cs`

3 langues (FR/EN/ES), 166 cles. Changement de langue via bouton dans chaque menu (cycle FR → EN → ES). Signal `LanguageChanged` declenche la mise a jour des elements UI sans recharger la scene.

## Controles

| Action | Controle |
|--------|----------|
| Deplacer la camera | ZQSD / Fleches |
| Zoom | Molette souris |
| Drag camera | Clic droit maintenu |
| Selectionner | Clic gauche |
| Selection multiple | Clic gauche + glisser |
| Deplacer les unites | Clic droit |
| Recentrer camera | C / Home |

## Conventions de code

- Classes et methodes : PascalCase
- Variables privees : _camelCase
- Variables locales : camelCase
- Commentaires : francais
- Commits : `type: description` (feat, fix, refactor, docs)

## Git

- `main` : version stable
- `develop` : integration
- `feature/*` : nouvelles fonctionnalites

Ne jamais push directement sur main. Les features partent de develop et y sont mergees.

## Auteurs

- **Darkft28** - [GitHub](https://github.com/Darkft28)
- **Louis27940** - [GitHub](https://github.com/Louis27940)
