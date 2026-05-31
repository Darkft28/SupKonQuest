let rpcFindMatch: nkruntime.RpcFunction = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, payload: string): string {
	if (!ctx.userId) {
		throw Error('No user ID in context');
	}

	let request = parseJsonObject(payload);
	if (!payload || payload.trim().length === 0) {
		request = {};
	}

	if (request === null) {
		throw Error('Invalid payload. Expected a JSON object.');
	}

	const requestedMode = readString(request['mode'], 32);
	if (requestedMode !== null && requestedMode !== SupKonQuestMatchmakerMode) {
		throw Error('Unsupported mode.');
	}

	const requestedMaxPlayers = readOptionalInteger(request['maxPlayers'], 8);
	const maxPlayers = requestedMaxPlayers === null ? SupKonQuestDefaultMaxPlayers : Math.max(2, requestedMaxPlayers);

	return JSON.stringify({
		matchIds: [],
		mode: SupKonQuestMatchmakerMode,
		maxPlayers: maxPlayers,
		useMatchmaker: true,
		nonAuthoritative: true,
	});
};

function readOptionalInteger(value: unknown, max: number): number | null {
	if (value === undefined || value === null) {
		return null;
	}

	return readInteger(value, 2, max);
}
