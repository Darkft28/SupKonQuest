# SupKonQuest

Jeu de strategie et de conquete en temps reel developpe avec Godot 4.5 et C# (.NET 8.0).

## Description

SupKonQuest est un RTS (Real-Time Strategy) ou le joueur doit capturer des camps sur une carte generee proceduralement. On commence avec un camp et un peu d'or, on produit des unites, et on part a la conquete des camps adverses. Le jeu propose un mode solo et un mode multijoueur en reseau local.

Le principe est simple : chaque camp genere de l'or passivement, cet or permet d'acheter des unites, et ces unites servent a capturer d'autres camps. Le joueur qui controle tous les camps gagne la partie.

## Technologies utilisees

**Godot 4.5** : on a choisi Godot comme moteur de jeu car il est open-source, leger et bien adapte au 2D. Son systeme de scenes et de noeuds permet de structurer le projet de maniere modulaire. Chaque element du jeu (unite, camp, carte) est une scene reutilisable.

**C# (.NET 8.0)** : Godot supporte deux langages, GDScript et C#. On a opte pour C# car le typage statique aide a eviter des bugs, et ca permet d'utiliser les structures de donnees .NET comme `Dictionary`, `Queue` ou `List` directement.

**ENet** : pour le multijoueur, on utilise ENet qui est integre nativement dans Godot. C'est un protocole reseau base sur UDP mais qui rajoute de la fiabilite (retransmission des paquets perdus, ordonnancement). Ca offre un bon compromis entre performance et fiabilite par rapport a TCP.

Le projet n'utilise aucune dependance externe (pas de NuGet). Tout est fait avec les APIs Godot et la librairie standard .NET.

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
├── Assets/                     # Sprites, textures, assets graphiques
│   ├── Map/                    # Textures des biomes (eau, sable, herbe, etc.)
│   ├── Objects/                # Sprites des camps, arbres, montagnes
│   └── Units/Characters/       # 8 types d'unites avec 4 directions chacun
├── Scenes/                     # Scenes Godot (.tscn)
│   ├── MainMenu.tscn
│   ├── Game.tscn               # Scene principale du jeu
│   ├── Lobby.tscn              # Lobby multijoueur
│   ├── Unit.tscn               # Prefab d'une unite
│   └── camp_simple.tscn        # Prefab d'un camp
├── Scripts/
│   ├── Game/                   # Logique de jeu
│   ├── Network/                # Multijoueur
│   └── UI/                     # Interface
└── project.godot               # Config Godot (autoloads, inputs)
```

## Architecture

### Scene Tree

Godot fonctionne avec un arbre de noeuds (Scene Tree). Chaque element du jeu est un noeud, et les scenes (.tscn) sont des sous-arbres reutilisables, un peu comme des prefabs dans Unity. Voici l'arbre du jeu en cours de partie :

```
Root
├── Singletons (AutoLoads)      # Charges au demarrage, accessibles partout
│   ├── NetworkManager
│   ├── GameState
│   ├── LocalizationManager
│   └── GameManager
└── Game.tscn
    ├── MapGenerator
    │   ├── Sol (TileMapLayer)
    │   ├── Objets (TileMapLayer)
    │   ├── Camera2D
    │   ├── SelectionManager
    │   └── Units (conteneur des camps et unites)
    ├── GameHUD (interface)
    └── Minimap
```

### Singletons (AutoLoad)

On utilise le systeme d'AutoLoad de Godot pour les managers globaux. Ce sont des scripts charges automatiquement au lancement et qui persistent entre les changements de scene. Ca correspond au pattern Singleton : une seule instance accessible de partout.

- **GameManager** : gere l'or de chaque equipe (pool partage). Accessible via `GameManager.Instance`.
- **NetworkManager** : gere la connexion ENet (heberger/rejoindre). Accessible via `GetNode("/root/NetworkManager")`.
- **GameState** : gere le flux de jeu (generation du seed, chargement des scenes).
- **LocalizationManager** : gere les traductions FR/EN/ES.

On a mis ces systemes en singleton parce qu'ils sont utilises un peu partout : l'or est lu par le HUD, les camps, le systeme de capture... Passer la reference manuellement a chaque noeud serait trop lourd.

### Signaux (Observer Pattern)

Godot utilise un systeme de signaux pour la communication entre noeuds. Un noeud emet un signal, et les noeuds abonnes sont notifies. Ca evite de creer des dependances directes entre les scripts.

Par exemple, `NetworkManager` emet `PlayerConnected` quand un joueur rejoint, et `LobbyUI` s'abonne a ce signal pour mettre a jour la liste. `NetworkManager` n'a pas besoin de connaitre `LobbyUI`.

En C# ca se declare comme ca :

```csharp
[Signal] public delegate void PlayerConnectedEventHandler(long id);

// Emission :
EmitSignal(SignalName.PlayerConnected, id);
```

## Generation procedurale de la carte

**Script :** `Scripts/Game/MapGenerator.cs`

La carte est generee a l'aide de bruit de Perlin (Perlin Noise), via la classe `FastNoiseLite` integree a Godot. Le bruit de Perlin est un algorithme qui genere des valeurs pseudo-aleatoires continues : deux points proches dans l'espace auront des valeurs proches, ce qui donne des formes naturelles (collines, vallees) plutot qu'un bruit completement aleatoire.

On utilise le mode fractal FBM (Fractal Brownian Motion) avec 5 octaves. Le principe du FBM est de superposer plusieurs couches du meme bruit a des echelles differentes : la premiere octave donne la forme generale (continent/ocean), les suivantes ajoutent des details de plus en plus fins.

### Deux couches de bruit

On a deux generateurs de bruit independants :
- Un pour l'elevation (frequence 0.008, assez lisse) qui determine le biome
- Un pour la densite de foret (frequence 0.05, plus detaille) qui determine le placement des arbres

Les seeds sont derivees d'une seed de base : `baseSeed`, `baseSeed + 1000`, `baseSeed + 2000`. Ca garantit des motifs differents tout en restant deterministe.

### Biomes

La valeur d'altitude (entre -1 et 1) est decoupee en biomes par seuils :
- < -0.2 : Eau
- -0.2 a -0.15 : Sable
- -0.15 a 0.4 : Herbe ou foret (selon la densite d'arbres)
- 0.4 a 0.55 : Roche
- > 0.55 : Neige

La carte fait 256x256 tuiles de 128px chacune, soit environ 32 000 x 32 000 px au total. Elle utilise deux TileMapLayers superposes : un pour le sol et un pour les objets (arbres, montagnes).

### Placement des camps

Les camps sont places aleatoirement sur les tuiles d'herbe avec une probabilite de 0.1%. Le placement utilise `System.Random` qui est un PRNG (generateur pseudo-aleatoire) deterministe : pour un meme seed, on obtient la meme sequence de nombres, donc les memes positions de camps. C'est important pour le multijoueur.

La classe utilise l'attribut `[Tool]` de Godot, ce qui permet de lancer la generation directement dans l'editeur pour visualiser le resultat sans demarrer le jeu.

## Systeme d'unites

**Scripts :** `Scripts/Game/Unit.cs`, `Scripts/Game/UnitStats.cs`

### Deplacement avec CharacterBody2D

Les unites heritent de `CharacterBody2D`, un type de noeud Godot prevu pour les corps cinematiques. Contrairement a `RigidBody2D` qui simule la physique (gravite, forces), `CharacterBody2D` donne un controle direct sur le mouvement. On definit un vecteur `Velocity` et on appelle `MoveAndSlide()` qui deplace le corps en gerant les collisions automatiquement (l'unite glisse le long des obstacles au lieu de les traverser).

Le deplacement tourne dans `_PhysicsProcess`, la boucle physique de Godot qui s'execute a un taux fixe (60 fois par seconde), ce qui garantit un mouvement fluide et constant quel que soit le framerate.

Chaque unite a un `CollisionShape2D` circulaire (rayon 40px) pour les collisions entre unites.

### Detection de blocage

On a implemente une detection de blocage custom : si une unite se deplace a moins de 10% de sa vitesse attendue pendant plus de 2 secondes (120 frames), elle s'arrete automatiquement. Il y a un delai de 1 seconde avant de commencer la verification pour eviter les faux positifs au moment ou l'unite demarre.

### Stats (Data-Driven)

Les stats des 8 types d'unites sont definies dans un dictionnaire statique dans `UnitStats.cs`. Ce choix de separer les donnees de la logique (pattern data-driven) fait qu'on peut modifier les equilibrages sans toucher au code de `Unit.cs`.

| Type | Prix | PV | Attaque | Defense | Vitesse | Portee | Production |
|------|------|----|---------|---------|---------|--------|------------|
| Infantry | 50 | 100 | 15 | 10 | 150 | 50 | 2s |
| Support | 75 | 80 | 8 | 5 | 120 | 100 | 3s |
| Range | 80 | 70 | 20 | 5 | 100 | 300 | 3s |
| Heal | 100 | 60 | 0 | 3 | 100 | 150 | 3s |
| AntiArmor | 120 | 80 | 35 | 8 | 90 | 120 | 4s |
| Mortar | 130 | 50 | 40 | 3 | 60 | 400 | 4s |
| Heavy | 150 | 150 | 25 | 20 | 70 | 60 | 5s |
| Tank | 200 | 200 | 30 | 25 | 50 | 100 | 6s |

La formule de degats est soustractive : `degats = max(0, attaque - defense)`. Les unites des camps neutres ont 1.5x PV pour rendre la capture plus difficile.

## Systeme de camps

**Script :** `Scripts/Game/CampSimple.cs`

Les camps heritent de `Area2D` et servent de base pour chaque equipe. Ils generent 50 or/sec et possedent 500 PV.

### File de production

Chaque camp a une file d'attente de production implementee avec `Queue<string>` (max 7 unites). Le fonctionnement suit un pattern de type producteur-consommateur :

1. Le joueur achete une unite → l'or est deduit et le type est ajoute a la queue
2. A chaque frame, si rien n'est en production, on dequeue le prochain type et on lance un timer
3. Quand le timer arrive a 0, l'unite est instanciee depuis le prefab `Unit.tscn` via `PackedScene.Instantiate()` et placee autour du camp

Les unites sont spawnees en cercle a 350px du camp, avec un angle aleatoire.

### Capture

Pour capturer un camp, il faut d'abord eliminer toutes ses unites. Le camp maintient une `List<Unit>` de ses defenseurs et la nettoie a chaque frame avec `IsInstanceValid()` (pour gerer les references vers des noeuds detruits par Godot). Une fois tous les defenseurs morts, le camp devient vulnerable. Quand ses PV tombent a 0, il change d'equipe, retrouve ses PV max, et 3 unites bonus apparaissent.

## Systeme de selection

**Script :** `Scripts/Game/SelectionManager.cs`

Le SelectionManager gere la selection des unites et des camps via `_UnhandledInput`, une methode Godot qui ne recoit les evenements que s'ils n'ont pas deja ete consommes par l'UI.

Deux modes de selection :
- **Clic** : si le joueur clique sans trop bouger la souris (< 10px), on cherche le camp le plus proche (rayon 200px) puis l'unite la plus proche (rayon 64px). Les camps ont la priorite.
- **Rectangle** : si le joueur drag la souris, un rectangle semi-transparent s'affiche et toutes les unites dedans sont selectionnees.

Les unites selectionnees sont teintees en jaune via `Modulate`, une propriete Godot qui multiplie la couleur du sprite. Un clic droit envoie un `MoveTo()` a toutes les unites selectionnees.

## Camera

**Script :** `Scripts/Game/CameraController.cs`

La camera herite de `Camera2D` et gere le zoom + le deplacement.

Le zoom utilise une interpolation lineaire (Lerp) pour etre progressif plutot qu'instantane. On stocke un zoom cible et on interpole vers lui a chaque frame. Le zoom se fait vers la position du curseur grace a un calcul de compensation : on sauvegarde la position monde du curseur avant le zoom, on zoome, puis on deplace la camera pour que le curseur pointe toujours au meme endroit.

La camera est limitee aux bornes de la carte via `Mathf.Clamp`, en tenant compte de la taille du viewport divisee par le zoom (quand on dezoome, la zone visible grandit).

## Minimap

**Script :** `Scripts/UI/Minimap.cs`

La minimap utilise un `SubViewport` Godot : c'est un viewport secondaire qui fait son propre rendu. L'astuce est de partager le meme `World2D` que le viewport principal, ce qui fait que la minimap affiche la meme scene mais avec une camera differente, zoomee pour montrer toute la carte.

Le zoom de la camera minimap est calcule pour que la carte entiere tienne dans le viewport. Un rectangle rouge est dessine par-dessus via `_Draw()` pour montrer la zone visible par la camera principale. En cliquant sur la minimap, on convertit les coordonnees locales en coordonnees monde et on teleporte la camera.

## Multijoueur

**Scripts :** `Scripts/Network/NetworkManager.cs`, `Scripts/Network/GameState.cs`

### ENet et architecture client-serveur

Le multijoueur utilise `ENetMultiplayerPeer` de Godot. ENet est un protocole construit au-dessus d'UDP qui ajoute de la fiabilite (acquittement des paquets) et de l'ordonnancement, tout en gardant la faible latence d'UDP. Le serveur ecoute sur le port 7777 et accepte jusqu'a 4 joueurs.

L'hote cree le serveur, les clients se connectent via IP. L'hote a toujours l'ID 1.

### RPCs

Les appels reseau se font via des RPCs (Remote Procedure Calls). On annote une methode avec `[Rpc]` et Godot se charge de l'appeler sur les machines distantes. On utilise le mode `Authority` qui fait que seul le serveur peut declencher l'appel, ce qui empeche un client de tricher.

### Synchronisation deterministe

Plutot que de synchroniser chaque action (ce qui couterait beaucoup de bande passante), on synchronise uniquement le seed de generation. L'hote genere un seed aleatoire, l'envoie a tous les clients par RPC, et chacun genere sa carte localement. Comme le generateur est deterministe (meme seed = meme carte), tous les joueurs ont la meme carte sans avoir a envoyer les 65 536 tuiles.

C'est possible grace a `System.Random` de .NET qui est un PRNG deterministe : pour un meme seed, la sequence de nombres est identique sur toutes les machines.

## Systeme de groupes

Godot a un systeme de groupes : on peut tagger les noeuds avec des chaines de caracteres puis requeter tous les noeuds d'un groupe. On l'utilise pour :

- `"units"` : toutes les unites (pour la selection)
- `"team_1"`, `"team_2"` : unites par equipe
- `"camps"` : tous les camps (pour la detection au clic)

C'est plus performant que de parcourir tout l'arbre de scenes pour trouver les noeuds du bon type.

## Localisation

**Script :** `Scripts/UI/LocalizationManager.cs`

Le jeu supporte 3 langues (francais, anglais, espagnol). Les traductions sont stockees dans un `Dictionary<string, Dictionary<Language, string>>`. Quand on change de langue, un signal `LanguageChanged` est emis et les elements d'UI re-demandent leurs textes, ce qui met a jour l'affichage sans recharger la scene.

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

## Conventions

- Classes et methodes : PascalCase
- Variables privees : _camelCase
- Variables locales : camelCase
- Commits : `type: description` (feat, fix, refactor, docs)

## Git

- `main` : version stable
- `develop` : integration
- `feature/*` : nouvelles fonctionnalites

Ne jamais push directement sur main. Les features partent de develop et y sont mergees.

## Auteur

- **Darkft28** - [GitHub](https://github.com/Darkft28)
