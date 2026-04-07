# TODO - Code review approfondie (2026-04-06)

Format: `Priorite | Zone | Probleme | Action | Test de validation`

## P0 - Critique

- [ ] **P0 | Reseau/Economie (Relay)** | En mode Nakama relay, l'or est gere localement par joueur/slot sans sync globale forte. Les achats peuvent diverger selon le timing reseau. (`Scripts/Economy/GameManager.cs`, `Scripts/Network/NetworkSync.cs`, `Scripts/Network/NetworkCommandRouter.cs`) | Rester en modele relay client-driven, mais ajouter convergence explicite: sync d'or plus frequente, reconciliation d'etat sur des evenements cles (achat, capture, refund), et correction deterministe en cas d'ecart. | Lancer 2 clients relay, spam achats sur les deux joueurs pendant 3 minutes: ecart d'or <= seuil fixe et meme files de production a la fin.

- [ ] **P0 | Reseau/Robustesse commandes relay** | Les commandes `BuyUnit/MoveUnits/AttackCamp` sont appliquees sans ACK metier et avec protection limitee contre duplicate/out-of-order. (`Scripts/Network/NetworkCommandRouter.cs`) | Ajouter protocole robuste sans serveur autoritaire: idempotence par `(sender, sequence)`, ACK applicatif, retry limite, et replay-safe. | Rejouer paquets dupliques/hors ordre/perdus: aucune duplication d'achat ni ordre applique 2 fois.

- [ ] **P0 | Reconnexion multijoueur** | En cas de deconnexion, retour menu force apres 5s sans recovery. (`Scripts/Network/NetworkManager.cs`) | Implementer reconnexion/session resume (au minimum: tentative auto + ecran attente + abandon explicite). | Couper le reseau 3-5s puis revenir: partie recuperee ou flow de reprise clair sans perte silencieuse.

## P1 - Haute priorite

- [ ] **P1 | Economie/Balance** | Les constantes d'or (`PassiveGoldPerSecond=500`, `GoldPerSecond=500`) sont tres loin du game flow documente (5/s passif, 50/s camp). (`Scripts/Economy/GameManager.cs`, `Scripts/Camps/CampSimple.cs`, `CLAUDE.md`) | Aligner les constantes code + docs + UI design. Ajouter une source unique de configuration economie. | Test 60s en partie: revenu observe = revenu attendu (+ bonus region). 

- [ ] **P1 | Sync fonctionnalites Port** | L'achat/placement de port est local et non synchronise explicitement entre peers (etat `HasPort`, visuel port, droits de prod navale). (`Scripts/UI/GameHUD.cs`, `Scripts/Map/TerritoryManager.cs`, `Scripts/Camps/CampSimple.Naval.cs`, `Scripts/Network/*`) | Ajouter commandes/replication reseau pour `BuyPort` + `PlacePortAt` + rollback/remboursement, avec validation autoritaire. | Host construit un port: client voit immediatement le port et peut constater la production navale coherente.

- [ ] **P1 | Validation fonctionnelle des actions en relay** | Les commandes sont appliquees cote receveur avec peu de garde-fous gameplay (camp valide, ownership attendu, preconditions metier). (`Scripts/Network/NetworkCommandRouter.cs`) | Ajouter validations metier minimales (sans zero-trust): coherence joueur local, existence entites, preconditions de file/camp, logs de rejet. | Envoyer commandes sur etat obsolete (camp deja capture, unite detruite): commande ignoree proprement, pas de crash ni divergence.

## P2 - Moyenne priorite

- [ ] **P2 | Cohesion architecture multijoueur** | Deux modeles reseau coexistent (ENet RPC et Nakama relay) avec logiques partiellement dupliquees et comportements differents. (`Scripts/Network/*`, `Scripts/UI/GameHUD.cs`, `Scripts/Selection/SelectionManager.cs`) | Factoriser une couche de commande unique (interface transport), separer transport reseau et regles gameplay. | Les memes tests gameplay passent en ENet et en relay sans divergence de regles.

- [ ] **P2 | Performance recherche d'unites** | Plusieurs boucles globales `GetNodesInGroup` frequentes pour IA/combat/territoire, risque de cout CPU en late game. (`Scripts/Units/*`, `Scripts/Camps/CampSimple.Defense.cs`, `Scripts/Map/TerritoryManager.cs`) | Introduire index spatiaux simples (grille/hash), event-driven updates et budgets de scan. | Benchmark 1000+ unites: frametime stable, pas de pics > budget cible.

- [ ] **P2 | Robustesse registre reseau** | `NetworkEntityRegistry` statique sans garde de cycle de vie de scene (clear global, ids globaux, concurrence logique). (`Scripts/Network/NetworkEntityRegistry.cs`, `Scripts/Map/MapGenerator.cs`) | Encadrer reset/ownership par scene et ajouter garde anti-collision + checks de coherence. | 10 regenerations de map + parties consecutives sans entites fantomes ni collisions d'ID.

- [ ] **P2 | Qualite UX lobby** | `LobbyUI` melange authentification, pseudo, matchmaking, navigation scene, avec peu d'etats explicites. (`Scripts/UI/LobbyUI.cs`) | Passer a une machine d'etats UI simple (Idle/Auth/Ready/Matchmaking/Joining/Error). | Tests manuels des transitions (annulation, echec auth, retour menu) sans bouton bloque.

## P3 - Dette technique / qualite

- [ ] **P3 | Documentation vs code** | L'architecture documentee mentionne des elements absents (ex: `Scripts/AI/AIController.cs`) et certains chiffres gameplay divergent. (`CLAUDE.md`, arborescence `Scripts/`) | Mettre la doc a jour automatiquement a partir du code (checklist release). | CI ou script local detecte les ecarts doc/code.

- [ ] **P3 | Couverture de tests** | Pas de tests automatises visibles pour economie, capture, sync reseau et production queues. | Ajouter tests unitaires (formule degats, tiers, remboursements) + tests d'integration headless pour flux multijoueur critiques. | Suite de tests executable en CI, avec cas de non-regression des incidents connus.

## Hors perimetre court terme (assume)

- [ ] **N/A | Anti-triche avancee** | Pas de besoin immediat de protection contre client malveillant. | Differe jusqu'a decision produit d'introduire un backend autoritaire.
- [ ] **N/A | Modele serveur autoritaire complet** | Non prevu dans le futur proche. | Conserver l'architecture relay et investir dans convergence, UX reseau et resilience.

## Plan de verification minimal apres corrections

1. Build C# propre: `dotnet build SupKonQuest.csproj`
2. Scenario ENet 2 joueurs en FFA: achats, capture, port, deconnexion/reconnexion.
3. Scenario Nakama relay 2 joueurs en FFA: meme protocole + verif convergence etat (or, unites, camps, ports).
4. Stress test local: production massive + deplacements + combats continus 5 min.

