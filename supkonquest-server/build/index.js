"use strict";
var SupKonQuestModuleName = 'supkonquest_relay';
var SupKonQuestRpcFindMatchId = 'supkonquest.find_match';
var SupKonQuestMatchmakerGame = 'supkonquest';
var SupKonQuestMatchmakerMode = 'relay';
var SupKonQuestDefaultMaxPlayers = 2;
var SupKonQuestIdleSeconds = 30;
var LobbyMinPlayers = 2;
var LobbyMaxPlayers = 8;
var LobbyBaseSeconds = 20;
var LobbyJoinBonusSeconds = 5;
var LobbyMatchTickRate = 5;
var ValidShipTypes = {
    Transport: true,
    Fregate: true,
    Destroyer: true,
};
var ValidUnitTypes = {
    Infantry: true,
    Support: true,
    Range: true,
    Heal: true,
    AntiArmor: true,
    Mortar: true,
    Heavy: true,
    Tank: true,
};
var RelayOpcode;
(function (RelayOpcode) {
    RelayOpcode[RelayOpcode["BuyUnit"] = 1001] = "BuyUnit";
    RelayOpcode[RelayOpcode["BuyShip"] = 1002] = "BuyShip";
    RelayOpcode[RelayOpcode["SpawnShip"] = 1003] = "SpawnShip";
    RelayOpcode[RelayOpcode["BuildPort"] = 1004] = "BuildPort";
    RelayOpcode[RelayOpcode["SpawnUnit"] = 1005] = "SpawnUnit";
    RelayOpcode[RelayOpcode["MoveUnits"] = 2001] = "MoveUnits";
    RelayOpcode[RelayOpcode["AttackCamp"] = 2002] = "AttackCamp";
    RelayOpcode[RelayOpcode["CampCaptured"] = 2003] = "CampCaptured";
    RelayOpcode[RelayOpcode["MoveShips"] = 2004] = "MoveShips";
    RelayOpcode[RelayOpcode["CampDamage"] = 2005] = "CampDamage";
    RelayOpcode[RelayOpcode["BoardTransport"] = 2006] = "BoardTransport";
    RelayOpcode[RelayOpcode["TransportUnloaded"] = 2007] = "TransportUnloaded";
    RelayOpcode[RelayOpcode["UnitsMoveToTransport"] = 2008] = "UnitsMoveToTransport";
    RelayOpcode[RelayOpcode["GoldSnapshot"] = 3001] = "GoldSnapshot";
    RelayOpcode[RelayOpcode["PlayerLeaveCleanup"] = 5002] = "PlayerLeaveCleanup";
    RelayOpcode[RelayOpcode["CastUltimate"] = 6001] = "CastUltimate";
    RelayOpcode[RelayOpcode["UltimateVfx"] = 6002] = "UltimateVfx";
    RelayOpcode[RelayOpcode["UnitDamage"] = 7001] = "UnitDamage";
    RelayOpcode[RelayOpcode["ShipDamage"] = 7002] = "ShipDamage";
    RelayOpcode[RelayOpcode["EntityDied"] = 7003] = "EntityDied";
})(RelayOpcode || (RelayOpcode = {}));
var LobbyOpcode;
(function (LobbyOpcode) {
    LobbyOpcode[LobbyOpcode["LobbyTick"] = 4001] = "LobbyTick";
    LobbyOpcode[LobbyOpcode["MatchStart"] = 4002] = "MatchStart";
})(LobbyOpcode || (LobbyOpcode = {}));
function parseJsonObject(payload) {
    if (!payload) {
        return null;
    }
    try {
        var parsed = JSON.parse(payload);
        return isJsonObject(parsed) ? parsed : null;
    }
    catch (_a) {
        return null;
    }
}
function readField(parsed, camelKey, pascalKey) {
    if (Object.prototype.hasOwnProperty.call(parsed, camelKey)) {
        return parsed[camelKey];
    }
    if (Object.prototype.hasOwnProperty.call(parsed, pascalKey)) {
        return parsed[pascalKey];
    }
    return undefined;
}
function isJsonObject(value) {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}
function readString(value, maxLength) {
    if (typeof value !== 'string') {
        return null;
    }
    var trimmed = value.trim();
    if (trimmed.length === 0 || trimmed.length > maxLength) {
        return null;
    }
    return trimmed;
}
function readOptionalString(value, maxLength) {
    if (value === undefined || value === null) {
        return '';
    }
    return readString(value, maxLength);
}
function readInteger(value, min, max) {
    if (typeof value !== 'number' || !isFinite(value) || Math.floor(value) !== value) {
        return null;
    }
    if (value < min || value > max) {
        return null;
    }
    return value;
}
function readPositiveInteger(value, max) {
    return readInteger(value, 1, max);
}
function readNonNegativeInteger(value, max) {
    return readInteger(value, 0, max);
}
function readNumber(value) {
    if (typeof value !== 'number' || !isFinite(value)) {
        return null;
    }
    return value;
}
function readStringArray(value, maxItems, maxLength) {
    if (!Array.isArray(value) || value.length === 0 || value.length > maxItems) {
        return null;
    }
    var result = [];
    for (var _i = 0, value_1 = value; _i < value_1.length; _i++) {
        var item = value_1[_i];
        var normalized = readString(item, maxLength);
        if (normalized === null) {
            return null;
        }
        result.push(normalized);
    }
    return result;
}
function readFloatArray(value, expectedLength, maxItems) {
    if (!Array.isArray(value) || value.length === 0 || value.length > maxItems) {
        return null;
    }
    if (expectedLength > 0 && value.length !== expectedLength) {
        return null;
    }
    var result = [];
    for (var _i = 0, value_2 = value; _i < value_2.length; _i++) {
        var item = value_2[_i];
        var normalized = readNumber(item);
        if (normalized === null) {
            return null;
        }
        result.push(normalized);
    }
    return result;
}
function readShipType(value) {
    var shipType = readString(value, 32);
    if (shipType === null) {
        return null;
    }
    return ValidShipTypes[shipType] ? shipType : null;
}
function readUnitType(value) {
    var unitType = readString(value, 32);
    if (unitType === null) {
        return null;
    }
    return ValidUnitTypes[unitType] ? unitType : null;
}
function buildMatchLabel(state) {
    return JSON.stringify(state.label);
}
function createRelayLabel(playerCount, maxPlayers) {
    return {
        game: SupKonQuestMatchmakerGame,
        mode: SupKonQuestMatchmakerMode,
        open: playerCount < maxPlayers ? 1 : 0,
        players: playerCount,
        maxPlayers: maxPlayers,
    };
}
function createInitialRelayMatchState(maxPlayers, matchId) {
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
function isRelayOpcode(opCode) {
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
function isLobbyOpcode(opCode) {
    return opCode === LobbyOpcode.LobbyTick || opCode === LobbyOpcode.MatchStart;
}
function generateSeedFromMatchId(matchId) {
    var hash = 17;
    for (var i = 0; i < matchId.length; i++) {
        hash = hash * 31 + matchId.charCodeAt(i);
    }
    return hash & 0x7fffffff;
}
function getConnectedUserIds(state) {
    var userIds = [];
    for (var _i = 0, _a = Object.keys(state.players); _i < _a.length; _i++) {
        var userId = _a[_i];
        if (state.players[userId] !== null) {
            userIds.push(userId);
        }
    }
    userIds.sort(function (a, b) {
        if (a < b) {
            return -1;
        }
        if (a > b) {
            return 1;
        }
        return 0;
    });
    return userIds;
}
function getLobbyPlayerCount(state) {
    return getConnectedUserIds(state).length;
}
function broadcastLobbyTick(dispatcher, state) {
    var playerCount = getLobbyPlayerCount(state);
    var secondsRemaining = Math.max(0, Math.ceil(state.lobbySecondsRemaining));
    var payload = JSON.stringify({
        secondsRemaining: secondsRemaining,
        playerCount: playerCount,
    });
    dispatcher.broadcastMessage(LobbyOpcode.LobbyTick, payload);
}
function startMatch(ctx, logger, dispatcher, state) {
    if (state.matchStarted) {
        return;
    }
    var orderedUserIds = getConnectedUserIds(state);
    if (orderedUserIds.length < LobbyMinPlayers) {
        return;
    }
    state.matchStarted = true;
    state.lobbyActive = false;
    state.lobbySecondsRemaining = 0;
    state.userToTeamId = {};
    for (var i = 0; i < orderedUserIds.length; i++) {
        state.userToTeamId[orderedUserIds[i]] = i + 1;
    }
    var payload = JSON.stringify({
        seed: state.seed,
        orderedUserIds: orderedUserIds,
        matchId: ctx.matchId,
        mapType: state.mapType,
    });
    dispatcher.broadcastMessage(LobbyOpcode.MatchStart, payload);
    logger.info('SupKonQuest match %s started with %d players (seed=%d, mapType=%d).', ctx.matchId, orderedUserIds.length, state.seed, state.mapType);
}
function onLobbyPlayerCountChanged(ctx, logger, dispatcher, state, previousCount, newCount) {
    if (state.matchStarted) {
        state.lastKnownPlayerCount = newCount;
        return;
    }
    state.lastKnownPlayerCount = newCount;
    if (newCount < LobbyMinPlayers) {
        state.lobbyActive = false;
        state.lobbySecondsRemaining = 0;
        state.lastLobbyBroadcastSecond = -1;
        return;
    }
    if (previousCount < LobbyMinPlayers && newCount >= LobbyMinPlayers) {
        state.lobbyActive = true;
        state.lobbySecondsRemaining = LobbyBaseSeconds;
        state.lastLobbyBroadcastSecond = -1;
        broadcastLobbyTick(dispatcher, state);
        logger.info('SupKonQuest match %s lobby countdown started (%ds).', ctx.matchId, LobbyBaseSeconds);
    }
    else if (newCount > previousCount && previousCount >= LobbyMinPlayers) {
        state.lobbySecondsRemaining += LobbyJoinBonusSeconds;
        broadcastLobbyTick(dispatcher, state);
        logger.info('SupKonQuest match %s lobby extended by %ds (players=%d, remaining=%.1fs).', ctx.matchId, LobbyJoinBonusSeconds, newCount, state.lobbySecondsRemaining);
    }
    if (newCount >= LobbyMaxPlayers) {
        startMatch(ctx, logger, dispatcher, state);
    }
}
function tickLobby(ctx, logger, dispatcher, state) {
    if (state.matchStarted || !state.lobbyActive) {
        return;
    }
    var playerCount = getLobbyPlayerCount(state);
    if (playerCount < LobbyMinPlayers) {
        state.lobbyActive = false;
        state.lobbySecondsRemaining = 0;
        state.lastLobbyBroadcastSecond = -1;
        return;
    }
    state.lobbySecondsRemaining -= 1 / LobbyMatchTickRate;
    var broadcastSecond = Math.max(0, Math.ceil(state.lobbySecondsRemaining));
    if (broadcastSecond !== state.lastLobbyBroadcastSecond) {
        state.lastLobbyBroadcastSecond = broadcastSecond;
        broadcastLobbyTick(dispatcher, state);
    }
    if (state.lobbySecondsRemaining <= 0) {
        startMatch(ctx, logger, dispatcher, state);
    }
}
var matchInit = function (ctx, logger, nk, params) {
    var maxPlayers = resolveMaxPlayers(params['maxPlayers']);
    var state = createInitialRelayMatchState(maxPlayers, ctx.matchId || '');
    logger.info('SupKonQuest relay match initialized (maxPlayers=%d, seed=%d).', maxPlayers, state.seed);
    return {
        state: state,
        tickRate: LobbyMatchTickRate,
        label: buildMatchLabel(state),
    };
};
var matchJoinAttempt = function (ctx, logger, nk, dispatcher, tick, state, presence, metadata) {
    var existingPresence = state.players[presence.userId];
    if (existingPresence !== undefined) {
        if (existingPresence === null) {
            state.joinsInProgress++;
            return {
                state: state,
                accept: true,
            };
        }
        return {
            state: state,
            accept: false,
            rejectMessage: 'already joined',
        };
    }
    if (connectedPlayerCount(state) + state.joinsInProgress >= state.maxPlayers) {
        return {
            state: state,
            accept: false,
            rejectMessage: 'match full',
        };
    }
    state.joinsInProgress++;
    return {
        state: state,
        accept: true,
    };
};
var matchJoin = function (ctx, logger, nk, dispatcher, tick, state, presences) {
    var _a;
    var previousCount = getLobbyPlayerCount(state);
    for (var _i = 0, presences_1 = presences; _i < presences_1.length; _i++) {
        var presence = presences_1[_i];
        state.players[presence.userId] = presence;
        state.lastSequenceByUser[presence.userId] = (_a = state.lastSequenceByUser[presence.userId]) !== null && _a !== void 0 ? _a : 0;
        if (state.joinsInProgress > 0) {
            state.joinsInProgress--;
        }
    }
    updateMatchLabel(dispatcher, state);
    onLobbyPlayerCountChanged(ctx, logger, dispatcher, state, previousCount, getLobbyPlayerCount(state));
    return { state: state };
};
var matchLeave = function (ctx, logger, nk, dispatcher, tick, state, presences) {
    var previousCount = getLobbyPlayerCount(state);
    for (var _i = 0, presences_2 = presences; _i < presences_2.length; _i++) {
        var presence = presences_2[_i];
        state.players[presence.userId] = null;
        logger.info('Player %s left match %s.', presence.userId, ctx.matchId);
        if (!state.matchStarted) {
            continue;
        }
        var teamId = state.userToTeamId[presence.userId];
        if (teamId === undefined || state.cleanupAppliedByTeam[teamId] === true) {
            continue;
        }
        state.cleanupAppliedByTeam[teamId] = true;
        broadcastPlayerLeaveCleanup(dispatcher, teamId);
    }
    updateMatchLabel(dispatcher, state);
    onLobbyPlayerCountChanged(ctx, logger, dispatcher, state, previousCount, getLobbyPlayerCount(state));
    return { state: state };
};
var matchLoop = function (ctx, logger, nk, dispatcher, tick, state, messages) {
    var activePlayers = connectedPlayerCount(state);
    if (activePlayers === 0 && state.joinsInProgress === 0) {
        state.emptyTicks++;
        if (state.emptyTicks >= SupKonQuestIdleSeconds * LobbyMatchTickRate) {
            logger.info('Closing idle SupKonQuest relay match %s.', ctx.matchId);
            return null;
        }
    }
    else {
        state.emptyTicks = 0;
    }
    tickLobby(ctx, logger, dispatcher, state);
    for (var _i = 0, messages_1 = messages; _i < messages_1.length; _i++) {
        var message = messages_1[_i];
        if (isLobbyOpcode(message.opCode)) {
            continue;
        }
        if (!isRelayOpcode(message.opCode)) {
            logger.debug('Ignoring unexpected opcode %d from %s in match %s.', message.opCode, message.sender.userId, ctx.matchId);
            continue;
        }
        if (!state.matchStarted) {
            logger.debug('Rejected relay command from %s: match not started.', message.sender.userId);
            continue;
        }
        var payload = nk.binaryToString(message.data);
        var parsed = parseJsonObject(payload);
        if (parsed === null) {
            logger.debug('Rejected relay payload: invalid JSON for opcode %d from %s.', message.opCode, message.sender.userId);
            continue;
        }
        var envelope = validateRelayEnvelope(parsed, state, message.sender.userId);
        if (envelope === null) {
            logger.debug('Rejected relay command from %s because the envelope is invalid or out of order.', message.sender.userId);
            continue;
        }
        var accepted = false;
        switch (message.opCode) {
            case RelayOpcode.BuyUnit:
                accepted = validateBuyUnitCommand(parsed) !== null;
                break;
            case RelayOpcode.BuyShip:
                accepted = validateBuyShipCommand(parsed) !== null;
                break;
            case RelayOpcode.SpawnShip:
                accepted = validateSpawnShipCommand(parsed) !== null;
                break;
            case RelayOpcode.SpawnUnit:
                accepted = validateSpawnUnitCommand(parsed) !== null;
                break;
            case RelayOpcode.BuildPort:
                accepted = validateBuildPortCommand(parsed) !== null;
                break;
            case RelayOpcode.CampCaptured:
                accepted = validateCampCapturedCommand(parsed) !== null;
                break;
            case RelayOpcode.CampDamage:
                accepted = validateCampDamageCommand(parsed) !== null;
                break;
            case RelayOpcode.UnitsMoveToTransport:
                accepted = validateUnitsMoveToTransportCommand(parsed) !== null;
                break;
            case RelayOpcode.BoardTransport:
                accepted = validateBoardTransportCommand(parsed) !== null;
                break;
            case RelayOpcode.TransportUnloaded:
                accepted = validateTransportUnloadedCommand(parsed) !== null;
                break;
            case RelayOpcode.MoveUnits:
                accepted = validateMoveUnitsCommand(parsed) !== null;
                break;
            case RelayOpcode.MoveShips:
                accepted = validateMoveShipsCommand(parsed) !== null;
                break;
            case RelayOpcode.AttackCamp:
                accepted = validateAttackCampCommand(parsed) !== null;
                break;
            case RelayOpcode.GoldSnapshot:
                accepted = validateGoldSnapshotCommand(parsed) !== null;
                break;
            case RelayOpcode.CastUltimate:
                accepted = validateCastUltimateCommand(parsed) !== null;
                break;
            case RelayOpcode.UltimateVfx:
                accepted = validateUltimateVfxCommand(parsed) !== null;
                break;
            case RelayOpcode.UnitDamage:
                accepted = validateUnitDamageCommand(parsed) !== null;
                break;
            case RelayOpcode.ShipDamage:
                accepted = validateShipDamageCommand(parsed) !== null;
                break;
            case RelayOpcode.EntityDied:
                accepted = validateEntityDiedCommand(parsed) !== null;
                break;
        }
        if (!accepted) {
            logger.debug('Rejected relay command from %s for opcode %d.', message.sender.userId, message.opCode);
            continue;
        }
        state.lastSequenceByUser[envelope.senderUserId] = envelope.sequence;
        dispatcher.broadcastMessage(message.opCode, payload);
    }
    return { state: state };
};
var matchTerminate = function (ctx, logger, nk, dispatcher, tick, state, graceSeconds) {
    return { state: state };
};
var matchSignal = function (ctx, logger, nk, dispatcher, tick, state, data) {
    return {
        state: state,
        data: data,
    };
};
function resolveMaxPlayers(rawValue) {
    if (!rawValue) {
        return LobbyMaxPlayers;
    }
    var parsed = parseInt(rawValue, 10);
    if (!isFinite(parsed) || parsed < LobbyMinPlayers) {
        return LobbyMaxPlayers;
    }
    return parsed > LobbyMaxPlayers ? LobbyMaxPlayers : parsed;
}
function connectedPlayerCount(state) {
    return getLobbyPlayerCount(state);
}
function updateMatchLabel(dispatcher, state) {
    state.label = createRelayLabel(connectedPlayerCount(state), state.maxPlayers);
    dispatcher.matchLabelUpdate(buildMatchLabel(state));
}
function broadcastPlayerLeaveCleanup(dispatcher, teamId) {
    var payload = JSON.stringify({
        senderUserId: 'server',
        sequence: 0,
        teamId: teamId,
    });
    dispatcher.broadcastMessage(RelayOpcode.PlayerLeaveCleanup, payload);
}
function validateRelayEnvelope(parsed, state, senderUserId) {
    var _a;
    var envelopeUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    if (envelopeUserId === null || sequence === null) {
        return null;
    }
    if (envelopeUserId !== senderUserId) {
        return null;
    }
    if (state.players[senderUserId] === undefined || state.players[senderUserId] === null) {
        return null;
    }
    var lastSequence = (_a = state.lastSequenceByUser[senderUserId]) !== null && _a !== void 0 ? _a : 0;
    if (sequence !== lastSequence + 1) {
        return null;
    }
    return {
        senderUserId: envelopeUserId,
        sequence: sequence,
    };
}
function validateBuyUnitCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var unitType = readString(readField(parsed, 'unitType', 'UnitType'), 32);
    if (senderUserId === null || sequence === null || campId === null || teamId === null || unitType === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        campId: campId,
        teamId: teamId,
        unitType: unitType,
    };
}
function validateBuyShipCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var shipType = readShipType(readField(parsed, 'shipType', 'ShipType'));
    if (senderUserId === null || sequence === null || campId === null || teamId === null || shipType === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        campId: campId,
        teamId: teamId,
        shipType: shipType,
    };
}
function validateMoveUnitsCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var unitIds = readStringArray(readField(parsed, 'unitIds', 'UnitIds'), 128, 64);
    var startX = readNumber(readField(parsed, 'startX', 'StartX'));
    var startY = readNumber(readField(parsed, 'startY', 'StartY'));
    var targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
    var targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
    if (senderUserId === null || sequence === null || unitIds === null || startX === null || startY === null || targetX === null || targetY === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        unitIds: unitIds,
        startX: startX,
        startY: startY,
        targetX: targetX,
        targetY: targetY,
    };
}
function validateMoveShipsCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var shipIds = readStringArray(readField(parsed, 'shipIds', 'ShipIds'), 128, 64);
    var targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
    var targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
    var unloadRequested = readField(parsed, 'unloadRequested', 'UnloadRequested');
    var unloadX = readNumber(readField(parsed, 'unloadX', 'UnloadX'));
    var unloadY = readNumber(readField(parsed, 'unloadY', 'UnloadY'));
    if (senderUserId === null || sequence === null || shipIds === null || targetX === null || targetY === null) {
        return null;
    }
    if (unloadRequested !== undefined && unloadRequested !== null && typeof unloadRequested !== 'boolean') {
        return null;
    }
    if (unloadRequested === true && (unloadX === null || unloadY === null)) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        shipIds: shipIds,
        targetX: targetX,
        targetY: targetY,
        unloadRequested: unloadRequested === true,
        unloadX: unloadX !== null && unloadX !== void 0 ? unloadX : 0,
        unloadY: unloadY !== null && unloadY !== void 0 ? unloadY : 0,
    };
}
function validateUnitDamageCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var targetNetworkId = readString(readField(parsed, 'targetNetworkId', 'TargetNetworkId'), 64);
    var damage = readNumber(readField(parsed, 'damage', 'Damage'));
    var attackerTeamId = readPositiveInteger(readField(parsed, 'attackerTeamId', 'AttackerTeamId'), 2147483647);
    if (senderUserId === null || sequence === null || targetNetworkId === null || damage === null || attackerTeamId === null) {
        return null;
    }
    if (damage <= 0) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        targetNetworkId: targetNetworkId,
        damage: damage,
        attackerTeamId: attackerTeamId,
    };
}
function validateShipDamageCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var targetNetworkId = readString(readField(parsed, 'targetNetworkId', 'TargetNetworkId'), 64);
    var damage = readNumber(readField(parsed, 'damage', 'Damage'));
    var attackerTeamId = readPositiveInteger(readField(parsed, 'attackerTeamId', 'AttackerTeamId'), 2147483647);
    if (senderUserId === null || sequence === null || targetNetworkId === null || damage === null || attackerTeamId === null) {
        return null;
    }
    if (damage <= 0) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        targetNetworkId: targetNetworkId,
        damage: damage,
        attackerTeamId: attackerTeamId,
    };
}
function validateEntityDiedCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var networkId = readString(readField(parsed, 'networkId', 'NetworkId'), 64);
    if (senderUserId === null || sequence === null || networkId === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        networkId: networkId,
    };
}
function validateAttackCampCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var unitIds = readStringArray(readField(parsed, 'unitIds', 'UnitIds'), 128, 64);
    var campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
    if (senderUserId === null || sequence === null || unitIds === null || campId === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        unitIds: unitIds,
        campId: campId,
    };
}
function validateCampCapturedCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
    var newTeamId = readPositiveInteger(readField(parsed, 'newTeamId', 'NewTeamId'), 2147483647);
    if (senderUserId === null || sequence === null || campId === null || newTeamId === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        campId: campId,
        newTeamId: newTeamId,
    };
}
function validateUnitsMoveToTransportCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var shipNetworkId = readString(readField(parsed, 'shipNetworkId', 'ShipNetworkId'), 64);
    var unitIds = readStringArray(readField(parsed, 'unitIds', 'UnitIds'), 32, 64);
    if (senderUserId === null || sequence === null || shipNetworkId === null || unitIds === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        shipNetworkId: shipNetworkId,
        unitIds: unitIds,
    };
}
function validateBoardTransportCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var shipNetworkId = readString(readField(parsed, 'shipNetworkId', 'ShipNetworkId'), 64);
    var unitNetworkId = readString(readField(parsed, 'unitNetworkId', 'UnitNetworkId'), 64);
    var unitType = readUnitType(readField(parsed, 'unitType', 'UnitType'));
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var health = readNumber(readField(parsed, 'health', 'Health'));
    if (senderUserId === null || sequence === null || shipNetworkId === null || unitNetworkId === null || unitType === null || teamId === null || health === null) {
        return null;
    }
    if (health <= 0) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        shipNetworkId: shipNetworkId,
        unitNetworkId: unitNetworkId,
        unitType: unitType,
        teamId: teamId,
        health: health,
    };
}
function validateTransportUnloadedCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var shipNetworkId = readString(readField(parsed, 'shipNetworkId', 'ShipNetworkId'), 64);
    var unitNetworkIds = readStringArray(readField(parsed, 'unitNetworkIds', 'UnitNetworkIds'), 32, 64);
    var unitTypesRaw = readField(parsed, 'unitTypes', 'UnitTypes');
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    if (senderUserId === null || sequence === null || shipNetworkId === null || unitNetworkIds === null || teamId === null) {
        return null;
    }
    var count = unitNetworkIds.length;
    var unitTypes = [];
    if (!Array.isArray(unitTypesRaw) || unitTypesRaw.length !== count) {
        return null;
    }
    for (var _i = 0, unitTypesRaw_1 = unitTypesRaw; _i < unitTypesRaw_1.length; _i++) {
        var item = unitTypesRaw_1[_i];
        var unitType = readUnitType(item);
        if (unitType === null) {
            return null;
        }
        unitTypes.push(unitType);
    }
    var posXs = readFloatArray(readField(parsed, 'posXs', 'PosXs'), count, 32);
    var posYs = readFloatArray(readField(parsed, 'posYs', 'PosYs'), count, 32);
    var healths = readFloatArray(readField(parsed, 'healths', 'Healths'), count, 32);
    if (posXs === null || posYs === null || healths === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        shipNetworkId: shipNetworkId,
        unitNetworkIds: unitNetworkIds,
        unitTypes: unitTypes,
        teamId: teamId,
        posXs: posXs,
        posYs: posYs,
        healths: healths,
    };
}
function validateCampDamageCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
    var damage = readNumber(readField(parsed, 'damage', 'Damage'));
    var attackerTeamId = readPositiveInteger(readField(parsed, 'attackerTeamId', 'AttackerTeamId'), 2147483647);
    if (senderUserId === null || sequence === null || campId === null || damage === null || attackerTeamId === null) {
        return null;
    }
    if (damage <= 0) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        campId: campId,
        damage: damage,
        attackerTeamId: attackerTeamId,
    };
}
function validateSpawnShipCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var networkId = readString(readField(parsed, 'networkId', 'NetworkId'), 64);
    var shipType = readShipType(readField(parsed, 'shipType', 'ShipType'));
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var posX = readNumber(readField(parsed, 'posX', 'PosX'));
    var posY = readNumber(readField(parsed, 'posY', 'PosY'));
    var health = readNumber(readField(parsed, 'health', 'Health'));
    if (senderUserId === null || sequence === null || networkId === null || shipType === null || teamId === null || posX === null || posY === null || health === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        networkId: networkId,
        shipType: shipType,
        teamId: teamId,
        posX: posX,
        posY: posY,
        health: health,
    };
}
function validateSpawnUnitCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var networkId = readString(readField(parsed, 'networkId', 'NetworkId'), 64);
    var unitType = readUnitType(readField(parsed, 'unitType', 'UnitType'));
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
    var posX = readNumber(readField(parsed, 'posX', 'PosX'));
    var posY = readNumber(readField(parsed, 'posY', 'PosY'));
    var health = readNumber(readField(parsed, 'health', 'Health'));
    if (senderUserId === null || sequence === null || networkId === null || unitType === null || teamId === null || campId === null || posX === null || posY === null || health === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        networkId: networkId,
        unitType: unitType,
        teamId: teamId,
        campId: campId,
        posX: posX,
        posY: posY,
        health: health,
    };
}
function validateBuildPortCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var posX = readNumber(readField(parsed, 'posX', 'PosX'));
    var posY = readNumber(readField(parsed, 'posY', 'PosY'));
    var rotation = readNumber(readField(parsed, 'rotation', 'Rotation'));
    var flipH = readField(parsed, 'flipH', 'FlipH');
    if (senderUserId === null || sequence === null || campId === null || teamId === null || posX === null || posY === null || rotation === null) {
        return null;
    }
    if (typeof flipH !== 'boolean') {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        campId: campId,
        teamId: teamId,
        posX: posX,
        posY: posY,
        rotation: rotation,
        flipH: flipH,
    };
}
function validateGoldSnapshotCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var gold = readNonNegativeInteger(readField(parsed, 'gold', 'Gold'), 2147483647);
    var version = readNonNegativeInteger(readField(parsed, 'version', 'Version'), 2147483647);
    var reason = readOptionalString(readField(parsed, 'reason', 'Reason'), 64);
    if (senderUserId === null || sequence === null || teamId === null || gold === null || version === null || reason === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        teamId: teamId,
        gold: gold,
        version: version,
        reason: reason,
    };
}
function validateCastUltimateCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var abilityId = readString(readField(parsed, 'abilityId', 'AbilityId'), 64);
    var targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
    var targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
    if (senderUserId === null || sequence === null || teamId === null || abilityId === null || targetX === null || targetY === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        teamId: teamId,
        abilityId: abilityId,
        targetX: targetX,
        targetY: targetY,
    };
}
function validateUltimateVfxCommand(parsed) {
    var senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
    var sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
    var teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
    var abilityId = readString(readField(parsed, 'abilityId', 'AbilityId'), 64);
    var targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
    var targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
    if (senderUserId === null || sequence === null || teamId === null || abilityId === null || targetX === null || targetY === null) {
        return null;
    }
    return {
        senderUserId: senderUserId,
        sequence: sequence,
        teamId: teamId,
        abilityId: abilityId,
        targetX: targetX,
        targetY: targetY,
    };
}
var rpcFindMatch = function (ctx, logger, nk, payload) {
    if (!ctx.userId) {
        throw Error('No user ID in context');
    }
    var request = parseJsonObject(payload);
    if (!payload || payload.trim().length === 0) {
        request = {};
    }
    if (request === null) {
        throw Error('Invalid payload. Expected a JSON object.');
    }
    var requestedMode = readString(request['mode'], 32);
    if (requestedMode !== null && requestedMode !== SupKonQuestMatchmakerMode) {
        throw Error('Unsupported mode.');
    }
    var requestedMaxPlayers = readOptionalInteger(request['maxPlayers'], 8);
    var maxPlayers = requestedMaxPlayers === null ? SupKonQuestDefaultMaxPlayers : Math.max(2, requestedMaxPlayers);
    return JSON.stringify({
        matchIds: [],
        mode: SupKonQuestMatchmakerMode,
        maxPlayers: maxPlayers,
        useMatchmaker: true,
        nonAuthoritative: true,
    });
};
function readOptionalInteger(value, max) {
    if (value === undefined || value === null) {
        return null;
    }
    return readInteger(value, 2, max);
}
var AuthMinPasswordLength = 8;
var AuthUsernameMinLength = 3;
var AuthUsernameMaxLength = 16;
function normalizeUsername(username) {
    if (!username) {
        return '';
    }
    var normalized = username.trim();
    var result = '';
    for (var i = 0; i < normalized.length; i++) {
        var c = normalized.charAt(i);
        if (/[a-zA-Z0-9_-]/.test(c)) {
            result += c;
        }
    }
    if (result.length > AuthUsernameMaxLength) {
        result = result.substring(0, AuthUsernameMaxLength);
    }
    return result;
}
function validateEmailAuth(email, password, username, create) {
    if (!password || password.length < AuthMinPasswordLength) {
        return 'Password must be at least ' + AuthMinPasswordLength + ' characters.';
    }
    if (!email || email.length < 10 || email.length > 255 || email.indexOf('@') < 0) {
        return 'Invalid email address.';
    }
    if (!create) {
        return null;
    }
    var normalized = normalizeUsername(username);
    if (normalized.length < AuthUsernameMinLength) {
        return 'Username must be ' + AuthUsernameMinLength + '-' + AuthUsernameMaxLength + ' characters (letters, digits, _ or -).';
    }
    return null;
}
function beforeAuthenticateEmail(ctx, logger, nk, request) {
    var email = request.account && request.account.email ? request.account.email : '';
    var password = request.account && request.account.password ? request.account.password : '';
    var username = request.username ? request.username : '';
    var create = request.create === true;
    var error = validateEmailAuth(email, password, username, create);
    if (error) {
        throw error;
    }
    if (create) {
        request.username = normalizeUsername(username);
    }
    return request;
}
function afterAuthenticateEmail(ctx, logger, nk, session, request) {
    var action = request.create ? 'register' : 'login';
    var userId = ctx.userId ? ctx.userId : '';
    var username = request.username ? request.username : '';
    logger.info('SupKonQuest email auth %s userId=%s username=%s.', action, userId, username);
}
/// <reference path="auth.ts" />
function InitModule(ctx, logger, nk, initializer) {
    initializer.registerBeforeAuthenticateEmail(beforeAuthenticateEmail);
    initializer.registerAfterAuthenticateEmail(afterAuthenticateEmail);
    logger.info('SupKonQuest auth hooks registered (before/after AuthenticateEmail).');
    initializer.registerRpc(SupKonQuestRpcFindMatchId, rpcFindMatch);
    initializer.registerMatch(SupKonQuestModuleName, {
        matchInit: matchInit,
        matchJoinAttempt: matchJoinAttempt,
        matchJoin: matchJoin,
        matchLeave: matchLeave,
        matchLoop: matchLoop,
        matchTerminate: matchTerminate,
        matchSignal: matchSignal,
    });
    initializer.registerMatchmakerMatched(onMatchmakerMatched);
    logger.info('SupKonQuest Nakama relay logic loaded (matchmaker + lobby + relay).');
}
var onMatchmakerMatched = function (ctx, logger, nk, matches) {
    if (!matches || matches.length === 0) {
        return;
    }
    var properties = matches[0].properties;
    var game = properties['game'];
    var mode = properties['mode'];
    if (game !== SupKonQuestMatchmakerGame || mode !== SupKonQuestMatchmakerMode) {
        logger.debug('Matchmaker matched ignored: game=%s mode=%s.', game, mode);
        return;
    }
    var matchId = nk.matchCreate(SupKonQuestModuleName, {
        maxPlayers: String(LobbyMaxPlayers),
    });
    logger.info('SupKonQuest matchmaker created relay match %s (players=%d).', matchId, matches.length);
    return matchId;
};
