# SupKonQuest

Jeu de strategie et de conquete developpe avec Godot 4.5 et C#.

## Description

SupKonQuest est un jeu de strategie ou les joueurs explorent et conquerent une carte generee de maniere procedurale. Le monde est compose de differents biomes avec leurs propres caracteristiques.

## Fonctionnalites

- **Generation procedurale** - Carte de 256x256 tiles generee aleatoirement
- **Biomes varies** - Eau, sable, herbe, foret, roche, neige
- **Objets decoratifs** - Arbres, montagnes, camps
- **Camera fluide** - Zoom, deplacement clavier et souris

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
| Deplacer la camera (drag) | Clic droit + glisser |
| Regenerer la map | Espace |

## Structure du projet

```
SupKonQuest/
├── Assets/          # Ressources graphiques
├── Scenes/          # Scenes Godot (.tscn)
├── Scripts/         # Code C#
│   ├── Game/        # Logique de jeu
│   └── UI/          # Interface utilisateur
└── project.godot    # Configuration Godot
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
3. Commit les changements (`git commit -m 'Ajout de ma feature'`)
4. Push la branche (`git push origin feature/ma-feature`)
5. Ouvrir une Pull Request vers `develop`

## Licence

Ce projet est sous licence MIT - voir le fichier [LICENSE](LICENSE) pour plus de details.

## Auteur

- **Darkft28** - [GitHub](https://github.com/Darkft28)
