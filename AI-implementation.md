# Idées d'implémentation IA — SupKonQuest

## Problème actuel
En mode FFA avec plusieurs IA Easy, les combats entre bots sont des stalemates infinis : toutes les IAs ont le même niveau, aucune ne prend l'avantage. Le joueur n'est jamais forcé de s'engager au-delà de sa région.

---

## Idée 1 — IA hiérarchique : une "boss IA" parmi les bots ⭐

**Concept** : Sur les N territoires ennemis, attribuer 3/4 d'IAs Easy et 1 IA Medium ou Hard.
- Les Easy s'éliminent entre elles (combat chaotique, résultat aléatoire).
- La Hard survit naturellement, unifie le territoire ennemi, devient la menace finale.
- Cela force le joueur à traverser l'eau (gameplay naval) pour atteindre le vainqueur.

**Implémentation suggérée** :
- Dans `MapGenerator.InitAIController()`, trier les teamIds bots et attribuer Hard au dernier (ex. team la plus éloignée du joueur selon position de camp).
- Ou tirer aléatoirement le "boss" parmi les équipes bots.
- Paramètre en GameModeMenu : "IA Ennemie" = Facile / Moyen / Difficile → difficulté de la boss IA, les autres restent Easy.

---

## Idée 2 — IA navale

**Problème** : L'IA ne construit jamais de bateaux, elle est bloquée si le joueur occupe une île.

**Concept** :
- Si un camp ennemi est uniquement accessible par l'eau, l'IA commande des Transports.
- L'IA embarque ses unités idle dans un Transport disponible et les débarque près de la cible.
- Condition de déclenchement : `ChooseTarget()` retourne un camp sans chemin terrestre.

**Paramètres suggérés** :
- Easy : jamais de naval.
- Medium : naval si aucune cible terrestre depuis 30s.
- Hard : naval proactif dès qu'un camp côtier est détecté.

---

## Idée 3 — Personnalités IA distinctes

**Concept** : Plutôt que diff = vitesse de réaction, ajouter des archétypes comportementaux.

| Archétype | Comportement | Adapté à |
|-----------|-------------|----------|
| Rusher | Spam Infantry, attaque immédiate | Easy/Medium |
| Économiste | Accumule de l'or, achète Heavy/Tank | Medium/Hard |
| Défenseur | Garde tous ses camps, attaque peu | Easy |
| Expansionniste | Capture camps neutres en priorité | Medium |

Implémentation : ajouter un `enum AIPersonality` dans `AIController`, modifier `ScoreCamp()` et `PickUnitToBuy()` selon la personnalité.

---

## Idée 4 — Stagger des IA (démarrage décalé)

**Problème** : Toutes les IAs Easy attaquent en même temps au même endroit → embouteillage.

**Concept** : Ajouter un délai de démarrage aléatoire par équipe bot (`_gameStartDelay`).
- Team 2 : 0s de délai
- Team 3 : +5s à +15s aléatoire
- Team 4 : +10s à +25s aléatoire
- etc.

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

1. **Idée 1** (boss IA) — impact gameplay immédiat, peu de code
2. **Idée 4** (stagger) — fix du stalemate actuel, 5 lignes de code
3. **Idée 2** (naval) — complète le gameplay naval, plus complexe
4. **Idée 3** (personnalités) — refactor profond, pour plus tard
5. **Idées 5 & 6** — polish avancé
