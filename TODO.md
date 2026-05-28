# TODO — backlog connu

La revue detaillee du 2026-04-06 a ete retiree. 

## Multijoueur (implemente)

- PvP 2-8 via Nakama, 1 camp/joueur, camps restants neutres, pas d'IA en ligne
- Lobby in-match : demarrage uniquement sur opcode relay `4002` (MatchStart) — module serveur requis
- Opcodes lobby : `4001` LobbyTick, `4002` MatchStart (voir README section Multijoueur)

## Bugs connus (audit 2026-04-05)

- Support aura stacking illimite (cap +40-50 recommande)
- Prix HUD des unites tier 3 possiblement desynchronises du `.tscn` vs `UnitStats.cs`
- Pas de reconnexion multijoueur apres deconnexion

