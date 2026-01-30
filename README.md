# SupKonQuest

Jeu de strategie et de conquete developpe avec Godot 4.5 et C#.

## Description

SupKonQuest est un jeu de strategie en temps reel ou les joueurs conquerent des camps sur une carte generee proceduralement. Gerez vos ressources, produisez des unites et capturez les camps ennemis pour dominer la carte.

## Fonctionnalites

- **Generation procedurale** - Carte de 256x256 tiles generee aleatoirement avec seed
- **Biomes varies** - Eau, sable, herbe, foret, roche, neige
- **Systeme economique** - Or passif, bonus de capture, achat d'unites
- **8 types d'unites** - Infantry, Support, Heal, Range, AntiArmor, Heavy, Mortar, Tank
- **File de production** - Jusqu'a 7 unites en file d'attente par camp
- **Selection et deplacement** - Selection par clic ou rectangle, deplacement au clic droit
- **Camps capturables** - Eliminez les defenseurs puis capturez le camp
- **Camera fluide** - Zoom, deplacement clavier et souris
- **Multijoueur** - Support reseau via ENet (en developpement)
- **Localisation** - FR/EN/ES

## Prerequis

- [Godot 4.5](https://godotengine.org/) avec support .NET
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)

## Installation

1. Cloner le repository
```bash
git clone https://github.com/Darkft28/SupKonQuest.git
```

2. Ouvrir le projet dans Godot 4.5

3. Lancer le jeu avec F5

## Controles

| Action | Controle |
|--------|----------|
| Deplacer la camera | ZQSD / Fleches |
| Zoom | Molette souris |
| Deplacer la camera (drag) | Clic droit maintenu |
| Selectionner unite/camp | Clic gauche |
| Selection multiple | Clic gauche + glisser |
| Deplacer les unites | Clic droit (sur unites selectionnees) |
| Recentrer camera | C / Home |

## Unites

| Unite | Prix | Description |
|-------|------|-------------|
| Infantry | 50g | Unite de base equilibree |
| Support | 75g | Unite rapide avec bonne portee |
| Range | 80g | Attaque a distance (300 px) |
| Heal | 100g | Soigneur (pas d'attaque) |
| AntiArmor | 120g | Fort contre les blindes |
| Mortar | 130g | Tres longue portee (400 px) |
| Heavy | 150g | Haut HP et defense |
| Tank | 200g | Unite la plus puissante |

## Structure du projet

```
SupKonQuest/
├── Assets/
│   └── Units/Characters/  # Sprites des unites
├── Scenes/                # Scenes Godot (.tscn)
├── Scripts/
│   ├── Game/              # GameManager, Unit, CampSimple, MapGenerator, SelectionManager
│   ├── Network/           # NetworkManager, GameState
│   └── UI/                # GameHUD, MainMenu, Minimap, LobbyUI
└── project.godot
```

## Branches Git

| Branche | Description |
|---------|-------------|
| `main` | Version stable |
| `develop` | Integration et tests |
| `feature/*` | Nouvelles fonctionnalites |

## Contribution

1. Fork le projet
2. Creer une branche feature (`git checkout -b feature/ma-feature develop`)
3. Commit les changements (`git commit -m 'feat: ajout de ma feature'`)
4. Push la branche (`git push origin feature/ma-feature`)
5. Ouvrir une Pull Request vers `develop`

## Licence

Ce projet est sous licence MIT - voir le fichier [LICENSE](LICENSE) pour plus de details.

## Auteur

- **Darkft28** - [GitHub](https://github.com/Darkft28)
