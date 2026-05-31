# SupKonQuest — Manuel utilisateur

Guide pour jouer à SupKonQuest sans connaissance technique préalable.

---

## Table des matières

1. [Installation et lancement](#1-installation-et-lancement)
   - [Windows](#windows)
   - [macOS](#macos)
   - [Linux](#linux)
   - [Mode solo](#mode-solo)
   - [Mode multijoueur en ligne](#mode-multijoueur-en-ligne)
2. [Gameplay](#2-gameplay)
   - [Objectif](#objectif)
   - [Modes de jeu](#modes-de-jeu)
   - [Cartes disponibles](#cartes-disponibles)
   - [Déroulement d'une partie](#déroulement-dune-partie)
   - [Économie](#économie)
   - [Système de tiers](#système-de-tiers)
   - [Types d'unités terrestres](#types-dunités-terrestres)
   - [Navires](#navires)
   - [Capture d'un camp](#capture-dun-camp)
   - [Transport et traversée de l'eau](#transport-et-traversée-de-leau)
3. [Contrôles et interface](#3-contrôles-et-interface)
   - [Caméra](#caméra)
   - [Sélection et ordres](#sélection-et-ordres)
   - [Raccourcis de sélection (macros)](#raccourcis-de-sélection-macros)
   - [Ultimes d'équipe](#ultimes-déquipe)
   - [Interface de jeu (HUD)](#interface-de-jeu-hud)
   - [Mode solo — options](#mode-solo--options)
   - [Langues](#langues)
   - [Multijoueur — authentification](#multijoueur--authentification)
4. [Conseils de stratégie](#4-conseils-de-stratégie)
5. [FAQ — Problèmes fréquents](#5-faq--problèmes-fréquents)
   - [Le jeu ne démarre pas (Windows)](#le-jeu-ne-démarre-pas-windows)
   - [macOS refuse d'ouvrir l'application](#macos-refuse-douvrir-lapplication)
   - [Linux : « Permission denied »](#linux--permission-denied)
   - [Écran noir ou fenêtre qui se ferme immédiatement](#écran-noir-ou-fenêtre-qui-se-ferme-immédiatement)
   - [Pas de son](#pas-de-son)
   - [Impossible de rejoindre une partie en ligne](#impossible-de-rejoindre-une-partie-en-ligne)
   - [Mes unités ne bougent plus / restent bloquées](#mes-unités-ne-bougent-plus--restent-bloquées)
   - [Je ne peux pas acheter de navires](#je-ne-peux-pas-acheter-de-navires)
   - [Je ne peux pas placer un port](#je-ne-peux-pas-placer-un-port)
   - [Le bouton Tier 2 est grisé](#le-bouton-tier-2-est-grisé)
   - [Le jeu est lent ou saccadé](#le-jeu-est-lent-ou-saccadé)
   - [La langue affichée est incorrecte](#la-langue-affichée-est-incorrecte)

---

## 1. Installation et lancement

SupKonQuest se distribue sous forme d'exécutable prêt à l'emploi. Aucune installation de moteur de jeu, de SDK ou de compilation n'est nécessaire.

### Windows

1. Téléchargez et décompressez l'archive Windows.
2. Lancez `**SupKonQuest.exe**`.
3. **Important :** conservez le dossier `**SupKonQuest.dll`** (ou l'ensemble des bibliothèques fournies) **dans le même répertoire** que l'exécutable. Ne déplacez pas le `.exe` seul : le jeu ne démarrera pas sans ces fichiers.

### macOS

1. Téléchargez et décompressez l'archive macOS.
2. Ouvrez `**SupKonQuest.app`**.

macOS bloque souvent les applications non signées avec Gatekeeper (« SupKonQuest ne peut pas être ouvert car le développeur ne peut pas être vérifié »). C'est normal pour une build non signée. Pour lancer le jeu :

**Méthode 1 — Clic droit (recommandée)**

1. Clic droit (ou Ctrl+clic) sur `**SupKonQuest.app`**.
2. Choisissez **Ouvrir**.
3. Confirmez **Ouvrir** dans la boîte de dialogue.

**Méthode 2 — Réglages système**

1. Tentez d'ouvrir l'app une première fois (macOS affiche l'avertissement).
2. Allez dans **Réglages système → Confidentialité et sécurité**.
3. Sous **Sécurité**, cliquez sur **Ouvrir quand même** à côté de SupKonQuest.

**Méthode 3 — Terminal (si les méthodes ci-dessus échouent)**

```bash
xattr -cr /chemin/vers/SupKonQuest.app
```

Puis relancez l'application.

### Linux

1. Téléchargez et décompressez l'archive Linux.
2. Rendez l'exécutable lancable (une seule fois) :

```bash
chmod +x SupKonQuest.x86_64
```

1. Lancez `**SupKonQuest.x86_64**` (ou le nom exact de l'exécutable fourni).
2. **Important :** gardez le dossier des bibliothèques partagées (`.so`) **à côté de l'exécutable**, comme indiqué dans l'archive.

### Mode solo

Choisissez **Solo** dans le menu des modes de jeu. Aucune connexion Internet requise.

### Mode multijoueur en ligne

Le multijoueur fonctionne via les serveurs en ligne du jeu — **aucune configuration de votre côté**.

**Parcours :**

Menu principal → Mode de jeu → **Multijoueur** → Authentification → Lobby → Partie.

Créez un compte (email) ou jouez en **invité**, puis rejoignez le matchmaking (2 à 8 joueurs). Une connexion Internet stable est recommandée.

---

## 2. Gameplay

### Objectif

SupKonQuest est un jeu de stratégie en temps réel (RTS) où vous devez **contrôler toutes les régions de la carte** pour remporter la victoire. Chaque région regroupe plusieurs camps : les conquérir une par une vous rapproche de la domination totale. Chaque camp génère de l'or et permet de produire des unités.

### Modes de jeu


| Mode                | Description                                                                      |
| ------------------- | -------------------------------------------------------------------------------- |
| **Solo vs IA**      | Vous affrontez plusieurs IA (3 niveaux de difficulté).                           |
| **Multijoueur PvP** | 2 à 8 joueurs humains en ligne. Pas d'IA. Chaque joueur reçoit 1 camp de départ. |


### Cartes disponibles

Trois cartes prédéfinies :

- **Irridium** — 3 régions économiques
- **Alabasta** — 4 régions économiques
- **Torskey** — 2 régions économiques

En solo, vous choisissez la carte dans les paramètres. En multijoueur, la carte est tirée au sort automatiquement.

### Déroulement d'une partie

1. Vous commencez avec **1 camp** et **100 pièces d'or**.
2. Les camps génèrent de l'or en continu.
3. Utilisez l'or pour produire des unités et des navires.
4. Envoyez vos troupes conquérir les camps adverses ou neutres, région par région.
5. Le premier joueur (ou la dernière équipe) à **contrôler toutes les régions** gagne.

### Économie


| Source d'or                    | Gain                |
| ------------------------------ | ------------------- |
| Revenu passif (toujours actif) | +75 or/s            |
| Chaque camp possédé            | +50 or/s            |
| Capture d'un camp              | +50 or (instantané) |
| Contrôle d'une région entière  | +30 or/s (bonus)    |


Une **région** regroupe plusieurs camps. Contrôler tous les camps d'une même région active le bonus économique. Contrôler tous les camps de **votre région d'origine** débloque en plus le **tier 3** de production.

### Système de tiers


| Palier     | Comment le débloquer                               | Nouveautés                                         |
| ---------- | -------------------------------------------------- | -------------------------------------------------- |
| **Tier 1** | Automatique au départ                              | Infantry, Support, Range                           |
| **Tier 2** | Acheter pour 1500 or (bouton dans le HUD)          | Heal, AntiArmor                                    |
| **Tier 3** | Contrôler tous les camps de votre région d'origine | Mortar, Heavy, Tank, Transport, Frégate, Destroyer |


### Types d'unités terrestres


| Unité         | Rôle                                            |
| ------------- | ----------------------------------------------- |
| **Infantry**  | Soldat polyvalent, bon rapport qualité/prix     |
| **Support**   | Aura défensive (+10 défense aux alliés proches) |
| **Range**     | Tireur à distance (portée élevée)               |
| **Heal**      | Soigne les alliés blessés, ne combat pas        |
| **AntiArmor** | Anti-blindé (×2 dégâts contre Heavy)            |
| **Mortar**    | Artillerie à dégâts de zone                     |
| **Heavy**     | Infanterie lourde, très résistante              |
| **Tank**      | Blindé lourd, le plus lent et le plus endurant  |


### Navires

Tous les navires nécessitent le **tier 3** et un **port** actif.


| Navire        | Rôle                                                  | Tier |
| ------------- | ----------------------------------------------------- | ---- |
| **Transport** | Embarque jusqu'à 10 unités, les débarque sur une côte | 3    |
| **Frégate**   | Combat naval rapide                                   | 3    |
| **Destroyer** | Combat naval puissant                                 | 3    |


**Conditions pour un port :**

1. Contrôler **au moins une région entière** (tous les camps de cette région).
2. Avoir **500 or** et sélectionner un camp possédé sans port.
3. Cliquer sur le bouton **Port** dans le HUD, puis placer le port sur une **tuile côtière** de votre territoire.

Maximum **5 navires actifs** par équipe.

### Capture d'un camp

La capture se fait en **deux étapes** :

1. **Éliminez tous les défenseurs** du camp (4 unités au départ pour les camps neutres).
2. **Réduisez les points de vie du bâtiment** (barre de vie au-dessus du camp) à zéro.

Récompense : 50 or immédiats + 3 unités bonus.

Les camps neutres ne produisent rien mais possèdent des défenseurs renforcés (50 % de PV en plus). Ils n'attaquent que si vos troupes entrent dans leur zone territoriale.

### Transport et traversée de l'eau

Les unités terrestres ne peuvent pas traverser l'eau seules. Utilisez un **Transport** :

1. Produisez un Transport depuis un port (tier 3 requis).
2. Sélectionnez vos unités, clic droit sur le Transport allié → embarquement.
3. Déplacez le Transport vers une côte, clic droit sur la côte → débarquement.

---

## 3. Contrôles et interface

### Caméra


| Action                    | Touche                                     |
| ------------------------- | ------------------------------------------ |
| Déplacer la caméra        | **Z Q S D** ou **flèches directionnelles** |
| Zoom avant / arrière      | **Molette souris**                         |
| Déplacer la caméra (drag) | **Clic droit maintenu**                    |
| Recentrer sur la carte    | **C** ou **Home**                          |


**Recentrer** replace la caméra au **centre géographique de la carte**, pas sur votre camp. Utile pour vous retrouver après un déplacement.

### Sélection et ordres


| Action                                     | Contrôle                          |
| ------------------------------------------ | --------------------------------- |
| Sélectionner une entité                    | **Clic gauche**                   |
| Sélection multiple (rectangle)             | **Clic gauche + glisser**         |
| Déplacer les unités / navires sélectionnés | **Clic droit** sur la destination |
| Embarquer sur un Transport allié           | **Clic droit** sur le Transport   |


Les entités sélectionnées changent de couleur (jaunes pour les unités, cyan pour les navires).

### Raccourcis de sélection (macros)

Des raccourcis permettent de sélectionner rapidement vos troupes par type. Les touches par défaut sont reconfigurables dans le menu principal.


| Raccourci (défaut)  | Action                                                                 |
| ------------------- | ---------------------------------------------------------------------- |
| **1** à **8**       | Sélectionner toutes vos unités du type correspondant (Infantry → Tank) |
| **9**, **0**, **-** | Sélectionner vos navires (Transport, Frégate, Destroyer)               |
| **A**               | Sélectionner toutes vos unités possédées                               |


### Ultimes d'équipe

Les ultimes sont des capacités **d'équipe** : n'importe quel joueur peut les déclencher pour toute l'équipe (pas besoin d'avoir un Heal ou un Support sélectionné).


| Action                            | Contrôle                                     |
| --------------------------------- | -------------------------------------------- |
| Ultime de soin (zone)             | **E** puis **clic gauche** sur la zone cible |
| Ultime de support (bonus défense) | **R** puis **clic gauche** sur la zone cible |
| Annuler le ciblage                | **Échap** ou **clic droit**                  |


Temps de recharge : 20 s (soin), 25 s (support).

### Interface de jeu (HUD)

**Or possédé** : toujours visible en **bas à gauche**, à côté de la minimap.

**Minimap** : en **bas à gauche**. Le rectangle rouge indique la zone visible. Cliquez ou glissez pour déplacer la caméra.

Quand vous sélectionnez un camp possédé :

- **Boutons d'achat** : produire des unités (selon votre tier).
- **Bouton Tier 2** : débloquer le palier 2 (1500 or).
- **Bouton Port** : acheter un port (500 or) si vous contrôlez au moins une région entière, puis cliquer sur une tuile côtière.
- **Boutons navires** : si un port est actif et tier 3 débloqué, produire Transport / Frégate / Destroyer.

**Leaderboard** : classement en jeu (camps et régions contrôlées par équipe).

### Mode solo — options

Dans le popup de lancement solo :

- **Carte** : Irridium, Alabasta ou Torskey
- **Difficulté IA** : Facile, Moyen, Difficile
- **Mode rapide** : accélère le jeu (×3)

### Langues

Le jeu est disponible en **français**, **anglais** et **espagnol**. Changez la langue via le bouton de langue présent dans chaque menu (cycle FR → EN → ES).

### Multijoueur — authentification

- **Compte email** : inscription ou connexion (mot de passe ≥ 8 caractères).
- **Invité** : jouer sans compte (pseudo modifiable dans le lobby).
- **Déconnexion** : bouton dans le lobby pour effacer la session et revenir à l'écran de connexion.

---

## 4. Conseils de stratégie

1. **Économisez tôt** pour le tier 2 — Heal et AntiArmor changent la donne.
2. **Priorisez les régions entières** : bonus d'or (+30/s), tier 3, et accès au naval.
3. **Protégez vos camps** — la tourelle du bâtiment inflige des dégâts aux assaillants même sans unités sur place.
4. **Utilisez le naval** pour attaquer des régions séparées par l'eau une fois une région complète conquise.
5. **Composez votre armée** : Support + Heavy/Tank devant, Range/Mortar derrière, Heal au centre.
6. **Surveillez le leaderboard** pour identifier le joueur en tête et prioriser vos cibles.

---

## 5. FAQ — Problèmes fréquents

### Le jeu ne démarre pas (Windows)

- Vérifiez que le dossier `**SupKonQuest.dll`** (et les autres fichiers de l'archive) est **bien à côté** de `SupKonQuest.exe`.
- Installez les [Microsoft Visual C++ Redistributables](https://learn.microsoft.com/fr-fr/cpp/windows/latest-supported-vc-redist) (x64) si un message indique une DLL manquante.
- Ajoutez une exception dans votre antivirus si l'exécutable est bloqué ou supprimé à l'extraction.

### macOS refuse d'ouvrir l'application

- Utilisez **clic droit → Ouvrir** (voir section Installation macOS).
- Si le message mentionne une app « endommagée », exécutez `xattr -cr SupKonQuest.app` dans le Terminal.
- Sur Mac avec puce Apple (M1/M2/M3), utilisez la build **ARM64** si elle est proposée ; sinon la build Intel peut tourner via Rosetta (plus lent).

### Linux : « Permission denied »

```bash
chmod +x SupKonQuest.x86_64
./SupKonQuest.x86_64
```

Si des bibliothèques `.so` sont manquantes, lancez le jeu **depuis le dossier de l'archive**, sans déplacer l'exécutable seul.

### Écran noir ou fenêtre qui se ferme immédiatement

- Mettez à jour vos **pilotes graphiques**.
- Sur laptop, forcez l'utilisation du GPU dédié pour SupKonQuest dans les paramètres graphiques du système.
- Relancez en mode fenêtré si disponible ; vérifiez que la résolution d'affichage n'est pas inférieure au minimum requis.

### Pas de son

- Vérifiez le volume système et le volume in-game (menu principal / réglages audio).
- Sous Windows, vérifiez que SupKonQuest n'est pas en sourdine dans le **mixeur de volume**.

### Impossible de rejoindre une partie en ligne

- Vérifiez votre **connexion Internet**.
- Désactivez temporairement VPN ou proxy.
- Autorisez SupKonQuest dans le **pare-feu** (ports sortants HTTPS/WebSocket).
- Réessayez après quelques minutes : le matchmaking attend parfois d'autres joueurs (2 minimum).
- Déconnectez-vous puis reconnectez-vous (compte ou invité) si le lobby reste bloqué.

### Mes unités ne bougent plus / restent bloquées

- Donnez un **nouvel ordre** (clic droit ailleurs) : le jeu détecte les blocages et repasse en attente après ~2 secondes sans progression.
- Évitez d'envoyer des unités terrestres dans l'eau ou sur des zones rocheuses/inaccessibles.
- Encombrement : espacez vos groupes ou contournez les obstacles.

### Je ne peux pas acheter de navires

Conditions cumulatives :

1. **Tier 3** débloqué (tous les camps de **votre région d'origine** contrôlés).
2. Un **port** construit (500 or + **région entière** contrôlée au préalable).
3. Assez d'**or** et moins de **5 navires** actifs.
4. Sélectionnez le **port** pour voir les boutons navires.

### Je ne peux pas placer un port

- Vous devez contrôler **tous les camps d'au moins une région**.
- Sélectionnez un **camp possédé** sans port existant.
- Cliquez sur une tuile **terrestre de votre territoire**, **adjacente à l'eau** (littoral).
- Vérifiez que vous avez **500 or**.

### Le bouton Tier 2 est grisé

- Il faut **1500 or** disponibles. Attendez que votre économie monte (passif + camps + bonus région).

### Le jeu est lent ou saccadé

- Fermez les applications gourmandes en arrière-plan.
- Réduisez le zoom ou limitez le nombre d'unités sélectionnées simultanément en fin de partie dense.
- En mode solo, le **mode rapide** (×3) accélère le temps de jeu mais pas les performances graphiques.

### La langue affichée est incorrecte

- Utilisez le bouton **langue** dans le menu (cycle FR → EN → ES). Le changement s'applique immédiatement sans redémarrer.

---

