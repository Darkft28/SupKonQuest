# Changelog

Toutes les modifications notables de ce projet seront documentees dans ce fichier.

Le format est base sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère au [Semantic Versioning](https://semver.org/lang/fr/).

## [Non publie]

### Ajoute
- Structure de branches Git (main, develop, feature/*)
- Documentation du projet (README, CLAUDE.md, CHANGELOG)
- Centrage automatique de la camera sur la map

### Modifie
- Refactoring du CameraController pour calculer le centre dynamiquement
- Suppression du code de zoom en double dans TestSetup

---

## [0.2.0] - 2025-01-22

### Ajoute
- Controles camera ameliores
  - Zoom molette avec interpolation fluide
  - Deplacement clavier (ZQSD/fleches)
  - Drag avec clic droit
  - Zoom centre sur le curseur
- Menu principal
  - Bouton "Play" pour lancer le jeu
  - Bouton "Quit" pour quitter

### Modifie
- Camera spawn au centre de la map
- Organisation des dossiers du projet

---

## [0.1.0] - 2025-01-XX

### Ajoute
- Generation procedurale de map 256x256 tiles
- Systeme de biomes
  - Eau (altitude < -0.2)
  - Sable (altitude < -0.15)
  - Herbe (altitude < 0.4)
  - Foret (densite arbres > 0.2)
  - Roche (altitude < 0.55)
  - Neige (altitude >= 0.55)
- Objets decoratifs
  - Arbres dans les forets
  - Montagnes dans les zones rocheuses
  - Camps aleatoires dans les plaines
- TileMapLayer pour le sol et les objets
- Regeneration de map avec la touche Espace

---

## Versioning

Ce projet utilise le versioning semantique:
- **MAJOR** (X.0.0): Changements incompatibles
- **MINOR** (0.X.0): Nouvelles fonctionnalites retrocompatibles
- **PATCH** (0.0.X): Corrections de bugs retrocompatibles

### Roadmap

- [ ] v0.3.0 - Systeme de gameplay (unites, ressources)
- [ ] v0.4.0 - Interface utilisateur en jeu
- [ ] v0.5.0 - Multijoueur local
- [ ] v1.0.0 - Version stable complete
