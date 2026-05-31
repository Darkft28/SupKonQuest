let matchInit: nkruntime.MatchInitFunction<RelayMatchState> = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, params: {[key: string]: string}) {
	const maxPlayers = resolveMaxPlayers(params['maxPlayers']);
	const state = createInitialRelayMatchState(maxPlayers, ctx.matchId || '');

	logger.info('SupKonQuest relay match initialized (maxPlayers=%d, seed=%d).', maxPlayers, state.seed);

	return {
		state,
		tickRate: LobbyMatchTickRate,
		label: buildMatchLabel(state),
	};
};

let matchJoinAttempt: nkruntime.MatchJoinAttemptFunction<RelayMatchState> = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher, tick: number, state: RelayMatchState, presence: nkruntime.Presence, metadata: {[key: string]: any}) {
	const existingPresence = state.players[presence.userId];
	if (existingPresence !== undefined) {
		if (existingPresence === null) {
			state.joinsInProgress++;
			return {
				state,
				accept: true,
			};
		}

		return {
			state,
			accept: false,
			rejectMessage: 'already joined',
		};
	}

	if (connectedPlayerCount(state) + state.joinsInProgress >= state.maxPlayers) {
		return {
			state,
			accept: false,
			rejectMessage: 'match full',
		};
	}

	state.joinsInProgress++;
	return {
		state,
		accept: true,
	};
};

let matchJoin: nkruntime.MatchJoinFunction<RelayMatchState> = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher, tick: number, state: RelayMatchState, presences: nkruntime.Presence[]) {
	const previousCount = getLobbyPlayerCount(state);

	for (const presence of presences) {
		state.players[presence.userId] = presence;
		state.lastSequenceByUser[presence.userId] = state.lastSequenceByUser[presence.userId] ?? 0;
		if (state.joinsInProgress > 0) {
			state.joinsInProgress--;
		}
	}

	updateMatchLabel(dispatcher, state);
	onLobbyPlayerCountChanged(ctx, logger, dispatcher, state, previousCount, getLobbyPlayerCount(state));
	return { state };
};

let matchLeave: nkruntime.MatchLeaveFunction<RelayMatchState> = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher, tick: number, state: RelayMatchState, presences: nkruntime.Presence[]) {
	const previousCount = getLobbyPlayerCount(state);

	for (const presence of presences) {
		state.players[presence.userId] = null;
		logger.info('Player %s left match %s.', presence.userId, ctx.matchId);
		if (!state.matchStarted) {
			continue;
		}

		const teamId = state.userToTeamId[presence.userId];
		if (teamId === undefined || state.cleanupAppliedByTeam[teamId] === true) {
			continue;
		}
		state.cleanupAppliedByTeam[teamId] = true;
		broadcastPlayerLeaveCleanup(dispatcher, teamId);
	}

	updateMatchLabel(dispatcher, state);
	onLobbyPlayerCountChanged(ctx, logger, dispatcher, state, previousCount, getLobbyPlayerCount(state));
	return { state };
};

let matchLoop: nkruntime.MatchLoopFunction<RelayMatchState> = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher, tick: number, state: RelayMatchState, messages: nkruntime.MatchMessage[]) {
	const activePlayers = connectedPlayerCount(state);
	if (activePlayers === 0 && state.joinsInProgress === 0) {
		state.emptyTicks++;
		if (state.emptyTicks >= SupKonQuestIdleSeconds * LobbyMatchTickRate) {
			logger.info('Closing idle SupKonQuest relay match %s.', ctx.matchId);
			return null;
		}
	} else {
		state.emptyTicks = 0;
	}

	tickLobby(ctx, logger, dispatcher, state);

	for (const message of messages) {
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

		const payload = nk.binaryToString(message.data);
		const parsed = parseJsonObject(payload);
		if (parsed === null) {
			logger.debug('Rejected relay payload: invalid JSON for opcode %d from %s.', message.opCode, message.sender.userId);
			continue;
		}

		const envelope = validateRelayEnvelope(parsed, state, message.sender.userId);
		if (envelope === null) {
			logger.debug('Rejected relay command from %s because the envelope is invalid or out of order.', message.sender.userId);
			continue;
		}

		let accepted = false;
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

	return { state };
};

let matchTerminate: nkruntime.MatchTerminateFunction<RelayMatchState> = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher, tick: number, state: RelayMatchState, graceSeconds: number) {
	return { state };
};

let matchSignal: nkruntime.MatchSignalFunction<RelayMatchState> = function (ctx: nkruntime.Context, logger: nkruntime.Logger, nk: nkruntime.Nakama, dispatcher: nkruntime.MatchDispatcher, tick: number, state: RelayMatchState, data: string) {
	return {
		state,
		data,
	};
};

function resolveMaxPlayers(rawValue: string | undefined): number {
	if (!rawValue) {
		return LobbyMaxPlayers;
	}

	const parsed = parseInt(rawValue, 10);
	if (!isFinite(parsed) || parsed < LobbyMinPlayers) {
		return LobbyMaxPlayers;
	}

	return parsed > LobbyMaxPlayers ? LobbyMaxPlayers : parsed;
}

function connectedPlayerCount(state: RelayMatchState): number {
	return getLobbyPlayerCount(state);
}

function updateMatchLabel(dispatcher: nkruntime.MatchDispatcher, state: RelayMatchState): void {
	state.label = createRelayLabel(connectedPlayerCount(state), state.maxPlayers);
	dispatcher.matchLabelUpdate(buildMatchLabel(state));
}

function broadcastPlayerLeaveCleanup(dispatcher: nkruntime.MatchDispatcher, teamId: number): void {
	const payload = JSON.stringify({
		senderUserId: 'server',
		sequence: 0,
		teamId: teamId,
	});
	dispatcher.broadcastMessage(RelayOpcode.PlayerLeaveCleanup, payload);
}

function validateRelayEnvelope(parsed: JsonObject, state: RelayMatchState, senderUserId: string): RelayCommandEnvelope | null {
	const envelopeUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	if (envelopeUserId === null || sequence === null) {
		return null;
	}

	if (envelopeUserId !== senderUserId) {
		return null;
	}

	if (state.players[senderUserId] === undefined || state.players[senderUserId] === null) {
		return null;
	}

	const lastSequence = state.lastSequenceByUser[senderUserId] ?? 0;
	if (sequence !== lastSequence + 1) {
		return null;
	}

	return {
		senderUserId: envelopeUserId,
		sequence: sequence,
	};
}

function validateBuyUnitCommand(parsed: JsonObject): RelayBuyUnitCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const unitType = readString(readField(parsed, 'unitType', 'UnitType'), 32);
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

function validateBuyShipCommand(parsed: JsonObject): RelayBuyShipCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const shipType = readShipType(readField(parsed, 'shipType', 'ShipType'));
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

function validateMoveUnitsCommand(parsed: JsonObject): RelayMoveUnitsCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const unitIds = readStringArray(readField(parsed, 'unitIds', 'UnitIds'), 128, 64);
	const startX = readNumber(readField(parsed, 'startX', 'StartX'));
	const startY = readNumber(readField(parsed, 'startY', 'StartY'));
	const targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
	const targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
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

function validateMoveShipsCommand(parsed: JsonObject): RelayMoveShipsCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const shipIds = readStringArray(readField(parsed, 'shipIds', 'ShipIds'), 128, 64);
	const targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
	const targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
	const unloadRequested = readField(parsed, 'unloadRequested', 'UnloadRequested');
	const unloadX = readNumber(readField(parsed, 'unloadX', 'UnloadX'));
	const unloadY = readNumber(readField(parsed, 'unloadY', 'UnloadY'));
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
		unloadX: unloadX ?? 0,
		unloadY: unloadY ?? 0,
	};
}

function validateUnitDamageCommand(parsed: JsonObject): RelayUnitDamageCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const targetNetworkId = readString(readField(parsed, 'targetNetworkId', 'TargetNetworkId'), 64);
	const damage = readNumber(readField(parsed, 'damage', 'Damage'));
	const attackerTeamId = readPositiveInteger(readField(parsed, 'attackerTeamId', 'AttackerTeamId'), 2147483647);
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

function validateShipDamageCommand(parsed: JsonObject): RelayShipDamageCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const targetNetworkId = readString(readField(parsed, 'targetNetworkId', 'TargetNetworkId'), 64);
	const damage = readNumber(readField(parsed, 'damage', 'Damage'));
	const attackerTeamId = readPositiveInteger(readField(parsed, 'attackerTeamId', 'AttackerTeamId'), 2147483647);
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

function validateEntityDiedCommand(parsed: JsonObject): RelayEntityDiedCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const networkId = readString(readField(parsed, 'networkId', 'NetworkId'), 64);
	if (senderUserId === null || sequence === null || networkId === null) {
		return null;
	}

	return {
		senderUserId: senderUserId,
		sequence: sequence,
		networkId: networkId,
	};
}

function validateAttackCampCommand(parsed: JsonObject): RelayAttackCampCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const unitIds = readStringArray(readField(parsed, 'unitIds', 'UnitIds'), 128, 64);
	const campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
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

function validateCampCapturedCommand(parsed: JsonObject): RelayCampCapturedCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
	const newTeamId = readPositiveInteger(readField(parsed, 'newTeamId', 'NewTeamId'), 2147483647);
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

function validateUnitsMoveToTransportCommand(parsed: JsonObject): RelayUnitsMoveToTransportCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const shipNetworkId = readString(readField(parsed, 'shipNetworkId', 'ShipNetworkId'), 64);
	const unitIds = readStringArray(readField(parsed, 'unitIds', 'UnitIds'), 32, 64);
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

function validateBoardTransportCommand(parsed: JsonObject): RelayBoardTransportCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const shipNetworkId = readString(readField(parsed, 'shipNetworkId', 'ShipNetworkId'), 64);
	const unitNetworkId = readString(readField(parsed, 'unitNetworkId', 'UnitNetworkId'), 64);
	const unitType = readUnitType(readField(parsed, 'unitType', 'UnitType'));
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const health = readNumber(readField(parsed, 'health', 'Health'));
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

function validateTransportUnloadedCommand(parsed: JsonObject): RelayTransportUnloadedCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const shipNetworkId = readString(readField(parsed, 'shipNetworkId', 'ShipNetworkId'), 64);
	const unitNetworkIds = readStringArray(readField(parsed, 'unitNetworkIds', 'UnitNetworkIds'), 32, 64);
	const unitTypesRaw = readField(parsed, 'unitTypes', 'UnitTypes');
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	if (senderUserId === null || sequence === null || shipNetworkId === null || unitNetworkIds === null || teamId === null) {
		return null;
	}

	const count = unitNetworkIds.length;
	const unitTypes: string[] = [];
	if (!Array.isArray(unitTypesRaw) || unitTypesRaw.length !== count) {
		return null;
	}
	for (const item of unitTypesRaw) {
		const unitType = readUnitType(item);
		if (unitType === null) {
			return null;
		}
		unitTypes.push(unitType);
	}

	const posXs = readFloatArray(readField(parsed, 'posXs', 'PosXs'), count, 32);
	const posYs = readFloatArray(readField(parsed, 'posYs', 'PosYs'), count, 32);
	const healths = readFloatArray(readField(parsed, 'healths', 'Healths'), count, 32);
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

function validateCampDamageCommand(parsed: JsonObject): RelayCampDamageCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
	const damage = readNumber(readField(parsed, 'damage', 'Damage'));
	const attackerTeamId = readPositiveInteger(readField(parsed, 'attackerTeamId', 'AttackerTeamId'), 2147483647);
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

function validateSpawnShipCommand(parsed: JsonObject): RelaySpawnShipCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const networkId = readString(readField(parsed, 'networkId', 'NetworkId'), 64);
	const shipType = readShipType(readField(parsed, 'shipType', 'ShipType'));
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const posX = readNumber(readField(parsed, 'posX', 'PosX'));
	const posY = readNumber(readField(parsed, 'posY', 'PosY'));
	const health = readNumber(readField(parsed, 'health', 'Health'));
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

function validateSpawnUnitCommand(parsed: JsonObject): RelaySpawnUnitCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const networkId = readString(readField(parsed, 'networkId', 'NetworkId'), 64);
	const unitType = readUnitType(readField(parsed, 'unitType', 'UnitType'));
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
	const posX = readNumber(readField(parsed, 'posX', 'PosX'));
	const posY = readNumber(readField(parsed, 'posY', 'PosY'));
	const health = readNumber(readField(parsed, 'health', 'Health'));
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

function validateBuildPortCommand(parsed: JsonObject): RelayBuildPortCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const campId = readPositiveInteger(readField(parsed, 'campId', 'CampId'), 2147483647);
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const posX = readNumber(readField(parsed, 'posX', 'PosX'));
	const posY = readNumber(readField(parsed, 'posY', 'PosY'));
	const rotation = readNumber(readField(parsed, 'rotation', 'Rotation'));
	const flipH = readField(parsed, 'flipH', 'FlipH');
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

function validateGoldSnapshotCommand(parsed: JsonObject): RelayGoldSnapshotCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const gold = readNonNegativeInteger(readField(parsed, 'gold', 'Gold'), 2147483647);
	const version = readNonNegativeInteger(readField(parsed, 'version', 'Version'), 2147483647);
	const reason = readOptionalString(readField(parsed, 'reason', 'Reason'), 64);
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

function validateCastUltimateCommand(parsed: JsonObject): RelayCastUltimateCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const abilityId = readString(readField(parsed, 'abilityId', 'AbilityId'), 64);
	const targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
	const targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
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

function validateUltimateVfxCommand(parsed: JsonObject): RelayUltimateVfxCommand | null {
	const senderUserId = readString(readField(parsed, 'senderUserId', 'SenderUserId'), 64);
	const sequence = readPositiveInteger(readField(parsed, 'sequence', 'Sequence'), 2147483647);
	const teamId = readPositiveInteger(readField(parsed, 'teamId', 'TeamId'), 2147483647);
	const abilityId = readString(readField(parsed, 'abilityId', 'AbilityId'), 64);
	const targetX = readNumber(readField(parsed, 'targetX', 'TargetX'));
	const targetY = readNumber(readField(parsed, 'targetY', 'TargetY'));
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

