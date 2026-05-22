# SupKonQuest

Jeu de strategie et de conquete en temps reel developpe avec Godot 4.5 et C# (.NET 8.0).

## Description

SupKonQuest est un RTS (Real-Time Strategy) ou le joueur doit capturer des camps sur une carte predéfinie. On commence avec un camp et un peu d'or, on produit des unites terrestres et navales, et on part a la conquete des camps adverses. Le jeu propose un mode solo contre IA (3 niveaux, 1 camp par faction) et un mode multijoueur en ligne PvP (2 a 8 joueurs, 1 camp de depart par joueur, camps restants neutres).

Le principe : chaque camp genere de l'or passivement, cet or permet d'acheter des unites, et ces unites servent a capturer d'autres camps. Controler une region entiere rapporte un bonus. Le joueur qui controle tous les camps gagne.

## Technologies

**Godot 4.5** : moteur open-source, leger et bien adapte au 2D. Son systeme de scenes et de noeuds permet de structurer le projet de maniere modulaire. Chaque element (unite, camp, carte) est une scene reutilisable.

**C# (.NET 8.0)** : typage statique, structures de donnees .NET (`Dictionary`, `Queue`, `List`). Aucune dependance NuGet externe.

**ENet** : protocole reseau UDP fiable integre dans Godot pour l'ancien mode local/legacy (retransmission des paquets perdus, ordonnancement). Port 7777 pour le jeu, 7778 pour la decouverte LAN via UDP broadcast.

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

## Serveur Nakama local (pour le mode en ligne)

Si vous n'avez pas encore de serveur, le mode solo fonctionne sans Nakama.
Pour tester le mode en ligne (auth guest + matchmaking 2-8 + lobby in-match), lancez un Nakama local **et** le module relay (projet serveur separe) qui pilote le countdown et le demarrage de partie.

Prerequis minimaux:

- Docker Desktop

Commandes (PowerShell):

```powershell
docker network create nakama-net
docker run --name nakama-postgres --network nakama-net -e POSTGRES_PASSWORD=localdb -e POSTGRES_USER=local -e POSTGRES_DB=nakama -p 5432:5432 -d postgres:15-alpine
docker run --name nakama --network nakama-net -p 7350:7350 -p 7349:7349 -d heroiclabs/nakama:3.22.0 --database.address root@nakama-postgres:5432
```

Le projet utilise par defaut ces valeurs dans `project.godot`:

- `nakama/scheme = "http"`
- `nakama/host = "127.0.0.1"`
- `nakama/port = 7350`
- `nakama/server_key = "defaultkey"`

Arret/nettoyage rapide:

```powershell
docker stop nakama nakama-postgres
docker rm nakama nakama-postgres
docker network rm nakama-net
```

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
│   ├── Lobby.tscn              # Lobby multijoueur (Nakama matchmaking)
│   ├── Game.tscn               # Scene principale du jeu
│   ├── GameHUD.tscn            # Interface HUD (or, boutons d'achat)
│   ├── Unit.tscn               # Prefab unite terrestre
│   ├── Ship.tscn               # Prefab navire
│   └── camp_simple.tscn        # Prefab camp
├── Scripts/
│   ├── Units/                  # Unit.cs + partials (Combat, Movement, Healing, Transport, Visuals)
│   ├── Ships/                  # Ship.cs + partials (Combat, Movement, Transport, Visuals)
│   ├── Camps/                  # CampSimple.cs + partials (Production, Naval, Defense, Visuals)
│   ├── Map/                    # MapGenerator, TerrainGenerator, CampPlacer, TerritoryManager, TerritoryConnectivity
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
│   ├── GameManager             # Economie or + bonus region + victoire
│   ├── NetworkManager          # ENet legacy P2P (hosting, connexion, decouverte LAN)
│   ├── GameState               # Flux de jeu (seed, identifiant joueur local, IsAIMode)
│   ├── NakamaService           # Auth + matchmaking + relay (mode en ligne)
│   ├── LocalizationManager     # i18n FR/EN/ES
│   └── AudioSettings           # Volume audio
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

- **GameManager** : economie or par joueur/slot, bonus region, conditions de victoire via VictoryManager. Accessible via `GameManager.Instance`.
- **NetworkManager** : connexion ENet P2P, decouverte UDP (port 7778), code salon 6 caracteres.
- **GameState** : seed de carte, identifiant local de joueur (slot d'ownership), IsAIMode, AILevel, IsOnline.
- **NakamaService** : mode en ligne (guest, matchmaking, envoi de commandes relay).
- **LocalizationManager** : 166 cles traduites en FR/EN/ES, signal `LanguageChanged`.
- **AudioSettings** : preferences de volume.

### Signaux (Observer Pattern)

Godot utilise des signaux pour la communication entre noeuds. Exemple :

```csharp
[Signal] public delegate void PlayerConnectedEventHandler(long id);

EmitSignal(SignalName.PlayerConnected, id);
```

`NetworkManager` emet `PlayerConnected`, `LobbyUI` s'abonne pour mettre a jour la liste sans dependance directe.

## Generation de la carte

**Scripts :** `Scripts/Map/MapGenerator.cs`, `TerrainGenerator.cs` (statique), `CampPlacer.cs` (statique)

### Maps prédéfinies (presets)

La carte est chargee depuis un preset encode en RLE. Deux maps disponibles : **Irridium** et **Alabasta**.

```
RNG placement camps : seed = baseSeed+2000  (System.Random deterministe)
```

Le serveur genere un seed aleatoire, l'envoie via RPC aux clients. Les positions des camps sont predefinies par le preset puis melangees de facon deterministe (Fisher-Yates). Pas de generation procedurale des camps.

### Tuiles


| ID  | Biome | Praticable              |
| --- | ----- | ----------------------- |
| 6   | Eau   | Non (navires seulement) |
| 1   | Sable | Oui                     |
| 0   | Herbe | Oui                     |
| 3   | Foret | Oui                     |
| 5   | Roche | Non                     |
| 4   | Neige | Non                     |


Carte : 256x256 tuiles de 128px = ~32 000 x 32 000 px.

### Placement des camps

- Positions predefinies par la map preset (pas de probabilite ni distance minimale dans le code)
- Chaque camp reçoit un `RegionId` selon la grille territoire du preset (3 regions sur Irridium, 4 sur Alabasta)
- Chaque camp neutre spawne 4 defenseurs initiaux (Infantry, Support, Heal, Range) avec HP x1.5

### Territoire visuel

`TerritoryManager` colore les tuiles autour des camps (rayon 8 tuiles) selon l'equipe proprietaire. Mise a jour en temps reel. Pas d'achat manuel de tuiles : l'extension se fait uniquement par capture de camps.

`TerritoryConnectivity` construit un graphe de regions terrestres voisines (preset). Les unites et l'IA priorisent les camps atteignables a pied ; les cibles isolees par l'eau passent par le naval.

Placement de port : clic sur une tuile **terrestre de votre territoire** adjacente a l'eau (pas seulement pres du camp).

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


| Type      | Tier | Prix | PV  | Attaque | Defense | Vitesse | Portee | Production |
| --------- | ---- | ---- | --- | ------- | ------- | ------- | ------ | ---------- |
| Infantry  | 1    | 50g  | 100 | 15      | 10      | 150     | 100    | 2s         |
| Support   | 1    | 75g  | 80  | 8       | 5       | 120     | 100    | 3s         |
| Range     | 1    | 80g  | 70  | 20      | 5       | 100     | 300    | 3s         |
| Heal      | 2    | 100g | 60  | 0       | 3       | 100     | 150    | 3s         |
| AntiArmor | 2    | 120g | 80  | 35      | 8       | 90      | 120    | 4s         |
| Mortar    | 3    | 130g | 50  | 40      | 3       | 60      | 400    | 4s         |
| Heavy     | 3    | 150g | 150 | 25      | 20      | 70      | 100    | 5s         |
| Tank      | 3    | 200g | 200 | 30      | 25      | 50      | 100    | 6s         |


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


| Type      | Tier | PV  | Attaque | Defense | Vitesse | Portee | Prix | Production | Capacite  |
| --------- | ---- | --- | ------- | ------- | ------- | ------ | ---- | ---------- | --------- |
| Transport | 1    | 200 | 0       | 10      | 120     | -      | 150g | 5s         | 10 unites |
| Fregate   | 3    | 180 | 20      | 15      | 100     | 300    | 200g | 5s         | -         |
| Destroyer | 3    | 250 | 35      | 20      | 80      | 350    | 300g | 7s         | -         |


Un **port** s'achete manuellement depuis le HUD (bouton **⚓ Port — 500g**) puis le joueur clique sur une tuile cotiere pour le poser. L'orientation est auto-detectee selon la direction de l'eau adjacente. Le port dispose de sa propre file de production (max 5 navires). Le Transport peut embarquer jusqu'a 10 unites terrestres et les debarquer sur une cote. Le placement peut etre annule (or rembourse).

## Systeme de camps

**Script :** `Scripts/Camps/CampSimple.cs` + partials

- 600 PV, genere 50 or/sec (en plus du passif global de 500 or/sec)
- **Tourelle defensive** : 5 degats/sec a 600px (active contre les ennemis)
- **File de production** : `Queue<string>` max 7 unites, un seul type produit a la fois

### Capture (deux phases)

1. Eliminer tous les defenseurs du camp
2. Reduire les PV du camp a 0

Recompenses : +50 or instantane, 3 unites bonus spawnees (Infantry, Range, Infantry). Mort mutuelle : si le dernier attaquant meurt au meme moment que le dernier defenseur, le camp redevient neutre.

## Economie

### Sources d'or


| Source                                  | Montant                           |
| --------------------------------------- | --------------------------------- |
| Passif joueur                           | +500 or/sec                       |
| Par camp possede                        | +50 or/sec                        |
| Capture d'un camp                       | +50 or instantane                 |
| Or stocke dans camp neutre              | Transfere au moment de la capture |
| Bonus region (region entiere controlee) | +30 or/sec                        |


Or de depart : 100 or.

### Regions economiques

La carte est divisee en regions (3 sur Irridium, 4 sur Alabasta). Si une equipe controle tous les camps d'une region, elle reçoit +30 or/sec. Verifie chaque seconde. Les regions servent aussi a debloquer le Tier 3 (controler sa region d'origine).

### Systeme de tiers

Chaque equipe progresse sur 3 paliers de production :


| Palier | Condition de deblocage                          | Unites disponibles                        |
| ------ | ----------------------------------------------- | ----------------------------------------- |
| Tier 1 | Depart                                          | Infantry, Support, Range, Transport       |
| Tier 2 | Achat 1500 or                                   | + Heal, AntiArmor                         |
| Tier 3 | Controler tous les camps de sa region d'origine | + Mortar, Heavy, Tank, Fregate, Destroyer |


Le bouton de deblocage tier 2 est visible dans le HUD quand un camp est selectionne.

### Victoire

Controler 100% des camps non-neutres. Verifie chaque seconde par `VictoryManager`.

## Intelligence Artificielle

**Script :** `Scripts/AI/AIController.cs`

L'IA controle les equipes bot (mode solo ou FFA). Architecture **Utility AI** : chaque tick, l'IA score ses options (production, attaque, defense) et choisit la meilleure. Une instance `AIController` est creee par equipe bot dans `MapGenerator.InitAIController()`.


| Parametre              | Easy | Medium | Hard |
| ---------------------- | ---- | ------ | ---- |
| Tick de decision       | 6s   | 3.5s   | 2s   |
| Max unites             | 8    | 16     | 28   |
| Delai premiere attaque | 20s  | 12s    | 5s   |
| Delai de reaction      | 5s   | 1.5s   | 0.3s |
| Taux d'erreur cible    | 40%  | 15%    | 0%   |
| Ratio defense          | 0%   | 15%    | 20%  |


**Comportement par niveau :**

- **Easy** : spam Infantry, attaque le camp terrestre le plus proche, pas de defense reactive ni naval
- **Medium** : composition equilibree (Infantry 45%, Range 35%, Support 20%), economise pour tier 2, defense reactive, ralliement avant attaque ; port + Fregate si region d'origine entiere controlee ; offensive navale si region d'origine complete
- **Hard** : composition adaptative (contre AntiArmor si ennemi a >3 Heavy), vise tier 3 ; port des qu'une region entiere est controlee ; production navale (Transport occasionnel, Fregate/Destroyer) ; vagues navales possibles avant home region complete (20% par tick, cooldown 30s)

**Ciblage terrestre :** l'IA ignore les camps dont la `RegionId` n'est pas dans le composant connexe terrestre de ses regions possedees (`TerritoryConnectivity`).

**Tiers IA :**

- Tier 1 (depart) : Infantry, Support, Range
- Tier 2 (achat 1500 or) : + Heal, AntiArmor
- Tier 3 (controle region d'origine) : + Mortar, Heavy, Tank, Fregate, Destroyer, Transport

Activee via `GameState.IsAIMode = true`, niveau via `GameState.AILevel`.

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

### Mode en ligne (Nakama + relay)

- **PvP uniquement** : pas d'IA en multijoueur (`IsAIMode = false` force a l'entree du lobby/match).
- **Matchmaking** : 2 a 8 joueurs (`AddMatchmakerAsync` min 2 / max 8).
- **Camps** : 1 camp de depart par joueur humain ; les autres camps preset restent **neutres** (defenseurs 1,5x HP).
- **Equipes** : `LocalTeamId` = index dans la liste triee des `userId` Nakama + 1 ; `ActivePlayerCount` fige au demarrage.
- **Lobby in-match** : apres `JoinMatch` (>= 2 joueurs), le client affiche la liste des joueurs et attend le **serveur relay** — pas de demarrage automatique cote client.
- **Test local multi-instance** : lancer chaque client avec un slot device distinct, ex. `--nakama-slot=1` et `--nakama-slot=2`, pour eviter le meme `userId` Nakama.

### Contrat relay (module serveur externe)

Le module relay Nakama vit dans un **autre depot**. Il doit broadcaster :

| Opcode | Nom | Payload JSON (camelCase) |
| ------ | --- | ------------------------ |
| `4001` | LobbyTick | `{ "secondsRemaining": int, "playerCount": int }` — environ chaque seconde pendant l'attente |
| `4002` | MatchStart | `{ "seed": int, "orderedUserIds": ["userId1", ...] }` — liste triee par `userId` (meme regle que le client) |

Regles serveur attendues :

- Countdown **~20 s** des l'arrivee du 2e joueur.
- **+5 s** au temps restant a chaque nouveau joueur (jusqu'a 8).
- Demarrage immediat si **8 joueurs** dans le match.
- Seul `MatchStart` declenche le chargement de `Game.tscn` sur tous les clients.

Constantes client : `NetworkCommandRouter.OpcodeLobbyTick` / `OpcodeMatchStart`.

### Architecture legacy ENet (non utilise par l'UI actuelle)

- Code conserve dans `NetworkManager` (port 7777, decouverte LAN 7778, max 8 peers).
- L'UI lobby actuelle passe par `NakamaService` + `LobbyUI` uniquement.

### Gameplay relay (opcodes 1001-3001)

- `NetworkCommandRouter` : achats, deplacements, attaques, captures, or (snapshots).
- `NetworkEntityRegistry` : dictionnaire statique `NetworkId → Node`
  - IDs dynamiques : `"{peerId}_{counter}"`
  - IDs deterministes des defenseurs initiaux : `"camp_{campId}_unit_{index}"`
- Camps neutres en relay : simulation locale sur **tous** les peers (`CampSimple.IsLocallyOwned`).

### RPCs Godot (ENet legacy)


| RPC                                   | Mode      | Fiabilite  | Usage                                                  |
| ------------------------------------- | --------- | ---------- | ------------------------------------------------------ |
| RpcReceiveSeedAndStart                | Authority | Reliable   | Serveur → Clients : seed + debut                       |
| RpcSyncCampAssignments                | Authority | Reliable   | Attribution camps/joueurs                              |
| RpcSpawnUnit / RpcSpawnShip           | AnyPeer   | Reliable   | Creation entite distante                               |
| RpcEntityDied                         | AnyPeer   | Reliable   | Destruction puppet                                     |
| RpcApplyUnitDamage / Camp / Ship      | AnyPeer   | Reliable   | Degats (appliques uniquement par le peer proprietaire) |
| RpcCampCaptured                       | AnyPeer   | Reliable   | Synchronisation capture                                |
| RpcUnitBoarded / RpcTransportUnloaded | AnyPeer   | Reliable   | Transport naval                                        |
| RpcSyncEntityStates                   | AnyPeer   | Unreliable | 20Hz : positions/sante/etats                           |


### Determinisme

- Meme seed → meme terrain, memes positions de camps, meme distribution initiale
- L'economie de chaque joueur est calculee localement (pas de sync or)
- Limitation Godot : `bool[]` non supportee en RPC Variant → convertie en `int[]`

## Localisation

**Script :** `Scripts/UI/LocalizationManager.cs`

3 langues (FR/EN/ES), 166 cles. Changement de langue via bouton dans chaque menu (cycle FR → EN → ES). Signal `LanguageChanged` declenche la mise a jour des elements UI sans recharger la scene.

## Controles


| Action              | Controle              |
| ------------------- | --------------------- |
| Deplacer la camera  | ZQSD / Fleches        |
| Zoom                | Molette souris        |
| Drag camera         | Clic droit maintenu   |
| Selectionner        | Clic gauche           |
| Selection multiple  | Clic gauche + glisser |
| Deplacer les unites | Clic droit            |
| Recentrer camera    | C / Home              |


## Conventions de code

- Classes et methodes : PascalCase
- Variables privees : _camelCase
- Variables locales : camelCase
- Commentaires : francais
- Commits : `type: description` (feat, fix, refactor, docs)

## Git

### Branches


| Branche     | Role                      | Protection                                     |
| ----------- | ------------------------- | ---------------------------------------------- |
| `main`      | Version stable (releases) | PR obligatoire + 1 approbation + no force push |
| `develop`   | Integration (code teste)  | PR obligatoire + 1 approbation + no force push |
| `feature/`* | Developpement quotidien   | Aucune restriction                             |


### Workflow

```
feature/ma-fonctionnalite
        │
        │  git push origin feature/ma-fonctionnalite
        │
        ▼
   Pull Request → develop
        │
        │  Review + approbation requise
        │
        ▼
     develop  (integration, tests)
        │
        │  Pull Request de release
        │
        ▼
       main   (version stable)
```

### Etapes pour contribuer

```bash
# 1. Partir d'un develop a jour
git checkout develop
git pull origin develop

# 2. Creer une branche de feature
git checkout -b feature/nom-de-la-feature

# 3. Developper et commiter
git add Scripts/MonFichier.cs
git commit -m "feat: description de la feature"

# 4. Pusher la branche
git push origin feature/nom-de-la-feature

# 5. Ouvrir une Pull Request vers develop sur GitHub
# → attendre review et approbation avant de merger
```

### Convention de commits


| Prefixe     | Usage                                |
| ----------- | ------------------------------------ |
| `feat:`     | Nouvelle fonctionnalite              |
| `fix:`      | Correction de bug                    |
| `refactor:` | Reorganisation du code               |
| `docs:`     | Documentation                        |
| `style:`    | Formatage, pas de changement logique |
| `test:`     | Ajout ou modification de tests       |


## Auteurs

- **Darkft28** - [GitHub](https://github.com/Darkft28)
- **Louis27940** - [GitHub](https://github.com/Louis27940)

