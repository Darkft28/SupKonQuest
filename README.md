# SupKonQuest

Jeu de strategie et de conquete en temps reel developpe avec Godot 4.5 et C# (.NET 8.0).

## Description

SupKonQuest est un RTS (Real-Time Strategy) ou le joueur doit capturer des camps sur une carte predéfinie. On commence avec un camp et un peu d'or, on produit des unites terrestres et navales, et on part a la conquete des camps adverses. Le jeu propose un mode solo contre IA (3 niveaux, 1 camp par faction) et un mode multijoueur en ligne PvP (2 a 8 joueurs, 1 camp de depart par joueur, camps restants neutres).

Le principe : chaque camp genere de l'or passivement, cet or permet d'acheter des unites, et ces unites servent a capturer d'autres camps. Controler une region entiere rapporte un bonus. Le joueur qui controle **100 % des camps non neutres** gagne.

## Documentation


| Document                | Public       | Fichier dans le depot  |
| ----------------------- | ------------ | ---------------------- |
| Manuel utilisateur      | Joueurs      | `docs/user_manual.pdf` |
| Documentation technique | Developpeurs | `docs/tech_doc.pdf`    |


## Technologies

**Godot 4.5** : moteur open-source, leger et bien adapte au 2D. Son systeme de scenes et de noeuds permet de structurer le projet de maniere modulaire. Chaque element (unite, camp, carte) est une scene reutilisable.

**C# (.NET 8.0)** : typage statique, structures de donnees .NET (`Dictionary`, `Queue`, `List`). Aucune dependance NuGet externe.

**Nakama** : backend multijoueur (compte email ou invité, session chiffree locale, matchmaking, relay de commandes gameplay via WebSocket).

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

Le mode solo fonctionne sans Nakama. Pour tester le multijoueur (auth email / invite + matchmaking 2-8 + lobby in-match), lancez le stack **CockroachDB + Nakama** avec le module relay TypeScript du depot (`supkonquest-server/`).

**Prerequis :** Docker Desktop, Node.js + npm.

**1. Compiler le module relay**

```powershell
cd supkonquest-server
npm install
npm run build
```

**2. Lancer les conteneurs (depuis `supkonquest-server/`)**

Creez un `docker-compose.local.yml` (voir `docs/tech_doc.pdf` § 9 pour le contenu complet) puis :

```powershell
docker compose -f docker-compose.local.yml up --build
```

Le `Dockerfile` embarque `build/index.js`. Recompilez (`npm run build`) puis relancez `docker compose up --build` apres chaque modification du module TS.

**Configuration client** (`project.godot`, section `[nakama]`) :


| Parametre    | Local        | Production (Azure)               |
| ------------ | ------------ | -------------------------------- |
| `host`       | `127.0.0.1`  | IP ou domaine du serveur Azure   |
| `port`       | `7350`       | `7350`                           |
| `server_key` | `defaultkey` | `defaultkey` (ou cle configuree) |


**Test multi-instance (2 clients Godot) :**

```powershell
& "C:\Chemin\Vers\Godot_v4.5.1-stable_mono_win64.exe" --path "C:\Chemin\Vers\SupKonQuest" --nakama-slot=1
& "C:\Chemin\Vers\Godot_v4.5.1-stable_mono_win64.exe" --path "C:\Chemin\Vers\SupKonQuest" --nakama-slot=2
```

Chaque slot utilise des fichiers de session distincts (`user://nakama_device_id_1.txt`, etc.).

**Arret :**

```powershell
docker compose -f docker-compose.local.yml down
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
│   ├── Auth.tscn               # Connexion / inscription / invité (Nakama)
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
│   ├── Network/                # GameState, NetworkSync, NetworkEntityRegistry, NakamaService, AuthSessionStore
│   ├── AI/                     # AIController (Easy/Medium/Hard)
│   └── UI/                     # GameHUD, LobbyUI, AuthUI, Minimap, MainMenu, GameModeMenu, LocalizationManager, KeybindingsManager
├── supkonquest-server/         # Module Nakama relay (TypeScript) + Docker
│   ├── main.ts, lobby.ts, match_handler.ts, messages.ts
│   ├── docker-compose.yml      # Deploiement Azure ACI
│   └── NAKAMA_SERVER_TS.md     # Contrat opcodes (resume en docs/tech_doc.pdf § 5)
├── docs/
│   ├── user_manual.pdf         # Manuel joueur (PDF versionne)
│   └── tech_doc.pdf            # Doc technique (PDF versionne)
└── project.godot               # Config Godot (autoloads, inputs)
```

## Architecture

### Scene Tree

```
Root
├── Singletons (AutoLoads)
│   ├── GameManager             # Economie or + bonus region + victoire
│   ├── GameState               # Flux de jeu (seed, identifiant joueur local, IsAIMode, IsOnline)
│   ├── NakamaService           # Auth + matchmaking + relay (mode en ligne)
│   ├── LocalizationManager     # i18n FR/EN/ES
│   └── AudioSettings           # Volume audio
└── Game.tscn
    ├── MapGenerator
    │   ├── Sol (TileMapLayer)
    │   ├── Objets (TileMapLayer)
    │   ├── Camera2D (CameraController)
    │   ├── SelectionManager
    │   ├── NetworkSync          # Helpers sync online (Nakama relay)
    │   └── Units               # Conteneur des camps, unites, navires
    ├── GameHUD
    └── Minimap (SubViewport)
```

### Singletons (AutoLoad)

- **GameManager** : economie or par equipe, bonus region, tiers de production. Accessible via `GameManager.Instance`.
- **GameState** : seed de carte, equipe locale, mode solo/en ligne, carte selectionnee, mode rapide.
- **NakamaService** : mode en ligne (email, invite, restauration de session, matchmaking, relay).
- **KeybindingsManager** : macros de selection (unites 1-8, navires 9/0/-, ultimes reconfigurables).
- **LocalizationManager** : traductions FR/EN/ES, signal `LanguageChanged`.
- **AudioSettings** : preferences de volume.

### Signaux (Observer Pattern)

Godot utilise des signaux pour la communication entre noeuds. Exemple :

```csharp
[Signal] public delegate void PlayerConnectedEventHandler(long id);

EmitSignal(SignalName.PlayerConnected, id);
```

`NakamaService` emet les signaux de lobby/match ; `LobbyUI` s'abonne pour mettre a jour la liste des joueurs.

## Generation de la carte

**Scripts :** `Scripts/Map/MapGenerator.cs`, `TerrainGenerator.cs` (statique), `CampPlacer.cs` (statique)

### Maps prédéfinies (presets)

La carte est chargee depuis un preset encode en RLE. **Trois cartes** disponibles : **Irridium** (3 regions), **Alabasta** (4 regions), **Torskey** (3 regions).

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
- Chaque camp recoit un `RegionId` selon la grille territoire du preset
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
| Transport | 3    | 200 | 0       | 10      | 120     | -      | 150g | 5s         | 10 unites |
| Fregate   | 3    | 180 | 20      | 15      | 100     | 300    | 200g | 5s         | -         |
| Destroyer | 3    | 250 | 35      | 20      | 80      | 350    | 300g | 7s         | -         |


Tous les navires requierent le **tier 3** (region d'origine entierement controlee) et un **port** actif. Maximum **5 navires actifs** par equipe.

Un **port** s'achete manuellement depuis le HUD (bouton **Port — 500g**) sur un camp possede sans port, puis le joueur clique sur une tuile cotiere pour le poser. L'orientation est auto-detectee selon la direction de l'eau adjacente. Le port dispose de sa propre file de production (max 5 navires). Le Transport peut embarquer jusqu'a 10 unites terrestres et les debarquer sur une cote. Le placement peut etre annule (or rembourse).

## Systeme de camps

**Script :** `Scripts/Camps/CampSimple.cs` + partials

- 600 PV, genere 50 or/sec (en plus du passif global de 75 or/sec)
- **Tourelle defensive** : 5 degats/sec a 600px (active contre les ennemis)
- **File de production** : `Queue<string>` max 7 unites, un seul type produit a la fois

### Capture (deux phases)

1. Eliminer tous les defenseurs du camp
2. Reduire les PV du camp a 0

Recompenses : +50 or instantane, 3 unites bonus spawnees (Infantry, Range, Infantry).

## Economie

### Sources d'or


| Source                                  | Montant                           |
| --------------------------------------- | --------------------------------- |
| Passif (par equipe, joueur et IA)       | +75 or/sec                        |
| Par camp possede                        | +50 or/sec                        |
| Capture d'un camp                       | +50 or instantane                 |
| Or stocke dans camp neutre              | Transfere au moment de la capture |
| Bonus region (region entiere controlee) | +30 or/sec                        |


Or de depart : 100 or.

### Regions economiques

La carte est divisee en regions (3 sur Irridium, 4 sur Alabasta, 3 sur Torskey). Si une equipe controle tous les camps d'une region, elle recoit +30 or/sec. Verifie chaque seconde. Les regions servent aussi a debloquer le Tier 3 (controler sa region d'origine).

### Systeme de tiers

Chaque equipe progresse sur 3 paliers de production :


| Palier | Condition de deblocage                          | Unites / navires disponibles                         |
| ------ | ----------------------------------------------- | ---------------------------------------------------- |
| Tier 1 | Depart                                          | Infantry, Support, Range                             |
| Tier 2 | Achat 1500 or                                   | + Heal, AntiArmor                                    |
| Tier 3 | Controler tous les camps de sa region d'origine | + Mortar, Heavy, Tank, Transport, Fregate, Destroyer |


Le bouton de deblocage tier 2 est visible dans le HUD quand un camp est selectionne.

### Victoire

Controler **100 % des camps non neutres** (`VictoryManager`, verification chaque seconde).

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
- **C / Home** : recentrer sur le centre de la carte
- Limites clampees aux bords de la carte

## Minimap

**Script :** `Scripts/UI/Minimap.cs`

`SubViewport` partageant le meme `World2D` que le viewport principal. Rectangle rouge indiquant la zone visible. Clic/drag sur la minimap teleporte la camera.

## Multijoueur

**Scripts :** `Scripts/Network/`

### Mode en ligne (Nakama + relay)

**Parcours multijoueur** : Menu principal → Mode de jeu → **Authentification** (`Auth.tscn`) → Lobby → partie.

- **Compte email** : inscription (email, pseudo, mot de passe ≥ 8) ou connexion ; pseudo = `username` Nakama (fixe en v1) ; session restauree au prochain lancement.
- **Invité** : bouton dédié ; device ID persistant ; pseudo modifiable dans le lobby.
- **Déconnexion** : bouton dans le lobby ; efface la session chiffree et renvoie vers Auth.
- **PvP uniquement** : pas d'IA en multijoueur (`IsAIMode = false` force a l'entree du lobby/match).
- **Matchmaking** : 2 a 8 joueurs (`AddMatchmakerAsync` min 2 / max 8).
- **Camps** : 1 camp de depart par joueur humain ; les autres camps preset restent **neutres** (defenseurs 1,5x HP).
- **Equipes** : `LocalTeamId` = index dans la liste triee des `userId` Nakama + 1 ; `ActivePlayerCount` fige au demarrage.
- **Lobby in-match** : apres `JoinMatch` (>= 2 joueurs), le client affiche la liste des joueurs et attend le **serveur relay** — pas de demarrage automatique cote client.
- **Test multi-instance** : voir section [Serveur Nakama local](#serveur-nakama-local-pour-le-mode-en-ligne) (`--nakama-slot=1`, `--nakama-slot=2`).

### Contrat relay (`supkonquest-server/`)

Le module `**supkonquest_relay`** est dans ce depot (`supkonquest-server/`). Detail complet : `docs/tech_doc.pdf` § 5 et `supkonquest-server/NAKAMA_SERVER_TS.md`.

**Opcodes lobby (serveur → clients) :**


| Opcode | Nom        | Payload JSON (camelCase)                                                                                                 |
| ------ | ---------- | ------------------------------------------------------------------------------------------------------------------------ |
| `4001` | LobbyTick  | `{ secondsRemaining, playerCount }`                                                                                      |
| `4002` | MatchStart | `{ seed, orderedUserIds, matchId?, mapType }` — `mapType` : 0=Irridium, 1=Alabasta, 2=Torskey ; liste triee par `userId` |


Regles lobby : countdown **20 s** au 2e joueur, **+5 s** par joueur supplementaire, demarrage immediat a **8 joueurs**.

### Gameplay relay (opcodes 1001–7003)

- `NetworkCommandRouter` : achats, spawns, deplacements, transport, degats, captures, combat, ultimes (`6001`/`6002`), snapshots or (`3001`).
- `NetworkEntityRegistry` : dictionnaire statique `NetworkId → Node`
  - IDs dynamiques : `"{peerId}_{counter}"`
  - IDs deterministes des defenseurs initiaux : `"camp_{campId}_unit_{index}"`
- Camps neutres en relay : simulation locale sur **tous** les peers (`CampSimple.IsLocallyOwned`).
- Enveloppe obligatoire : `senderUserId` + `sequence` strictement croissante par joueur.

### Deconnexion en partie

- Mapping `userId → teamId` fige au `MatchStart`.
- Sur leave apres demarrage : serveur broadcast **une fois** `5002 PlayerLeaveCleanup` ; le client neutralise l'equipe (unites/navires supprimes, camps neutralises).

### Determinisme

- Meme seed → meme terrain, memes positions de camps, meme distribution initiale
- L'economie de chaque joueur est calculee localement (snapshots or via opcode relay)

## Localisation

**Script :** `Scripts/UI/LocalizationManager.cs`

3 langues (FR/EN/ES), 166 cles. Changement de langue via bouton dans chaque menu (cycle FR → EN → ES). Signal `LanguageChanged` declenche la mise a jour des elements UI sans recharger la scene.

## Controles


| Action                    | Controle                   |
| ------------------------- | -------------------------- |
| Deplacer la camera        | ZQSD / Fleches             |
| Zoom                      | Molette souris             |
| Drag camera               | Clic droit maintenu        |
| Selectionner              | Clic gauche                |
| Selection multiple        | Clic gauche + glisser      |
| Deplacer les unites       | Clic droit                 |
| Recentrer camera          | C / Home                   |
| Macro selection (unites)  | `1` a `8` (reconfigurable) |
| Macro selection (navires) | `9`, `0`, `-`              |
| Toutes les unites         | `A`                        |
| Ultime soin (equipe)      | `1` puis clic gauche       |
| Ultime support (equipe)   | `2` puis clic gauche       |
| Annuler le ciblage        | `Esc` / clic droit         |


Les ultimes sont des capacites **d'equipe** (recharge 20 s / 25 s), independantes des unites Heal/Support sur le terrain. Raccourcis modifiables dans le menu principal via `KeybindingsManager`.

## Conventions de code

- Classes et methodes : PascalCase
- Variables privees : _camelCase
- Variables locales : camelCase
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

