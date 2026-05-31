## Module implémenté (`supkonquest_relay`)

Fichiers sources (compilés en `build/index.js` via `npm run build`) :


| Fichier            | Rôle                                                             |
| ------------------ | ---------------------------------------------------------------- |
| `main.ts`          | `InitModule` : RPC, `registerMatch`, `registerMatchmakerMatched` |
| `messages.ts`      | Constantes, opcodes, types, validation JSON                      |
| `lobby.ts`         | Countdown lobby authoritatif, `MatchStart`                       |
| `match_handler.ts` | Handlers match + relay gameplay                                  |
| `match_rpc.ts`     | RPC `supkonquest.find_match` (stub matchmaker)                   |


### Matchmaking

- Le client utilise `AddMatchmakerAsync` avec `properties.game=supkonquest` et `properties.mode=relay`.
- `onMatchmakerMatched` crée un match authoritatif : `nk.matchCreate('supkonquest_relay', { maxPlayers: '8' })`.
- Les clients rejoignent via `JoinMatchAsync` ; le lobby démarre à **2 joueurs** connectés.

### Opcodes lobby (serveur → clients uniquement)


| Opcode | Nom        | Payload JSON (camelCase)                                            |
| ------ | ---------- | ------------------------------------------------------------------- |
| `4001` | LobbyTick  | `{ secondsRemaining, playerCount }` — ~1/s pendant le lobby         |
| `4002` | MatchStart | `{ seed, orderedUserIds, matchId? }` — `orderedUserIds` tri ordinal |


Règles lobby :

- **20 s** à l’arrivée du 2e joueur (pas de +5 pour le 2e).
- **+5 s** au temps restant à chaque joueur supplémentaire (3e, 4e, …).
- Démarrage **immédiat** si **8** joueurs.
- Les commandes gameplay sont **rejetées** tant que `matchStarted` est false.
- Seed : même hash que `NakamaService.GenerateSeedFromMatchId` (à partir de `ctx.matchId`).

### Opcodes gameplay relay (clients ↔ serveur)


| Opcode | Nom                  | Validation serveur                                                     |
| ------ | -------------------- | ---------------------------------------------------------------------- |
| `1001` | BuyUnit              | envelope + campId/teamId/unitType                                      |
| `1002` | BuyShip              | envelope + campId/teamId + `Transport` | `Fregate` | `Destroyer`       |
| `1003` | SpawnShip            | envelope + networkId/shipType/team/position/health                     |
| `1004` | BuildPort            | envelope + campId/team/position/rotation/flip                          |
| `1005` | SpawnUnit            | envelope + networkId/unitType/team/campId/position/health              |
| `2001` | MoveUnits            | envelope + unitIds + start/target                                      |
| `2002` | AttackCamp           | envelope + unitIds + campId                                            |
| `2003` | CampCaptured         | envelope + campId/newTeamId                                            |
| `2004` | MoveShips            | envelope + shipIds + targetX/Y (sans start)                            |
| `2005` | CampDamage           | envelope + campId/damage/attackerTeamId                                |
| `2006` | BoardTransport       | envelope + shipNetworkId/unitNetworkId/unitType/teamId/health          |
| `2007` | TransportUnloaded    | envelope + shipNetworkId + tableaux unités (ids, types, positions, pv) |
| `2008` | UnitsMoveToTransport | envelope + shipNetworkId + unitIds                                     |
| `3001` | GoldSnapshot         | envelope + teamId/gold/version/reason                                  |
| `6001` | CastUltimate         | envelope + unitIds/abilityId/targetX/targetY                           |
| `7001` | UnitDamage           | envelope + targetNetworkId/damage/attackerTeamId                       |
| `7002` | ShipDamage           | envelope + targetNetworkId/damage/attackerTeamId                       |
| `7003` | EntityDied           | envelope + networkId                                                   |


### Opcodes reconnexion autoritaire (serveur → clients)


| Opcode | Nom                    | Payload JSON                                                       |
| ------ | ---------------------- | ------------------------------------------------------------------ |
| `5001` | ReconnectWindowStarted | `{ senderUserId: "server", sequence: 0, teamId, durationSeconds }` |
| `5002` | PlayerLeaveCleanup     | `{ senderUserId: "server", sequence: 0, teamId }`                  |
| `5003` | ReconnectRestored      | `{ senderUserId: "server", sequence: 0, teamId }`                  |


Séquence : `sequence` strictement croissante par `senderUserId` (envelope + relay) pour les commandes clients. Les broadcasts serveur système utilisent `senderUserId="server"` et `sequence=0`.

