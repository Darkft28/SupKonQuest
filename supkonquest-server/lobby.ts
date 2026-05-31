function generateSeedFromMatchId(matchId: string): number {
    let hash = 17;
    for (let i = 0; i < matchId.length; i++) {
        hash = hash * 31 + matchId.charCodeAt(i);
    }

    return hash & 0x7fffffff;
}

function getConnectedUserIds(state: RelayMatchState): string[] {
    const userIds: string[] = [];
    for (const userId of Object.keys(state.players)) {
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

function getLobbyPlayerCount(state: RelayMatchState): number {
    return getConnectedUserIds(state).length;
}

function broadcastLobbyTick(dispatcher: nkruntime.MatchDispatcher, state: RelayMatchState): void {
    const playerCount = getLobbyPlayerCount(state);
    const secondsRemaining = Math.max(0, Math.ceil(state.lobbySecondsRemaining));
    const payload = JSON.stringify({
        secondsRemaining: secondsRemaining,
        playerCount: playerCount,
    });
    dispatcher.broadcastMessage(LobbyOpcode.LobbyTick, payload);
}

function startMatch(
    ctx: nkruntime.Context,
    logger: nkruntime.Logger,
    dispatcher: nkruntime.MatchDispatcher,
    state: RelayMatchState
): void {
    if (state.matchStarted) {
        return;
    }

    const orderedUserIds = getConnectedUserIds(state);
    if (orderedUserIds.length < LobbyMinPlayers) {
        return;
    }

    state.matchStarted = true;
    state.lobbyActive = false;
    state.lobbySecondsRemaining = 0;
    state.userToTeamId = {};
    for (let i = 0; i < orderedUserIds.length; i++) {
        state.userToTeamId[orderedUserIds[i]] = i + 1;
    }

    const payload = JSON.stringify({
        seed: state.seed,
        orderedUserIds: orderedUserIds,
        matchId: ctx.matchId,
        mapType: state.mapType,
    });
    dispatcher.broadcastMessage(LobbyOpcode.MatchStart, payload);
    logger.info(
        'SupKonQuest match %s started with %d players (seed=%d, mapType=%d).',
        ctx.matchId,
        orderedUserIds.length,
        state.seed,
        state.mapType
    );
}

function onLobbyPlayerCountChanged(
    ctx: nkruntime.Context,
    logger: nkruntime.Logger,
    dispatcher: nkruntime.MatchDispatcher,
    state: RelayMatchState,
    previousCount: number,
    newCount: number
): void {
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
    } else if (newCount > previousCount && previousCount >= LobbyMinPlayers) {
        state.lobbySecondsRemaining += LobbyJoinBonusSeconds;
        broadcastLobbyTick(dispatcher, state);
        logger.info(
            'SupKonQuest match %s lobby extended by %ds (players=%d, remaining=%.1fs).',
            ctx.matchId,
            LobbyJoinBonusSeconds,
            newCount,
            state.lobbySecondsRemaining
        );
    }

    if (newCount >= LobbyMaxPlayers) {
        startMatch(ctx, logger, dispatcher, state);
    }
}

function tickLobby(
    ctx: nkruntime.Context,
    logger: nkruntime.Logger,
    dispatcher: nkruntime.MatchDispatcher,
    state: RelayMatchState
): void {
    if (state.matchStarted || !state.lobbyActive) {
        return;
    }

    const playerCount = getLobbyPlayerCount(state);
    if (playerCount < LobbyMinPlayers) {
        state.lobbyActive = false;
        state.lobbySecondsRemaining = 0;
        state.lastLobbyBroadcastSecond = -1;
        return;
    }

    state.lobbySecondsRemaining -= 1 / LobbyMatchTickRate;

    const broadcastSecond = Math.max(0, Math.ceil(state.lobbySecondsRemaining));
    if (broadcastSecond !== state.lastLobbyBroadcastSecond) {
        state.lastLobbyBroadcastSecond = broadcastSecond;
        broadcastLobbyTick(dispatcher, state);
    }

    if (state.lobbySecondsRemaining <= 0) {
        startMatch(ctx, logger, dispatcher, state);
    }
}
