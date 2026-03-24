# TODO - SupKonQuest

Mis a jour le 2026-03-17 — corrections territoire, port, spawn, reseau + variation tuiles + audit IA Sprint 1.
Format : priorite, domaine, description, solution proposee, fichiers concernes.

---

## EVALUATION PRE-RENDU 2 : Etat actuel vs attendu

### 1. Architecture Reseau & Multijoueur

| Critere                    | Attendu | Etat      | Detail                                                                              |
|----------------------------|---------|-----------|-------------------------------------------------------------------------------------|
| Jeu en ligne (LAN)         | Oui     | FAIT      | ENet P2P port 7777, decouverte UDP 7778, code salon 6 chars                         |
| Synchronisation mouvements | Oui     | FAIT      | RPC unreliable 20Hz batch (positions, HP, etats)                                    |
| Synchronisation production | Oui     | FAIT      | Seul le peer proprietaire traite la queue, RpcSpawnUnit reliable                    |
| Synchronisation capture    | Oui     | FAIT      | RpcCampCaptured reliable, attribution via RpcSyncCampAssignments                    |
| Synchronisation or         | Oui     | FAIT      | RpcSyncGold toutes les 10s cote serveur, correction si ecart > 5 or                 |
| Gestion deconnexion        | Oui     | FAIT      | Overlay HUD + retour menu 5s dans OnPeerDisconnected / OnServerDisconnected         |
| Lobby / Pseudo             | Oui     | PARTIEL   | Lobby avec code salon presente, mais pas de compte/pseudo persistant                |

**Actions requises :**
- [ ] [PR2-NET-01] (Optionnel) Ajouter un champ "pseudo" dans le lobby affiche a l'autre joueur

---

### 2. Finalisation du Gameplay & Unites

#### Unites terrestres avancees

| Unite     | Mecanique specifique     | Etat | Detail                                                         |
|-----------|--------------------------|------|----------------------------------------------------------------|
| Support   | Buffs defense zone       | FAIT | +10 defense aux allies a 200px, cap +40 (anti-stacking)        |
| Heal      | Soin actif               | FAIT | +12 HP/sec, cible allie le plus endommage dans la portee       |
| AntiArmor | Degats bonus vs Heavy    | FAIT | x2 degats appliques dans Unit.Combat.cs avant calcul reduction |
| Mortar    | Degats de zone AoE       | FAIT | Splash 200px rayon, 20 degats plats (ignore la defense)        |

Toutes les 8 unites terrestres sont implementees. **COMPLET.**

#### Naval & Environnement

| Critere                                 | Attendu | Etat    | Detail                                                                                              |
|-----------------------------------------|---------|---------|-----------------------------------------------------------------------------------------------------|
| Ports (batiments speciaux)              | Oui     | FAIT    | Auto-detection cote, orientation intelligente, file production (max 5)                              |
| Transport (embarquement/debarquement)   | Oui     | FAIT    | Capacite 10 unites, RPC UnitBoarded/TransportUnloaded                                               |
| Fregate                                 | Oui     | FAIT    | 180HP, 20 ATK, portee 250, 200g                                                                     |
| Destroyer                               | Oui     | FAIT    | 250HP, 35 ATK, portee 350, 300g                                                                     |
| Gestion obstacles (forets, montagnes)   | Oui     | PARTIEL | Spawn unites evite forets/arbres/montagnes (FindClearSpawnPosition). Traversal pas encore bloque.   |
| Pathfinding terrestre vs naval          | Oui     | PARTIEL | Unites terrestres ne vont pas sur l'eau (checks tuile), navires en eau seulement. Pas de A* reel.  |

**Actions requises :**
- [ ] [PR2-NAV-01] Forets et montagnes doivent bloquer le deplacement des unites terrestres (CollisionShape2D ou check de tuile dans Movement) — le spawn est deja corrige
- [ ] [PR2-NAV-02] L'IA ne prend pas de bateaux. Si certains camps sont sur des iles, l'IA ne peut pas les atteindre. Ajouter logique navale dans AIController.

---

### 3. Intelligence Artificielle

| Critere                                  | Attendu | Etat     | Detail                                                                  |
|------------------------------------------|---------|----------|-------------------------------------------------------------------------|
| 3 niveaux de difficulte                  | Oui     | FAIT     | Easy/Medium/Hard avec tick, max unites, types differents                |
| Produire des unites                      | Oui     | FAIT     | BuyUnits() achete cycliquement                                          |
| Former des groupes                       | Oui     | PARTIEL  | Envoie N unites en meme temps vers meme cible (pas de formation)        |
| Attaquer des camps adverses              | Oui     | FAIT     | Score-based targeting, priorite camps neutres                           |
| Se defendre                              | Non     | MANQUANT | L'IA n'envoie aucune unite defendre ses propres camps                   |
| Pathfinding correct (eviter obstacles)   | Oui     | FAIT     | NavigationAgent2D Godot (Unit.Movement.cs + Ship.Movement.cs)           |
| Utiliser les bateaux                     | Oui     | MANQUANT | AIController n'a aucune logique navale                                  |

**Actions requises :**
- [ ] [PR2-IA-01] Ajouter defense des camps a l'IA (garder 25-30% unites pour defendre) - voir [AMELIO-07]
- [ ] [PR2-IA-02] Ajouter logique navale a l'IA : detecter les camps non accessibles par voie terrestre et produire/utiliser des Transports

---

### 4. Interface & Experience Utilisateur

| Critere                                   | Attendu | Etat     | Detail                                                             |
|-------------------------------------------|---------|----------|--------------------------------------------------------------------|
| i18n 3 langues                            | Oui     | FAIT     | FR/EN/ES, 166 cles, cycle dans les menus                           |
| Systeme de regions (visuel + economique)  | Oui     | FAIT     | TerritoryManager (couleurs), CheckRegionBonuses() +30 or/sec       |
| Boutons achat desactives si impossible    | Oui     | FAIT     | UpdateUnitButtons/UpdateShipButtons dans GameHUD.cs + tooltips     |
| Feedback visuel sorts (Heal/Boost)        | Oui     | PARTIEL  | Soin sans effet visuel particulier, aura Support invisible         |
| Sons de base (attaques, construction)     | Oui     | MANQUANT | Aucun son implemente                                               |

**Actions requises :**
- [ ] [PR2-UX-01] Ajouter effet visuel pour le soin (faisceau vert, particules) sur le Healer - voir [AMELIO-15]
- [ ] [PR2-UX-02] Ajouter effet visuel pour l'aura Support (cercle ou halo bleu autour des unites buffees)
- [ ] [PR2-UX-03] Ajouter sons de base : attaque, mort unite, capture camp, achat/production - voir [AMELIO-15]
- [ ] [PR2-UX-04] (Optionnel) Son de victoire/defaite

---

### 5. Documentation

| Critere                                    | Attendu | Etat     | Detail                                                                                   |
|--------------------------------------------|---------|----------|------------------------------------------------------------------------------------------|
| Architecture reseau documentee             | Oui     | PARTIEL  | README explique ENet/P2P mais pas en detail technique (RPCs, Authority model)            |
| Algorithme IA documente                    | Oui     | MANQUANT | Aucune documentation de l'algo IA (score-based targeting, niveaux)                       |
| Regles nouvelles unites (sorts, bateaux)   | Oui     | FAIT     | README mis a jour avec stats navires, interactions speciales                             |
| Guide connexion multijoueur                | Oui     | PARTIEL  | README mentionne code salon mais sans etape pas-a-pas                                    |

**Actions requises :**
- [ ] [PR2-DOC-01] Ajouter section "Architecture Reseau Detaillee" dans README ou doc separee : topologie P2P, liste RPCs, modele Authority/Puppet, batch sync 20Hz
- [ ] [PR2-DOC-02] Ajouter section "Intelligence Artificielle" dans README : algorithme score-based targeting, parametres par niveau
- [ ] [PR2-DOC-03] Ajouter guide pas-a-pas dans README : "Comment jouer en multijoueur" (creer salon → partager code → lancer)

---

### Bilan Pre-Rendu 2 : Resume

| Domaine                     | Etat                                                          |
|-----------------------------|---------------------------------------------------------------|
| Reseau/Multijoueur          | 95% - deconnexion geree, sync or OK, manque pseudo lobby      |
| Unites terrestres (8/8)     | 100% - complet                                                |
| Naval (ports + 3 navires)   | 90% - obstacles non bloqueants                                |
| IA (3 niveaux)              | 75% - NavigationAgent2D OK, manque defense et logique navale  |
| UI/UX (i18n, regions)       | 85% - boutons achats OK, manque sons et effets visuels sorts  |
| Documentation               | 65% - manque detail reseau/IA                                 |

**Estimation score global Pre-Rendu 2 : ~90% des criteres satisfaits** *(mis a jour 2026-03-09)*

---

## MOYEN - Ameliorations importantes

### [AMELIO-02] Afficher la file de production active
**Impact :** Le joueur ne sait pas ce qui est en cours de production ni combien de temps il reste.

**Solution :**
Ajouter un panneau dans le HUD quand un camp est selectionne :
```
File : [Infantry - 1.8s] [Range] [Tank]  (3/7)
```
```csharp
// CampSimple exposer :
public string GetCurrentProduction() => _currentProduction;
public float GetProductionTimeRemaining() => _productionTimer;
public IReadOnlyCollection<string> GetProductionQueue() => _productionQueue;

// GameHUD.cs update chaque frame si camp selectionne
```

**Fichiers :** `Scripts/UI/GameHUD.cs`, `Scripts/Camps/CampSimple.Production.cs`

---

### [AMELIO-03] Systeme de toast/notification UI
**Impact :** Aucun feedback visuel pour les evenements importants (capture imminente, victoire region...).

**Solution :**
Creer un `ToastManager` simple :
```csharp
// Scripts/UI/ToastManager.cs
public partial class ToastManager : CanvasLayer
{
    public static ToastManager Instance;
    private Label _label;
    private Tween _tween;

    public void Show(string message, float duration = 2.5f)
    {
        _label.Text = message;
        _label.Visible = true;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenInterval(duration);
        _tween.TweenCallback(Callable.From(() => _label.Visible = false));
    }
}
```
Utilisation : `ToastManager.Instance?.Show("Region Nord-Ouest capturee ! +30 or/sec")`.

**Fichiers :** `Scripts/UI/ToastManager.cs` (nouveau), `Scenes/GameHUD.tscn`

---

### [AMELIO-04] Indicateur de progression victoire en temps reel
**Impact :** Aucune information sur qui gagne. Le joueur decouvre la victoire uniquement a la fin.

**Solution :**
Barre de progression en haut du HUD :
```
[Equipe 1: 4/7 camps] ████░░░ [Equipe 2: 2/7 camps]
```
```csharp
// Scripts/UI/GameHUD.cs _Process()
int total = allCamps.Count(c => !c.IsNeutralCamp);
int team1 = allCamps.Count(c => c.TeamId == 1);
int team2 = allCamps.Count(c => c.TeamId == 2);
_team1Progress.Value = (float)team1 / total;
_team2Progress.Value = (float)team2 / total;
```

**Fichiers :** `Scripts/UI/GameHUD.cs`, `Scenes/GameHUD.tscn`

---

### [AMELIO-05] Confirmation avant quitter une partie multijoueur
**Impact :** Clic accidentel sur "Retour" en cours de partie → quitte sans prevenir l'adversaire.

**Solution :**
```csharp
// Scripts/UI/GameHUD.cs - OnPauseMenuQuit()
private void OnQuitPressed()
{
    if (NetworkManager.Instance?.IsConnected() == true)
    {
        var popup = GetNode<ConfirmationDialog>("QuitConfirmDialog");
        popup.DialogText = "Quitter la partie ? L'adversaire gagnera.";
        popup.Confirmed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        popup.PopupCentered();
    }
    else
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
}
```

**Fichiers :** `Scripts/UI/GameHUD.cs`, `Scenes/GameHUD.tscn`

---

### [AMELIO-06] Persistance de la langue choisie
**Impact :** La langue revient en francais a chaque lancement du jeu.

**Solution :**
```csharp
// Scripts/UI/LocalizationManager.cs
private const string SavePath = "user://settings.cfg";

public void SaveLanguage()
{
    var config = new ConfigFile();
    config.SetValue("settings", "language", (int)CurrentLanguage);
    config.Save(SavePath);
}

public void LoadLanguage()
{
    var config = new ConfigFile();
    if (config.Load(SavePath) == Error.Ok)
        SetLanguage((Language)(int)config.GetValue("settings", "language", 0));
}
```
Appeler `LoadLanguage()` dans `_Ready()` et `SaveLanguage()` a chaque changement.

**Fichiers :** `Scripts/UI/LocalizationManager.cs`

---

### [AMELIO-07] Ameliorer l'IA - defense des propres camps
**Impact :** L'IA envoie 100% de ses unites a l'attaque et ne defende jamais ses propres camps.

**Solution :**
```csharp
// Scripts/AI/AIController.cs - CommandIdleUnits()
// Reserver 30% pour la defense
int defenseCount = allUnits.Count / 3;
var defenders = allUnits.Take(defenseCount).ToList();
var attackers = allUnits.Skip(defenseCount).ToList();

// Defenders : aller au camp le plus menace (camp IA avec le moins de HP)
var mostThreatenedCamp = GetAICamps().OrderBy(c => c.GetCurrentHealth()).FirstOrDefault();
if (mostThreatenedCamp != null)
    foreach (var unit in defenders)
        unit.MoveTo(mostThreatenedCamp.GlobalPosition + RandomOffset());
```

**Fichiers :** `Scripts/AI/AIController.cs`

---

### [AMELIO-08] Refactoriser GameManager - trop de responsabilites
**Impact :** `GameManager.cs` gere l'economie, l'attribution des camps, les bonus de region et la victoire. Viole le SRP.

**Solution :**
Extraire en classes separees :
```
Scripts/Economy/
├── GameManager.cs          # Coordinateur leger
├── EconomySystem.cs        # AddGold, SpendGold, GetGold, passive income
├── RegionBonusSystem.cs    # CheckRegionBonuses(), GetRegionBonus()
├── CampAssignmentSystem.cs # AssignCampsToPlayers(), ShuffleList()
└── VictoryManager.cs       # CheckVictoryCondition(), DeclareVictory()
```

**Fichiers :** `Scripts/Economy/GameManager.cs` (refactoring majeur)

---

### [AMELIO-09] Centraliser les constantes de jeu
**Impact :** Magic numbers disperses dans 5+ fichiers.

**Solution :**
Creer `Scripts/GameConfig.cs` avec toutes les constantes (vitesses, HP, or, reseau...).

**Fichiers :** `Scripts/GameConfig.cs` (nouveau)

---

### [AMELIO-10] Distinctions visuelles port vs camp
**Impact :** Les ports et les camps se ressemblent visuellement. Un joueur peut rater le port.

**Solution :**
- Ajouter une icone ancre au-dessus des camps avec un port
- Ou cercle de selection visible au survol

**Fichiers :** `Scripts/Camps/CampSimple.Visuals.cs`, `Scripts/UI/Minimap.cs`

---

## BAS - Ameliorations nice-to-have

### [AMELIO-11] Tooltips sur les boutons d'achat
**Impact :** Le joueur doit deviner les stats de chaque unite.

**Fichiers :** `Scripts/UI/GameHUD.cs`, `Scenes/GameHUD.tscn`

---

### [AMELIO-12] Optimiser la recherche d'ennemis dans DetectionRange
**Impact :** `GetNodesInGroup("units")` appele chaque frame pour chaque unite. Couteux avec 100+ unites.

**Solution :** Cache la liste des ennemis, mis a jour toutes les 0.5s.

**Fichiers :** `Scripts/Units/Unit.cs`, `Scripts/Units/Unit.Combat.cs`

---

### [AMELIO-13] Optimiser CheckRegionBonuses dans GameManager
**Impact :** Nouveau dictionnaire cree chaque seconde. Invalider le cache uniquement lors des captures.

**Fichiers :** `Scripts/Economy/GameManager.cs`

---

### [AMELIO-14] Indicateur de bonus de region dans l'UI
**Impact :** Le joueur ne sait pas qu'il a un bonus de region actif.

**Solution :** Mini-carte des 4 regions dans le HUD ou toast quand une region est completee/perdue.

**Fichiers :** `Scripts/UI/GameHUD.cs`, `Scripts/Economy/GameManager.cs`

---

### [AMELIO-15] Son et effets visuels manquants
**Impact :** Aucun feedback sonore. Le jeu est silencieux.

**A implementer (par priorite) :**
1. Son de capture de camp
2. Son de mort d'unite
3. Son d'achat/production
4. Son de tir projectile
5. Musique de fond
6. Effet flash degats
7. Particules de mort

**Fichiers :** Nouveaux assets audio + `Scripts/Units/Unit.Visuals.cs`, `Scripts/Camps/CampSimple.Visuals.cs`

---

### [AMELIO-16] Sauvegarde/chargement de partie
Serialisation de l'etat via `FileAccess` dans `user://save.json`. Uniquement en mode solo/IA.

---

### [AMELIO-17] Formation navale
**Solution :** Offset de formation V dans `Ship.Movement.cs`.

**Fichiers :** `Scripts/Ships/Ship.Movement.cs`, `Scripts/Selection/SelectionManager.cs`

---

### [AMELIO-18] Mini-tutoriel in-game
Bulles d'aide au premier lancement.

---

### [AMELIO-19] Ajout de null-checks defensifs
Localisations : `GameManager._Process()`, `SelectionManager._camera`, `CampSimple.BuyUnit()`.

---

### [AMELIO-20] Navigation clavier dans les menus
Definir `theme_override_styles/focus` avec bordure coloree dans le theme Godot.

---

---

## AUDIT IA — 2026-03-17

*Audit realise par 4 agents (analyse code + recherches internet : patterns RTS, game feel, economie). Sources : ResearchGate, AAAI, GDC, AoE IV, StarCraft AI, Dune II.*

---

### Sprint 1 — Bugs critiques (impact immediat, faible risque)

- [x] [IA-BUG-01] **Double CheckRegionBonuses() dans GameManager._Process()** — (non-reproductible : CheckRegionBonuses() n'existe pas dans le code actuel de GameManager.cs). **Fichiers :** `Scripts/Economy/GameManager.cs`

- [x] [IA-BUG-02] **Healers comptes comme attaquants** — fix applique : dans `CommandIdleUnits()`, les unites `UnitType == "Heal"` sont exclues de la liste des attaquants (unitsByCamp loop + roamingUnits loop). **Fichiers :** `Scripts/AI/AIController.cs`

- [x] [IA-BUG-03] **GetAICenter() inclut les navires** — (non-reproductible : Ship n'herite pas de Unit, le filtre `node is Unit` exclut deja les navires). **Fichiers :** `Scripts/AI/AIController.cs`

- [x] [IA-BUG-04] **FindIdleAIUnits() sans validation** — fix applique : `IsInstanceValid(unit)` et `unit.GetCurrentHealth() > 0` ajoutes dans `FindIdleAIUnits()`. **Fichiers :** `Scripts/AI/AIController.cs`

- [x] [IA-BUG-05] **OwnerCamp non reassigne apres capture** — fix applique : dans `UpdateSpawnedUnitsTeam()`, si `oldUnitTeam != TeamId && unit.OwnerCamp == this`, alors `unit.OwnerCamp = null`. **Fichiers :** `Scripts/Camps/CampSimple.cs`

---

### Sprint 2 — Comportement de base (haute valeur, faible complexite)

- [ ] [IA-02-01] **Timer de premier attaque** — l'IA ne doit pas attaquer avant N secondes apres le debut de partie. Easy=90s, Medium=45s, Hard=20s. Ajouter `_gameStartTimer` dans `RunTick()`. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-02-02] **Delai de reaction** — introduire `_reactionDelay` (Easy=4s, Medium=1.5s, Hard=0.5s) entre la detection d'une situation et l'execution de `CommandIdleUnits()`. L'IA "voit" mais reagit lentement, exploitable consciemment par le joueur. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-02-03] **Taux d'erreur de ciblage** — Easy: 35% de chance de choisir un camp sous-optimal (pas le meilleur score). Medium: 15%. Hard: 0%. Plus fun qu'un skip complet car l'erreur est lisible. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-02-04] **Seuil de retraite** — si un groupe d'attaque perd >50% de sa taille initiale en route, les survivants font `MoveTo()` vers le camp IA le plus proche. Easy et Medium uniquement (Hard ne recule jamais). **Fichiers :** `Scripts/AI/AIController.cs`, `Scripts/Units/Unit.cs`

- [ ] [IA-02-05] **Defense reactive** — verifier en debut de chaque tick si un camp IA est sous attaque (HP < 80% OU ennemi detecte dans `TerritoryRadius`). Si oui, rediriger les unites idle les plus proches vers ce camp avant tout ordre offensif. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-02-06] **Timer d'attaque minimum** — si l'IA n'a pas attaque depuis 120s (Easy), 90s (Medium), 60s (Hard), forcer une attaque avec ce qui est disponible. Evite le turtling infini. **Fichiers :** `Scripts/AI/AIController.cs`

---

### Sprint 3 — Qualite strategique (valeur haute, complexite moyenne)

- [ ] [IA-03-01] **Composition d'armee par role** — remplacer le choix aleatoire par un systeme de ratio cible : 35% tank/melee (Infantry, Heavy), 35% DPS/range (Range, AntiArmor, Mortar), 30% support (Support, Heal). L'IA comptabilise sa composition actuelle et achete ce qui manque. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-03-02] **Adaptation economique** — calculer `scoreRelatif = (campsIA / total) - (campsEnnemi / total)`. Si > +0.3 (avance) : acheter premium (Heavy, Tank). Si < -0.3 (retard) : spam Infantry. Sinon : mixte. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-03-03] **Personnalites d'IA** — definir 3 profils comportementaux selectionnes par niveau ou aleatoirement :
  - **Rusher** : attaque des 4 unites, 80% Infantry, timer 60s, seuil d'epargne 0-50g
  - **Tortue** : attaque seulement a 8 unites + 200g en reserve, 30% Heavy/AntiArmor
  - **Macro** : priorite capture camps neutres pour revenus, puis armee premium (Tank, AntiArmor)
  - Chaque personnalite a un defaut exploitable connu (Rusher = camps vides, Tortue = rush early, Macro = fragile early).
  - **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-03-04] **Recalibrer le scoring de cible** — les constantes actuelles (+4000, -2000, -3000) sont en pixels absolus et incoherentes selon la taille de la map. Normaliser en fonction de `mapMaxDistance` pour que les bonus/malus representent des % de la distance max. **Fichiers :** `Scripts/AI/AIController.cs`

---

### Sprint 4 — Fonctionnalites avancees (valeur haute, complexite elevee)

- [ ] [IA-04-01] **Multi-front pour Hard** — selectionner 2 camps cibles distincts et envoyer des groupes separes depuis les camps IA les plus proches de chaque cible. Force le joueur a gerer 2 fronts simultanement. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-04-02] **Composition adaptative (counter)** — observer `GetTree().GetNodesInGroup("team_1")` pour compter les types d'unites du joueur. Si >3 Heavy : augmenter priorite AntiArmor. Si >5 Infantry groupes : ajouter Mortar. **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-04-03] **AI Director leger** — si le joueur controle >70% des camps : forcer une contre-attaque sur son camp le plus faiblement defandu. Si l'IA gagne tres largement : ajouter un delai artificiel entre les vagues (fenetre de comeback invisible). **Fichiers :** `Scripts/AI/AIController.cs`

- [ ] [IA-04-04] **Faiblesse geographique intentionnelle (Hard)** — definir une zone aveugle (ex: quadrant oppose en diagonale) que l'IA Hard n'attaque jamais. Win condition cachee pour les joueurs experts. Rend l'IA previsible sur un axe sans etre trop facile. **Fichiers :** `Scripts/AI/AIController.cs`

---

### Tableau de difficulte cible (apres implementation)

| Parametre | Easy | Medium | Hard |
|---|---|---|---|
| Timer premier attaque | 90s | 45s | 20s |
| Delai de reaction | 4-5s | 1.5-2s | <0.5s |
| Taux d'erreur ciblage | 35% | 15% | 0% |
| % unites en defense | 60-70% | 40-50% | 25-35% |
| Seuil min avant attaque | 8 unites | 5 unites | 3 unites |
| Retraite si pertes > | 50% | 50% | jamais |
| Composition d'armee | Infantry+Range | Equilibree | Adaptive |
| Multi-front | non | rarement | souvent |
| Bonus or (cheat) | 0% | 0% | +20% |

---

## Refactoring (dette technique)

### [REFACTO-01] Couplage singleton implicite dans Unit
`Unit.cs` appelle directement `NetworkSync.Instance` et `GameManager.Instance`. Impossible a tester en isolation.

**Solution a long terme :** Dependency injection via evenements/signaux.

**Fichiers :** `Scripts/Units/Unit.cs`, `Scripts/Units/Unit.Combat.cs`

---

### [REFACTO-02] Error handling dans les RPCs reseau
Si un `PackedScene` n'est pas trouve, l'erreur est silencieuse. Ajouter try/catch + logs explicites.

**Fichiers :** `Scripts/Network/NetworkSync.cs`

---

### [REFACTO-03] CampId deterministe base sur position (pas compteur statique)
`_nextCampId` statique peut desynchroniser les peers si l'ordre de `_Ready()` change.

**Solution :** `int campId = (int)(worldPosition.X * 1000 + worldPosition.Y) & 0x7FFFFFFF;`

**Fichiers :** `Scripts/Map/CampPlacer.cs`, `Scripts/Camps/CampSimple.cs`

---

## Checklist globale

### Bugs corriges
- [x] BUG-01 OnDefenderDied() — OwnerCamp + appel dans Die() + assignation dans spawn
- [x] BUG-02 Prix HUD — UpdatePriceLabels() dynamique depuis UnitStats
- [x] BUG-03 Support aura stacking — Mathf.Min(bonus, 40f)
- [x] BUG-04 Or perdu si capture — RefundProductionQueue() + RefundShipProductionQueue()
- [x] BUG-05 Feedback achat — UpdateUnitButtons/UpdateShipButtons + disabled + tooltips
- [x] BUG-06 Desync or multi — RpcSyncGold toutes les 10s, seuil 5 or
- [x] BUG-07 Deconnexion — overlay HUD + retour menu 5s (peer + serveur)
- [x] BUG-08 Blocage unites lentes — Mathf.Max(..., 0.5f) dans ProcessStuckDetection
- [x] BUG-09 Territoire non transfere a l'ennemi lors d'une capture — TerritoryManager.ApplyManualTiles() BFS + persistance dans _manualTiles pour le capteur, appel direct RefreshTerritory() depuis CaptureCamp/ApplyRemoteCapture
- [x] BUG-10 Port ennemi selectionnable sans filtre d'equipe — SelectionManager.SelectPort() verifie maintenant GetTeamId() == GetLocalTeamId()
- [x] BUG-11 Unites spawnees dans obstacles (forets, arbres, montagnes) — FindClearSpawnPosition() dans CampSimple.Production, CampPlacer refuse de placer un camp sur un objet existant
- [x] BUG-12 NetworkEntityRegistry peer ID null en mode solo — verif MultiplayerPeer != null avant GetUniqueId(), fallback peerId = 1

### Ameliorations a faire (priorite pre-rendu)
- [ ] [PR2-NAV-01] Forets/montagnes bloquantes
- [ ] [PR2-NAV-02] IA logique navale
- [ ] [PR2-IA-01] IA defense de ses camps
- [ ] [PR2-UX-01] Effet visuel Healer
- [ ] [PR2-UX-02] Effet visuel aura Support
- [ ] [PR2-UX-03] Sons de base
- [ ] [PR2-DOC-01] Doc architecture reseau
- [ ] [PR2-DOC-02] Doc algo IA
- [ ] [PR2-DOC-03] Guide multijoueur pas-a-pas

### Ameliorations realisees
- [x] Variation de tuiles FlipH/FlipV — TerrainGenerator.InitTileVariants() + PickAlt() hash deterministique, 4 variantes par source terrain, cartes procedurales et predefinies couvertes

### Ameliorations importantes
- [ ] [AMELIO-02] File de production visible dans HUD
- [ ] [AMELIO-03] Toast/notification UI
- [ ] [AMELIO-04] Barre de progression victoire
- [ ] [AMELIO-05] Confirmation quitter en multi
- [ ] [AMELIO-06] Persistance langue
- [ ] [AMELIO-07] IA defense camps
- [ ] [AMELIO-08] Refactoring GameManager (SRP)
- [ ] [AMELIO-09] Constantes centralisees (GameConfig.cs)
- [ ] [AMELIO-10] Distinction visuelle port vs camp

### Nice-to-have
- [ ] [AMELIO-11] Tooltips stats unites
- [ ] [AMELIO-12] Cache liste ennemis (perf)
- [ ] [AMELIO-13] Cache regions bonus (perf)
- [ ] [AMELIO-14] Indicateur bonus region UI
- [ ] [AMELIO-15] Sons et effets visuels
- [ ] [AMELIO-16] Sauvegarde de partie
- [ ] [AMELIO-17] Formation navale
- [ ] [AMELIO-18] Mini-tutoriel
- [ ] [AMELIO-19] Null-checks defensifs
- [ ] [AMELIO-20] Navigation clavier menus

### Refactoring
- [ ] [REFACTO-01] Couplage singleton dans Unit
- [ ] [REFACTO-02] Error handling RPCs reseau
- [ ] [REFACTO-03] CampId deterministe base sur position

### Audit IA — Bugs critiques (Sprint 1)
- [x] [IA-BUG-01] Double CheckRegionBonuses() — non-reproductible (methode absente du code actuel)
- [x] [IA-BUG-02] Healers exclus des attaquants — AIController.cs
- [x] [IA-BUG-03] GetAICenter() inclut les navires — non-reproductible (Ship n'herite pas de Unit)
- [x] [IA-BUG-04] FindIdleAIUnits() — IsInstanceValid + GetCurrentHealth() > 0 ajoutes
- [x] [IA-BUG-05] OwnerCamp = null apres capture — CampSimple.UpdateSpawnedUnitsTeam()

### Audit IA — Comportement de base (Sprint 2)
- [x] [IA-02-01] Timer de premier attaque (Easy=90s, Medium=45s, Hard=20s)
- [x] [IA-02-02] Delai de reaction par difficulte (Easy=4s, Medium=1.5s, Hard=0.5s)
- [x] [IA-02-03] Taux d'erreur de ciblage (Easy=35%, Medium=15%, Hard=0%)
- [x] [IA-02-04] Seuil de retraite si pertes >50% (Easy/Medium, AttackGroup tracking)
- [x] [IA-02-05] Defense reactive (FindThreatenedAICamp + HasEnemiesNearCamp)
- [x] [IA-02-06] Timer d'attaque minimum anti-turtling (Easy=120s, Medium=90s, Hard=60s)

### Audit IA — Qualite strategique (Sprint 3)
- [ ] [IA-03-01] Composition d'armee par role (35/35/30%)
- [ ] [IA-03-02] Adaptation economique (winning/losing state)
- [ ] [IA-03-03] Personnalites d'IA (Rusher / Tortue / Macro)
- [ ] [IA-03-04] Recalibrer scoring de cible (normaliser distances)

### Audit IA — Fonctionnalites avancees (Sprint 4)
- [ ] [IA-04-01] Multi-front pour Hard (2 cibles simultanees)
- [ ] [IA-04-02] Composition adaptative counter la compo ennemie
- [ ] [IA-04-03] AI Director leger (comeback mechanic)
- [ ] [IA-04-04] Faiblesse geographique intentionnelle Hard
