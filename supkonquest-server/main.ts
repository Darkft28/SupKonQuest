/// <reference path="auth.ts" />

function InitModule(ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, initializer: nkruntime.Initializer) {
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

let onMatchmakerMatched: nkruntime.MatchmakerMatchedFunction = function (
	ctx: nkruntime.Context,
	logger: nkruntime.Logger,
	nk: nkruntime.Nakama,
	matches: nkruntime.MatchmakerResult[]
): string | void {
	if (!matches || matches.length === 0) {
		return;
	}

	const properties = matches[0].properties;
	const game = properties['game'];
	const mode = properties['mode'];
	if (game !== SupKonQuestMatchmakerGame || mode !== SupKonQuestMatchmakerMode) {
		logger.debug('Matchmaker matched ignored: game=%s mode=%s.', game, mode);
		return;
	}

	const matchId = nk.matchCreate(SupKonQuestModuleName, {
		maxPlayers: String(LobbyMaxPlayers),
	});
	logger.info('SupKonQuest matchmaker created relay match %s (players=%d).', matchId, matches.length);
	return matchId;
};
