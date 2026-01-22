# Guide de contribution

Merci de vouloir contribuer a SupKonQuest!

## Workflow Git

### Branches

```
main              # Production stable - NE PAS PUSH DIRECTEMENT
└── develop       # Integration et tests
    ├── feature/* # Nouvelles fonctionnalites
    └── fix/*     # Corrections de bugs
```

### Creer une nouvelle feature

```bash
# Se placer sur develop
git checkout develop
git pull origin develop

# Creer la branche feature
git checkout -b feature/ma-nouvelle-feature

# Developper...

# Commiter
git add .
git commit -m "Ajout de ma feature"

# Pousser
git push -u origin feature/ma-nouvelle-feature
```

### Merger dans develop

```bash
git checkout develop
git pull origin develop
git merge feature/ma-nouvelle-feature
git push origin develop
```

### Merger develop dans main

```bash
git checkout main
git pull origin main
git merge develop
git push origin main
```

## Conventions de code

### Nommage C#

| Element | Convention | Exemple |
|---------|------------|---------|
| Classes | PascalCase | `CameraController` |
| Methodes | PascalCase | `GenererMap()` |
| Variables privees | _camelCase | `_tileMapSol` |
| Variables locales | camelCase | `halfWidth` |
| Constantes | PascalCase | `MapWidth` |

### Structure des fichiers

```
Scripts/
├── Game/          # Logique de jeu (generation, camera, etc.)
├── UI/            # Scripts d'interface
├── Network/       # Code reseau (futur)
└── Utils/         # Utilitaires partages
```

### Commits

Format recommande:
```
Type: Description courte

Description detaillee si necessaire
```

Types:
- `feat:` Nouvelle fonctionnalite
- `fix:` Correction de bug
- `refactor:` Refactoring sans changement fonctionnel
- `docs:` Documentation
- `style:` Formatage, style de code
- `test:` Ajout/modification de tests

## Checklist avant merge

- [ ] Le code compile sans erreur
- [ ] Le jeu se lance correctement
- [ ] Les nouvelles fonctionnalites sont testees
- [ ] Le CHANGELOG est mis a jour
- [ ] Pas de code commente inutile
