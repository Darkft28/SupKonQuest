const SupKonQuestModuleName = 'supkonquest_relay';
const SupKonQuestRpcFindMatchId = 'supkonquest.find_match';
const SupKonQuestMatchmakerGame = 'supkonquest';
const SupKonQuestMatchmakerMode = 'relay';
const SupKonQuestDefaultMaxPlayers = 2;
const SupKonQuestIdleSeconds = 30;

const LobbyMinPlayers = 2;
const LobbyMaxPlayers = 8;
const LobbyBaseSeconds = 20;
const LobbyJoinBonusSeconds = 5;
const LobbyMatchTickRate = 5;

const ValidShipTypes: {[key: string]: boolean} = {
    Transport: true,
    Fregate: true,
    Destroyer: true,
};

const ValidUnitTypes: {[key: string]: boolean} = {
    Infantry: true,
    Support: true,
    Range: true,
    Heal: true,
    AntiArmor: true,
    Mortar: true,
    Heavy: true,
    Tank: true,
};

enum RelayOpcode {
    BuyUnit = 1001,
    BuyShip = 1002,
    SpawnShip = 1003,
    BuildPort = 1004,
    SpawnUnit = 1005,
    MoveUnits = 2001,
    AttackCamp = 2002,
    CampCaptured = 2003,
    MoveShips = 2004,
    CampDamage = 2005,
    BoardTransport = 2006,
    TransportUnloaded = 2007,
    UnitsMoveToTransport = 2008,
    GoldSnapshot = 3001,
    PlayerLeaveCleanup = 5002,
    CastUltimate = 6001,
    UltimateVfx = 6002,
    UnitDamage = 7001,
    ShipDamage = 7002,
    EntityDied = 7003,
}

enum LobbyOpcode {
    LobbyTick = 4001,
    MatchStart = 4002,
}

type RelayCommandOpcode =
    | RelayOpcode.BuyUnit
    | RelayOpcode.BuyShip
    | RelayOpcode.SpawnShip
    | RelayOpcode.BuildPort
    | RelayOpcode.SpawnUnit
    | RelayOpcode.MoveUnits
    | RelayOpcode.AttackCamp
    | RelayOpcode.CampCaptured
    | RelayOpcode.MoveShips
    | RelayOpcode.CampDamage
    | RelayOpcode.BoardTransport
    | RelayOpcode.TransportUnloaded
    | RelayOpcode.UnitsMoveToTransport
    | RelayOpcode.GoldSnapshot
    | RelayOpcode.CastUltimate
    | RelayOpcode.UltimateVfx
    | RelayOpcode.UnitDamage
    | RelayOpcode.ShipDamage
    | RelayOpcode.EntityDied;

interface MatchLabel {
    game: string;
    mode: string;
    open: number;
    players: number;
    maxPlayers: number;
}

interface RelayMatchState {
    label: MatchLabel;
    emptyTicks: number;
    joinsInProgress: number;
    maxPlayers: number;
    players: {[userId: string]: nkruntime.Presence | null};
    lastSequenceByUser: {[userId: string]: number};
    lobbyActive: boolean;
    lobbySecondsRemaining: number;
    matchStarted: boolean;
    seed: number;
    mapType: number;
    lastLobbyBroadcastSecond: number;
    lastKnownPlayerCount: number;
    userToTeamId: {[userId: string]: number};
    cleanupAppliedByTeam: {[teamId: number]: boolean};
}

interface RpcFindMatchRequest {
    mode?: string;
    region?: string;
    maxPlayers?: number;
}

interface RpcFindMatchResponse {
    matchIds: string[];
    mode?: string;
    maxPlayers?: number;
    useMatchmaker?: boolean;
    nonAuthoritative?: boolean;
}

interface RelayCommandEnvelope {
    senderUserId: string;
    sequence: number;
}

interface RelayBuyUnitCommand extends RelayCommandEnvelope {
    campId: number;
    teamId: number;
    unitType: string;
}

interface RelayBuyShipCommand extends RelayCommandEnvelope {
    campId: number;
    teamId: number;
    shipType: string;
}

interface RelayMoveUnitsCommand extends RelayCommandEnvelope {
    unitIds: string[];
    startX: number;
    startY: number;
    targetX: number;
    targetY: number;
}

interface RelayMoveShipsCommand extends RelayCommandEnvelope {
    shipIds: string[];
    targetX: number;
    targetY: number;
    unloadRequested: boolean;
    unloadX: number;
    unloadY: number;
}

interface RelayUnitDamageCommand extends RelayCommandEnvelope {
    targetNetworkId: string;
    damage: number;
    attackerTeamId: number;
}

interface RelayShipDamageCommand extends RelayCommandEnvelope {
    targetNetworkId: string;
    damage: number;
    attackerTeamId: number;
}

interface RelayEntityDiedCommand extends RelayCommandEnvelope {
    networkId: string;
}

interface RelayAttackCampCommand extends RelayCommandEnvelope {
    unitIds: string[];
    campId: number;
}

interface RelayGoldSnapshotCommand extends RelayCommandEnvelope {
    teamId: number;
    gold: number;
    version: number;
    reason: string;
}

interface RelayCampCapturedCommand extends RelayCommandEnvelope {
    campId: number;
    newTeamId: number;
}

interface RelayCampDamageCommand extends RelayCommandEnvelope {
    campId: number;
    damage: number;
    attackerTeamId: number;
}

interface RelayBoardTransportCommand extends RelayCommandEnvelope {
    shipNetworkId: string;
    unitNetworkId: string;
    unitType: string;
    teamId: number;
    health: number;
}

interface RelayTransportUnloadedCommand extends RelayCommandEnvelope {
    shipNetworkId: string;
    unitNetworkIds: string[];
    unitTypes: string[];
    teamId: number;
    posXs: number[];
    posYs: number[];
    healths: number[];
}

interface RelayUnitsMoveToTransportCommand extends RelayCommandEnvelope {
    shipNetworkId: string;
    unitIds: string[];
}

interface RelaySpawnShipCommand extends RelayCommandEnvelope {
    networkId: string;
    shipType: string;
    teamId: number;
    posX: number;
    posY: number;
    health: number;
}

interface RelaySpawnUnitCommand extends RelayCommandEnvelope {
    networkId: string;
    unitType: string;
    teamId: number;
    campId: number;
    posX: number;
    posY: number;
    health: number;
}

interface RelayBuildPortCommand extends RelayCommandEnvelope {
    campId: number;
    teamId: number;
    posX: number;
    posY: number;
    rotation: number;
    flipH: boolean;
}

interface RelayCastUltimateCommand extends RelayCommandEnvelope {
    teamId: number;
    abilityId: string;
    targetX: number;
    targetY: number;
}

interface RelayUltimateVfxCommand extends RelayCommandEnvelope {
    teamId: number;
    abilityId: string;
    targetX: number;
    targetY: number;
}

type JsonObject = {[key: string]: unknown};

function parseJsonObject(payload: string | null | undefined): JsonObject | null {
    if (!payload) {
        return null;
    }

    try {
        const parsed = JSON.parse(payload);
        return isJsonObject(parsed) ? parsed : null;
    } catch {
        return null;
    }
}

function readField(parsed: JsonObject, camelKey: string, pascalKey: string): unknown {
    if (Object.prototype.hasOwnProperty.call(parsed, camelKey)) {
        return parsed[camelKey];
    }

    if (Object.prototype.hasOwnProperty.call(parsed, pascalKey)) {
        return parsed[pascalKey];
    }

    return undefined;
}

function isJsonObject(value: unknown): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(value: unknown, maxLength: number): string | null {
    if (typeof value !== 'string') {
        return null;
    }

    const trimmed = value.trim();
    if (trimmed.length === 0 || trimmed.length > maxLength) {
        return null;
    }

    return trimmed;
}

function readOptionalString(value: unknown, maxLength: number): string | null {
    if (value === undefined || value === null) {
        return '';
    }

    return readString(value, maxLength);
}

function readInteger(value: unknown, min: number, max: number): number | null {
    if (typeof value !== 'number' || !isFinite(value) || Math.floor(value) !== value) {
        return null;
    }

    if (value < min || value > max) {
        return null;
    }

    return value;
}

function readPositiveInteger(value: unknown, max: number): number | null {
    return readInteger(value, 1, max);
}

function readNonNegativeInteger(value: unknown, max: number): number | null {
    return readInteger(value, 0, max);
}

function readNumber(value: unknown): number | null {
    if (typeof value !== 'number' || !isFinite(value)) {
        return null;
    }

    return value;
}

function readStringArray(value: unknown, maxItems: number, maxLength: number): string[] | null {
    if (!Array.isArray(value) || value.length === 0 || value.length > maxItems) {
        return null;
    }

    const result: string[] = [];
    for (const item of value) {
        const normalized = readString(item, maxLength);
        if (normalized === null) {
            return null;
        }

        result.push(normalized);
    }

    return result;
}

function readFloatArray(value: unknown, expectedLength: number, maxItems: number): number[] | null {
    if (!Array.isArray(value) || value.length === 0 || value.length > maxItems) {
        return null;
    }
    if (expectedLength > 0 && value.length !== expectedLength) {
        return null;
    }

    const result: number[] = [];
    for (const item of value) {
        const normalized = readNumber(item);
        if (normalized === null) {
            return null;
        }
        result.push(normalized);
    }

    return result;
}

function readShipType(value: unknown): string | null {
    const shipType = readString(value, 32);
    if (shipType === null) {
        return null;
    }

    return ValidShipTypes[shipType] ? shipType : null;
}

function readUnitType(value: unknown): string | null {
    const unitType = readString(value, 32);
    if (unitType === null) {
        return null;
    }

    return ValidUnitTypes[unitType] ? unitType : null;
}

function buildMatchLabel(state: RelayMatchState): string {
    return JSON.stringify(state.label);
}

function createRelayLabel(playerCount: number, maxPlayers: number): MatchLabel {
    return {
        game: SupKonQuestMatchmakerGame,
        mode: SupKonQuestMatchmakerMode,
        open: playerCount < maxPlayers ? 1 : 0,
        players: playerCount,
        maxPlayers: maxPlayers,
    };
}

function createInitialRelayMatchState(maxPlayers: number, matchId: string): RelayMatchState {
    return {
        label: createRelayLabel(0, maxPlayers),
        emptyTicks: 0,
        joinsInProgress: 0,
        maxPlayers: maxPlayers,
        players: {},
        lastSequenceByUser: {},
        lobbyActive: false,
        lobbySecondsRemaining: 0,
        matchStarted: false,
        seed: generateSeedFromMatchId(matchId),
        mapType: Math.floor(Math.random() * 3),
        lastLobbyBroadcastSecond: -1,
        lastKnownPlayerCount: 0,
        userToTeamId: {},
        cleanupAppliedByTeam: {},
    };
}

function isRelayOpcode(opCode: number): opCode is RelayCommandOpcode {
    return opCode === RelayOpcode.BuyUnit ||
        opCode === RelayOpcode.BuyShip ||
        opCode === RelayOpcode.SpawnShip ||
        opCode === RelayOpcode.BuildPort ||
        opCode === RelayOpcode.SpawnUnit ||
        opCode === RelayOpcode.MoveUnits ||
        opCode === RelayOpcode.AttackCamp ||
        opCode === RelayOpcode.CampCaptured ||
        opCode === RelayOpcode.MoveShips ||
        opCode === RelayOpcode.CampDamage ||
        opCode === RelayOpcode.BoardTransport ||
        opCode === RelayOpcode.TransportUnloaded ||
        opCode === RelayOpcode.UnitsMoveToTransport ||
        opCode === RelayOpcode.GoldSnapshot ||
        opCode === RelayOpcode.CastUltimate ||
        opCode === RelayOpcode.UltimateVfx ||
        opCode === RelayOpcode.UnitDamage ||
        opCode === RelayOpcode.ShipDamage ||
        opCode === RelayOpcode.EntityDied;
}

function isLobbyOpcode(opCode: number): boolean {
    return opCode === LobbyOpcode.LobbyTick || opCode === LobbyOpcode.MatchStart;
}
