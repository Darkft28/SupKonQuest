# Idées d'implémentation IA — SupKonQuest

## Déjà implémenté (branche `feature/bateaux`)

- **Idée 2 — IA navale** : ports, production de navires, offensives Medium/Hard, ciblage terrestre via `TerritoryConnectivity`, placement de port IA sur tout le littoral du territoire (pas seulement au pied du camp).


## Déjà implémenté (branche `feature/ia-refonte`)

- **Idée 1 — IA hiérachique** : 1 boss par territoire pour éviter les stalemates entre ia

- **Idée 4 — IA stager** : décalage entre démarage de chaque ia pour éviter un match mirroir entre deux ia

## Idées encore ouvertes

- **Idée 3 — Personnalité IA**

- **Idée 5 — Escalade dynamique**

- **Idée 6 — Trève entre IA**

---

## Idée 1 — IA hiérarchique : une "boss IA" parmi les bots (implémenté)

**Concept** : Sur les N territoires ennemis, attribuer 3/4 d'IAs Easy et 1 IA Medium ou Hard.

- Les Easy s'éliminent entre elles (combat chaotique, résultat aléatoire).
- La Hard survit naturellement, unifie le territoire ennemi, devient la menace finale.
- Cela force le joueur à traverser l'eau (gameplay naval) pour atteindre le vainqueur.

**Implémentation suggérée** :

- Dans `MapGenerator.InitAIController()`, trier les teamIds bots et attribuer Hard au dernier (ex. slot le plus éloigné du joueur selon position de camp).
- Ou tirer aléatoirement le "boss" parmi les slots IA.
- Paramètre en GameModeMenu : "IA Ennemie" = Facile / Moyen / Difficile → difficulté de la boss IA, les autres restent Easy.

---

## Idée 2 — IA navale (implémenté)

Comportement actuel dans `AIController.cs` :


| Niveau | Ports / prod navale                       | Offensive navale                                             |
| ------ | ----------------------------------------- | ------------------------------------------------------------ |
| Easy   | Non                                       | Non                                                          |
| Medium | Si region d'origine entierement controlee | Si region d'origine complete                                 |
| Hard   | Des qu'une region entiere est controlee   | Oui ; 20% par tick avant home region complete (cooldown 30s) |


Ciblage terrestre : camps hors composant connexe terrestre ignores (`IsLandReachable`). Pas encore d'embarquement automatique des unites dans les Transports.

**Améliorations possibles** : embarquement/debarquement IA, Transports pour camps uniquement maritimes.

---

## Idée 3 — Personnalités IA distinctes

**Concept** : Plutôt que diff = vitesse de réaction, ajouter des archétypes comportementaux.


| Archétype      | Comportement                        | Adapté à    |
| -------------- | ----------------------------------- | ----------- |
| Rusher         | Spam Infantry, attaque immédiate    | Easy/Medium |
| Économiste     | Accumule de l'or, achète Heavy/Tank | Medium/Hard |
| Défenseur      | Garde tous ses camps, attaque peu   | Easy        |
| Expansionniste | Capture camps neutres en priorité   | Medium      |


**Implémentation** : ajouter un `enum AIPersonality` dans `AIController`, modifier `ScoreCamp()` et `PickUnitToBuy()` selon la personnalité.

---

## Idée 4 — Stagger des IA (démarrage décalé) (implémenté)

**Problème** : Toutes les IAs Easy attaquent en même temps au même endroit → embouteillage.

**Concept** : Ajouter un délai de démarrage aléatoire par slot bot (`_gameStartDelay`).


| Slot IA | Délai       |
| ------- | ----------- |
| Slot 2  | 0s          |
| Slot 3  | +5s à +15s  |
| Slot 4  | +10s à +25s |
| ...     | ...         |


Cela étale les conflits inter-IA et évite les stalemates parfaits.

---

## Idée 5 — Escalade dynamique selon les camps capturés

**Concept** : Chaque camp capturé par une IA augmente légèrement son `MaxUnits` et réduit son `TickInterval`.

- Récompense l'IA qui gagne → elle devient progressivement plus menaçante.
- Simule un empire qui grandit.

```csharp
// Exemple dans RunTick()
int campCount = GetAICamps().Count;
int effectiveMaxUnits = MaxUnits[_diffIdx] + (campCount - 1) * 4;
```

---

## Idée 6 — Trêve temporaire entre IAs

**Concept** : Si le joueur est dominant (>50% des camps), les IAs Easy s'ignorent temporairement pour attaquer le joueur ensemble.

- Détection : `GameManager.Instance.GetAllCamps().Count(c => c.GetTeamId() == 1) > totalCamps / 2`
- Les IAs redirigent leurs unités vers les camps du joueur pendant 30-60s.

---

## Priorité d'implémentation suggérée


| Priorité | Idée                          | Effort                  | Impact                           |
| -------- | ----------------------------- | ----------------------- | -------------------------------- |
| 1        | Idée 1 — Boss IA              | Faible                  | Impact gameplay immédiat         |
| 2        | Idée 4 — Stagger              | Très faible (~5 lignes) | Fix du stalemate actuel          |
| 3        | Idée 2 — Naval (embarquement) | Moyen                   | Complète le gameplay naval       |
| 4        | Idée 3 — Personnalités        | Élevé                   | Refactor profond, pour plus tard |
| 5        | Idées 5 & 6 — Polish          | Moyen                   | Finition avancée                 |


