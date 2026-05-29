using Godot;
using Nakama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public partial class NakamaService : Node
{
	public static NakamaService Instance { get; private set; }

	[Signal] public delegate void AuthenticatedEventHandler(string userId, string displayName);
	[Signal] public delegate void AuthenticationFailedEventHandler(string reason);
	[Signal] public delegate void MatchmakingStartedEventHandler(string ticket);
	[Signal] public delegate void MatchmakingFailedEventHandler(string reason);
	[Signal] public delegate void MatchLobbyEnteredEventHandler(string matchId, int pendingSeed);
	[Signal] public delegate void MatchLobbyTickEventHandler(int secondsRemaining, int playerCount);
	[Signal] public delegate void MatchStartingEventHandler(string matchId, int localTeamId, int seed, int playerCount);
	[Signal] public delegate void MatchStateReceivedEventHandler(long opcode, string payload);
	[Signal] public delegate void DisconnectedEventHandler();
	[Signal] public delegate void LoggedOutEventHandler();

	public const int MaxMatchPlayers = 8;
	public const int MinPasswordLength = 8;
	public const int MinUsernameLength = 3;
	public const int MaxUsernameLength = 16;
	private const long OpcodeLobbyTick = 4001;
	private const long OpcodeMatchStart = 4002;

	private const string DeviceIdFilePath = "user://nakama_device_id.txt";
	private const string ConfigDeviceSlotPath = "nakama/device_slot";
	private const string EnvDeviceSlotName = "SUPKONQUEST_NAKAMA_SLOT";
	private const string ConfigSchemePath = "nakama/scheme";
	private const string ConfigHostPath = "nakama/host";
	private const string ConfigPortPath = "nakama/port";
	private const string ConfigServerKeyPath = "nakama/server_key";
	private const string DefaultScheme = "http";
	private const string DefaultHost = "4.166.40.82";
	private const int DefaultPort = 7350;
	private const string DefaultServerKey = "defaultkey";

	private Client _client;
	private ISession _session;
	private ISocket _socket;
	private string _scheme = DefaultScheme;
	private string _host = DefaultHost;
	private int _port = DefaultPort;
	private string _serverKey = DefaultServerKey;
	private string _deviceId = "";
	private string _userId = "";
	private string _displayName = "";
	private string _matchId = "";
	private string _matchmakerTicket = "";
	private string _localSessionId = "";
	private int _pendingMatchSeed;
	private bool _inMatchLobby;
	private bool _hasEmittedMatchStarting;
	private float _lobbyCountdownSeconds = -1f;
	private bool _hasReceivedLobbyTick;
	private readonly Dictionary<string, string> _matchPlayers = new();
	private readonly Dictionary<string, int> _userTeamMap = new();
	private AuthType _currentAuthType = AuthType.Guest;

	public bool IsAuthenticated => _session != null && !_session.IsExpired;
	public AuthType CurrentAuthType => _currentAuthType;
	public bool IsGuestAccount => _currentAuthType == AuthType.Guest;
	public bool IsSocketConnected => _socket != null;
	public string UserId => _userId;
	public string DisplayName => _displayName;
	public string MatchId => _matchId;
	public IReadOnlyDictionary<string, string> MatchPlayers => _matchPlayers;
	public bool IsInMatchLobby => _inMatchLobby;
	public int LobbySecondsRemaining => _hasReceivedLobbyTick
		? Math.Max(0, (int)Math.Ceiling(_lobbyCountdownSeconds))
		: -1;

	public override void _Ready()
	{
		Instance = this;
		LoadConfiguration();
	}

	public override void _ExitTree()
	{
		Disconnect();
		if (Instance == this)
			Instance = null;
	}

	public void LoadConfiguration()
	{
		_scheme = GetProjectSetting(ConfigSchemePath, DefaultScheme);
		_host = GetProjectSetting(ConfigHostPath, DefaultHost);
		_port = (int)GetProjectSetting(ConfigPortPath, DefaultPort);
		_serverKey = GetProjectSetting(ConfigServerKeyPath, DefaultServerKey);
		GD.Print($"[NAKAMA] Config: {_scheme}://{_host}:{_port}");
	}

	public void Configure(string scheme, string host, int port, string serverKey)
	{
		_scheme = string.IsNullOrWhiteSpace(scheme) ? DefaultScheme : scheme.Trim();
		_host = string.IsNullOrWhiteSpace(host) ? DefaultHost : host.Trim();
		_port = port > 0 ? port : DefaultPort;
		_serverKey = string.IsNullOrWhiteSpace(serverKey) ? DefaultServerKey : serverKey.Trim();
	}

	public async Task<bool> TryRestoreSessionAsync()
	{
		if (!AuthSessionStore.HasStoredSession)
			return false;

		if (!AuthSessionStore.TryLoad(out ISession restored, out AuthType authType))
		{
			AuthSessionStore.Clear();
			EmitSignal(SignalName.AuthenticationFailed, "auth_session_expired");
			return false;
		}

		try
		{
			EnsureClient();
			_session = restored;
			_currentAuthType = authType;

			var utcNow = DateTime.UtcNow;
			if (_session.HasExpired(utcNow) || _session.HasExpired(utcNow.AddDays(1)))
				_session = await _client.SessionRefreshAsync(_session);

			await ApplySessionAsync(_session, authType);
			return true;
		}
		catch (ApiResponseException ex)
		{
			GD.PrintErr($"[NAKAMA] Session restore failed: {ex.Message}");
			InvalidateLocalSession(clearStoredSession: true);
			EmitSignal(SignalName.AuthenticationFailed, MapAuthErrorKey(ex));
			return false;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NAKAMA] Session restore failed: {ex.Message}");
			InvalidateLocalSession(clearStoredSession: true);
			EmitSignal(SignalName.AuthenticationFailed, MapExceptionToAuthKey(ex));
			return false;
		}
	}

	public async Task<bool> RegisterWithEmailAsync(string email, string username, string password)
	{
		if (!ValidateEmail(email, out string emailError))
		{
			EmitSignal(SignalName.AuthenticationFailed, emailError);
			return false;
		}

		if (!ValidateUsername(username, out string usernameError))
		{
			EmitSignal(SignalName.AuthenticationFailed, usernameError);
			return false;
		}

		if (!ValidatePassword(password, out string passwordError))
		{
			EmitSignal(SignalName.AuthenticationFailed, passwordError);
			return false;
		}

		try
		{
			EnsureClient();
			_session = await _client.AuthenticateEmailAsync(email.Trim(), password, username, true);
			ValidateSessionOrThrow("RegisterWithEmailAsync");
			await ApplySessionAsync(_session, AuthType.Email);
			return true;
		}
		catch (ApiResponseException ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, MapAuthErrorKey(ex));
			GD.PrintErr($"[NAKAMA] Register failed: {ex.Message}");
			return false;
		}
		catch (Exception ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, MapExceptionToAuthKey(ex));
			GD.PrintErr($"[NAKAMA] Register failed: {ex.Message}");
			return false;
		}
	}

	public async Task<bool> LoginWithEmailAsync(string email, string password)
	{
		if (!ValidateEmail(email, out string emailError))
		{
			EmitSignal(SignalName.AuthenticationFailed, emailError);
			return false;
		}

		if (!ValidatePassword(password, out string passwordError))
		{
			EmitSignal(SignalName.AuthenticationFailed, passwordError);
			return false;
		}

		try
		{
			EnsureClient();
			_session = await _client.AuthenticateEmailAsync(email.Trim(), password, null, false);
			ValidateSessionOrThrow("LoginWithEmailAsync");
			await ApplySessionAsync(_session, AuthType.Email);
			return true;
		}
		catch (ApiResponseException ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, MapAuthErrorKey(ex));
			GD.PrintErr($"[NAKAMA] Login failed: {ex.Message}");
			return false;
		}
		catch (Exception ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, MapExceptionToAuthKey(ex));
			GD.PrintErr($"[NAKAMA] Login failed: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// v2: link guest device account to email via LinkEmailAsync(session, email, password).
	/// </summary>
	public Task<bool> LinkGuestToEmailAsync(string email, string password)
	{
		GD.PrintErr("[NAKAMA] LinkGuestToEmailAsync is not implemented yet.");
		return Task.FromResult(false);
	}

	public async Task AuthenticateGuestAsync()
	{
		try
		{
			EnsureClient();
			_deviceId = LoadOrCreateDeviceId();
			_session = await _client.AuthenticateDeviceAsync(_deviceId);
			ValidateSessionOrThrow("AuthenticateDeviceAsync");
			await ApplySessionAsync(_session, AuthType.Guest);
		}
		catch (ApiResponseException ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, MapAuthErrorKey(ex));
			GD.PrintErr($"[NAKAMA] Guest auth failed: {ex.Message}");
		}
		catch (Exception ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, MapExceptionToAuthKey(ex));
			GD.PrintErr($"[NAKAMA] Guest auth failed: {ex.Message}");
		}
	}

	public async Task LogoutAsync()
	{
		AuthSessionStore.Clear();
		InvalidateLocalSession(clearStoredSession: false);
		await CloseSocketAsync();
		_userId = "";
		_displayName = "";
		_currentAuthType = AuthType.Guest;
		EmitSignal(SignalName.LoggedOut);
	}

	public async Task<bool> UpdateUniqueUsernameAsync(string desiredName)
	{
		if (!IsGuestAccount)
		{
			EmitSignal(SignalName.AuthenticationFailed, "auth_error_guest_only");
			return false;
		}

		string normalized = NormalizeUsername(desiredName);
		if (!ValidateUsername(normalized, out string usernameError))
		{
			EmitSignal(SignalName.AuthenticationFailed, usernameError);
			return false;
		}

		try
		{
			EnsureClient();
			if (!IsAuthenticated)
				await AuthenticateGuestAsync();

			await _client.UpdateAccountAsync(_session, normalized, normalized, null, null, null, null);
			_displayName = normalized;
			EmitSignal(SignalName.Authenticated, _userId, _displayName);
			return true;
		}
		catch (Exception ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, MapExceptionToAuthKey(ex));
			GD.PrintErr($"[NAKAMA] Username update failed: {ex.Message}");
			return false;
		}
	}

	public async Task StartMatchmakingAsync()
	{
		try
		{
			await EnsureAuthenticatedAndSocketAsync();
			if (_socket == null)
				throw new InvalidOperationException("Socket Nakama non disponible");

			var stringProperties = new Dictionary<string, string>
			{
				{ "game", "supkonquest" },
				{ "mode", "relay" },
				{ "region", "eu" }
			};

			string query = "+properties.game:supkonquest +properties.mode:relay";
			var ticket = await _socket.AddMatchmakerAsync(query, 2, MaxMatchPlayers, stringProperties);
			if (ticket == null || string.IsNullOrWhiteSpace(ticket.Ticket))
				throw new InvalidOperationException("Nakama n'a pas renvoyé de ticket matchmaker.");

			_matchmakerTicket = ticket.Ticket;
			EmitSignal(SignalName.MatchmakingStarted, ticket.Ticket);
		}
		catch (Exception ex)
		{
			EmitSignal(SignalName.MatchmakingFailed, ex.Message);
			GD.PrintErr($"[NAKAMA] Matchmaking failed: {ex.Message}");
		}
	}

	public async Task CancelMatchmakingAsync()
	{
		if (_socket == null || string.IsNullOrWhiteSpace(_matchmakerTicket))
			return;

		try
		{
			await _socket.RemoveMatchmakerAsync(_matchmakerTicket);
			_matchmakerTicket = "";
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NAKAMA] Matchmaking cancel failed: {ex.Message}");
		}
	}

	public async Task<bool> SendMatchCommandAsync<T>(long opcode, T payload, JsonSerializerOptions serializerOptions = null)
	{
		if (_socket == null || string.IsNullOrEmpty(_matchId))
			return false;

		try
		{
			string json = serializerOptions == null
				? JsonSerializer.Serialize(payload)
				: JsonSerializer.Serialize(payload, serializerOptions);
			await _socket.SendMatchStateAsync(_matchId, opcode, json);
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NAKAMA] Send match command failed: {ex.Message}");
			return false;
		}
	}

	public void Disconnect()
	{
		_ = CloseSocketAsync();
		_session = null;
		_matchId = "";
		_matchmakerTicket = "";
		_localSessionId = "";
		_pendingMatchSeed = 0;
		ResetLobbyState();
		_userId = "";
		_displayName = "";
		_matchPlayers.Clear();
		_userTeamMap.Clear();
		EmitSignal(SignalName.Disconnected);
	}

	private async Task EnsureAuthenticatedAndSocketAsync()
	{
		if (!IsAuthenticated)
			throw new InvalidOperationException("auth_not_authenticated");

		await EnsureSocketConnectedAsync();
	}

	private async Task ApplySessionAsync(ISession session, AuthType authType)
	{
		_session = session;
		_currentAuthType = authType;
		_userId = _session.UserId;
		_displayName = ResolveDisplayName(_session);
		AuthSessionStore.Save(_session, authType);

		if (authType == AuthType.Guest)
		{
			_deviceId = LoadOrCreateDeviceId();
			GD.Print($"[NAKAMA] Guest auth userId={_userId} deviceId={_deviceId[..Math.Min(8, _deviceId.Length)]}...");
		}
		else
		{
			GD.Print($"[NAKAMA] Email auth userId={_userId} username={_displayName}");
		}

		EmitSignal(SignalName.Authenticated, _userId, _displayName);
	}

	private static string ResolveDisplayName(ISession session)
	{
		if (!string.IsNullOrWhiteSpace(session.Username))
			return session.Username;

		string userId = session.UserId ?? "";
		return userId.Length > 8 ? $"Player-{userId[..8]}" : $"Player-{userId}";
	}

	private void InvalidateLocalSession(bool clearStoredSession)
	{
		if (clearStoredSession)
			AuthSessionStore.Clear();

		_session = null;
		_userId = "";
		_displayName = "";
	}

	private async Task CloseSocketAsync()
	{
		try
		{
			if (_socket != null)
			{
				_socket.ReceivedMatchmakerMatched -= OnReceivedMatchmakerMatched;
				_socket.ReceivedMatchPresence -= OnReceivedMatchPresence;
				_socket.ReceivedMatchState -= OnReceivedMatchState;
				await _socket.CloseAsync();
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NAKAMA] Socket disconnect warning: {ex.Message}");
		}

		_socket = null;
	}

	public static string MapAuthErrorKey(ApiResponseException ex)
	{
		if (ex == null)
			return "connection_failed";

		string message = ex.Message?.ToLowerInvariant() ?? "";
		if (ex.StatusCode == 401 || message.Contains("invalid") || message.Contains("credentials"))
			return "auth_error_invalid_credentials";

		if (message.Contains("username") && (message.Contains("exists") || message.Contains("in use")))
			return "auth_error_username_taken";

		if (message.Contains("password") || message.Contains("too short"))
			return "auth_error_password";

		if (message.Contains("email"))
			return "auth_error_email";

		return "connection_failed";
	}

	public static string MapExceptionToAuthKey(Exception ex)
	{
		if (ex is ApiResponseException apiEx)
			return MapAuthErrorKey(apiEx);

		string message = ex.Message?.ToLowerInvariant() ?? "";
		if (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException
		    || message.Contains("sending the request")
		    || message.Contains("connection refused")
		    || message.Contains("no connection")
		    || message.Contains("actively refused")
		    || message.Contains("timed out")
		    || message.Contains("could not be established"))
			return "auth_error_network";

		return "connection_failed";
	}

	public static bool ValidateEmail(string email, out string errorKey)
	{
		errorKey = "";
		if (string.IsNullOrWhiteSpace(email))
		{
			errorKey = "auth_error_email";
			return false;
		}

		string trimmed = email.Trim();
		if (trimmed.Length < 10 || trimmed.Length > 255 || !trimmed.Contains('@'))
		{
			errorKey = "auth_error_email";
			return false;
		}

		return true;
	}

	public static bool ValidateUsername(string username, out string errorKey)
	{
		errorKey = "";
		string normalized = NormalizeUsername(username);
		if (normalized.Length < MinUsernameLength)
		{
			errorKey = "auth_error_username";
			return false;
		}

		return true;
	}

	public static bool ValidatePassword(string password, out string errorKey)
	{
		errorKey = "";
		if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
		{
			errorKey = "auth_error_password";
			return false;
		}

		return true;
	}

	private async Task EnsureSocketConnectedAsync()
	{
		if (_socket != null)
			return;

		ValidateSessionOrThrow("EnsureSocketConnectedAsync");
		EnsureClient();
		_socket = Socket.From(_client);
		_socket.ReceivedMatchmakerMatched += OnReceivedMatchmakerMatched;
		_socket.ReceivedMatchPresence += OnReceivedMatchPresence;
		_socket.ReceivedMatchState += OnReceivedMatchState;

		try
		{
			await _socket.ConnectAsync(_session);
		}
		catch
		{
			// En cas d'échec (ex: serveur indisponible), forcer un état propre pour autoriser un retry.
			_socket.ReceivedMatchmakerMatched -= OnReceivedMatchmakerMatched;
			_socket.ReceivedMatchPresence -= OnReceivedMatchPresence;
			_socket.ReceivedMatchState -= OnReceivedMatchState;
			_socket = null;
			throw;
		}
	}

	private void EnsureClient()
	{
		if (_client != null)
			return;

		_client = new Client(_scheme, _host, _port, _serverKey);
	}

	private async void OnReceivedMatchmakerMatched(IMatchmakerMatched matched)
	{
		try
		{
			if (_socket == null)
				return;

			IMatch match = await _socket.JoinMatchAsync(matched);
			if (match == null || string.IsNullOrWhiteSpace(match.Id))
				throw new InvalidOperationException("Nakama n'a pas renvoyé de match après matchmaking.");

			_matchId = match.Id;
			_matchmakerTicket = "";
			ResetLobbyState();
			_localSessionId = matched?.Self?.Presence?.SessionId ?? "";
			RefreshMatchPlayers(match, matched);
			LogMatchPresences(match);

			if (HasDuplicateLocalUserId(match, matched, out int duplicateSessions))
			{
				await AbortMatchForDuplicateIdentityAsync($"join snapshot ({duplicateSessions} matching presences)");
				return;
			}

			_pendingMatchSeed = GenerateSeedFromMatchId(match.Id);
			CallDeferred(nameof(DeferredTryEnterMatchLobby), "join snapshot");
		}
		catch (Exception ex)
		{
			EmitMatchmakingFailedThreadSafe(ex.Message);
			GD.PrintErr($"[NAKAMA] Join match failed: {ex.Message}");
		}
	}

	private void DeferredTryEnterMatchLobby(string source)
	{
		TryEnterMatchLobby(source);
	}

	private void DeferredEmitMatchLobbyEntered(string matchId, int pendingSeed)
	{
		EmitSignal(SignalName.MatchLobbyEntered, matchId, pendingSeed);
	}

	private void DeferredEmitMatchStarting(string matchId, int localTeamId, int seed, int playerCount)
	{
		EmitSignal(SignalName.MatchStarting, matchId, localTeamId, seed, playerCount);
	}

	private void DeferredEmitMatchmakingFailed(string reason)
	{
		EmitSignal(SignalName.MatchmakingFailed, reason);
	}

	private void EmitMatchmakingFailedThreadSafe(string reason)
	{
		CallDeferred(nameof(DeferredEmitMatchmakingFailed), reason);
	}

	private void OnReceivedMatchPresence(IMatchPresenceEvent matchPresence)
	{
		bool duplicateIdentityDetected = false;
		foreach (var joined in matchPresence.Joins)
		{
			_matchPlayers[joined.UserId] = string.IsNullOrWhiteSpace(joined.Username) ? joined.UserId : joined.Username;
			if (joined.UserId != _userId)
				continue;

			if (string.IsNullOrWhiteSpace(_localSessionId))
			{
				_localSessionId = joined.SessionId;
				continue;
			}

			if (!string.Equals(joined.SessionId, _localSessionId, StringComparison.Ordinal))
			{
				duplicateIdentityDetected = true;
			}
		}

		foreach (var left in matchPresence.Leaves)
		{
			_matchPlayers.Remove(left.UserId);
		}

		CallDeferred(nameof(DeferredOnReceivedMatchPresence), duplicateIdentityDetected);
	}

	private void DeferredOnReceivedMatchPresence(bool duplicateIdentityDetected)
	{
		if (duplicateIdentityDetected)
		{
			_ = AbortMatchForDuplicateIdentityAsync("presence event");
			return;
		}

		if (_inMatchLobby)
			OnLobbyPresenceChanged();
		else
			TryEnterMatchLobby("presence event");

	}

	private void OnReceivedMatchState(IMatchState matchState)
	{
		string payload = Encoding.UTF8.GetString(matchState.State);
		CallDeferred(nameof(DeferredHandleMatchState), (long)matchState.OpCode, payload);
	}

	private void DeferredHandleMatchState(long opcode, string payload)
	{
		if (opcode == OpcodeLobbyTick)
		{
			ApplyRelayLobbyTick(payload);
			return;
		}

		if (opcode == OpcodeMatchStart)
		{
			ApplyRelayMatchStart(payload);
			return;
		}

		EmitSignal(SignalName.MatchStateReceived, opcode, payload);
		NetworkCommandRouter.HandleIncomingRelayCommand(opcode, payload);
	}

	private void TryEnterMatchLobby(string source)
	{
		if (_inMatchLobby || _hasEmittedMatchStarting || string.IsNullOrWhiteSpace(_matchId))
			return;

		var orderedUserIds = GetOrderedParticipantUserIds();
		if (orderedUserIds.Count < 2)
		{
			GD.Print($"[NAKAMA] Waiting for lobby ({source}): count={orderedUserIds.Count}");
			return;
		}

		if (orderedUserIds.FindIndex(id => id == _userId) < 0)
		{
			GD.PrintErr($"[NAKAMA] Local user missing from lobby ({source}).");
			return;
		}

		BeginMatchLobby();
		GD.Print($"[NAKAMA] Match lobby entered ({source}): {_matchId} players={orderedUserIds.Count}");
	}

	private void BeginMatchLobby()
	{
		_inMatchLobby = true;
		_hasEmittedMatchStarting = false;
		_hasReceivedLobbyTick = false;
		_lobbyCountdownSeconds = -1f;

		int seed = _pendingMatchSeed != 0 ? _pendingMatchSeed : GenerateSeedFromMatchId(_matchId);
		CallDeferred(nameof(DeferredEmitMatchLobbyEntered), _matchId, seed);
		EmitLobbyTick();
	}

	private void OnLobbyPresenceChanged()
	{
		EmitLobbyTick();
	}

	private void EmitLobbyTick()
	{
		CallDeferred(nameof(DeferredEmitLobbyTick));
	}

	private void DeferredEmitLobbyTick()
	{
		int seconds = LobbySecondsRemaining;
		int playerCount = GetOrderedParticipantUserIds().Count;
		EmitSignal(SignalName.MatchLobbyTick, seconds, playerCount);
	}

	private void ApplyRelayLobbyTick(string payload)
	{
		var tick = JsonSerializer.Deserialize<RelayLobbyTickPayload>(payload, RelayLobbyJsonReadOptions);
		if (tick == null)
			return;

		_hasReceivedLobbyTick = true;
		_lobbyCountdownSeconds = Math.Max(0f, tick.SecondsRemaining);
		DeferredEmitLobbyTick();
	}

	private void ApplyRelayMatchStart(string payload)
	{
		var start = JsonSerializer.Deserialize<RelayMatchStartPayload>(payload, RelayLobbyJsonReadOptions);
		if (start == null || start.OrderedUserIds == null || start.OrderedUserIds.Length < 2)
		{
			GD.PrintErr("[NAKAMA] Invalid relay MatchStart payload.");
			return;
		}

		FinalizeMatchStartFromRelay(start.Seed, start.OrderedUserIds);
	}

	private void FinalizeMatchStartFromRelay(int seed, string[] orderedUserIds, string source = "relay")
	{
		if (_hasEmittedMatchStarting || string.IsNullOrWhiteSpace(_matchId))
			return;

		var ordered = orderedUserIds
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.OrderBy(id => id, StringComparer.Ordinal)
			.ToList();

		if (ordered.Count < 2)
			return;

		int localIndex = ordered.FindIndex(id => id == _userId);
		if (localIndex < 0)
		{
			GD.PrintErr($"[NAKAMA] MatchStart missing local user ({source}).");
			return;
		}

		_inMatchLobby = false;
		_hasEmittedMatchStarting = true;
		int effectiveSeed = seed > 0
			? seed
			: (_pendingMatchSeed != 0 ? _pendingMatchSeed : GenerateSeedFromMatchId(_matchId));
		_pendingMatchSeed = effectiveSeed;
		int localTeamId = localIndex + 1;
		int playerCount = ordered.Count;
		_userTeamMap.Clear();
		for (int i = 0; i < ordered.Count; i++)
			_userTeamMap[ordered[i]] = i + 1;

		GD.Print($"[NAKAMA] Match starting ({source}): {_matchId} team={localTeamId} seed={effectiveSeed} players={playerCount}");
		CallDeferred(nameof(DeferredEmitMatchStarting), _matchId, localTeamId, effectiveSeed, playerCount);
	}

	private List<string> GetOrderedParticipantUserIds()
	{
		return _matchPlayers.Keys
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.OrderBy(id => id, StringComparer.Ordinal)
			.ToList();
	}

	private void ResetLobbyState()
	{
		_inMatchLobby = false;
		_hasEmittedMatchStarting = false;
		_hasReceivedLobbyTick = false;
		_lobbyCountdownSeconds = -1f;
	}

	private void RefreshMatchPlayers(IMatch match, IMatchmakerMatched matched = null)
	{
		_matchPlayers.Clear();

		bool addedFromMatch = false;
		foreach (var presence in match.Presences.OrderBy(p => p.UserId))
		{
			addedFromMatch = true;
			_matchPlayers[presence.UserId] = string.IsNullOrWhiteSpace(presence.Username)
				? presence.UserId
				: presence.Username;
		}

		if (!addedFromMatch && matched != null)
		{
			if (matched.Self?.Presence != null)
			{
				var selfPresence = matched.Self.Presence;
				_matchPlayers[selfPresence.UserId] = string.IsNullOrWhiteSpace(selfPresence.Username)
					? selfPresence.UserId
					: selfPresence.Username;
			}

			foreach (var user in matched.Users)
			{
				if (user?.Presence == null)
					continue;

				var presence = user.Presence;
				_matchPlayers[presence.UserId] = string.IsNullOrWhiteSpace(presence.Username)
					? presence.UserId
					: presence.Username;
			}
		}
	}

	private int ResolveLocalTeamId(IMatch match, IMatchmakerMatched matched)
	{
		var orderedUserIds = BuildOrderedParticipantUserIds(match, matched);
		for (int i = 0; i < orderedUserIds.Count; i++)
		{
			if (orderedUserIds[i] == _userId)
				return i + 1;
		}

		GD.PrintErr($"[NAKAMA] ResolveLocalTeamId failed: local userId={_userId} not found in presences for match={_matchId}.");
		return 1;
	}

	private static List<string> BuildOrderedParticipantUserIds(IMatch match, IMatchmakerMatched matched)
	{
		var userIds = new HashSet<string>(StringComparer.Ordinal);

		foreach (var presence in match.Presences)
		{
			if (!string.IsNullOrWhiteSpace(presence.UserId))
				userIds.Add(presence.UserId);
		}

		if (matched?.Self?.Presence != null && !string.IsNullOrWhiteSpace(matched.Self.Presence.UserId))
			userIds.Add(matched.Self.Presence.UserId);

		foreach (var user in matched?.Users ?? Enumerable.Empty<IMatchmakerUser>())
		{
			if (user?.Presence == null || string.IsNullOrWhiteSpace(user.Presence.UserId))
				continue;

			userIds.Add(user.Presence.UserId);
		}

		return userIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
	}

	private static string ResolveDeviceIdFilePath()
	{
		string slot = ResolveDeviceSlot();
		return string.IsNullOrWhiteSpace(slot)
			? DeviceIdFilePath
			: $"user://nakama_device_id_{slot}.txt";
	}

	private static string ResolveDeviceSlot()
	{
		string fromCommandLine = ReadDeviceSlotFromCommandLine();
		if (!string.IsNullOrWhiteSpace(fromCommandLine))
			return fromCommandLine;

		string envSlot = OS.GetEnvironment(EnvDeviceSlotName);
		if (!string.IsNullOrWhiteSpace(envSlot))
			return SanitizeDeviceSlot(envSlot);

		string configuredSlot = GetProjectSetting(ConfigDeviceSlotPath, "");
		return SanitizeDeviceSlot(configuredSlot);
	}

	private static string ReadDeviceSlotFromCommandLine()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			string slot = TryParseDeviceSlotArgument(arg);
			if (!string.IsNullOrWhiteSpace(slot))
				return slot;
		}

		string[] args = OS.GetCmdlineArgs();
		for (int i = 0; i < args.Length; i++)
		{
			string slot = TryParseDeviceSlotArgument(args[i]);
			if (!string.IsNullOrWhiteSpace(slot))
				return slot;

			if (IsDeviceSlotFlag(args[i]) && i + 1 < args.Length)
			{
				slot = SanitizeDeviceSlot(args[i + 1]);
				if (!string.IsNullOrWhiteSpace(slot))
					return slot;
			}
		}

		return "";
	}

	private static bool IsDeviceSlotFlag(string arg)
	{
		return string.Equals(arg, "--nakama-slot", StringComparison.OrdinalIgnoreCase);
	}

	private static string TryParseDeviceSlotArgument(string arg)
	{
		if (string.IsNullOrWhiteSpace(arg))
			return "";

		const string prefix = "--nakama-slot=";
		if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			return SanitizeDeviceSlot(arg[prefix.Length..]);

		return "";
	}

	private static string SanitizeDeviceSlot(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		var builder = new StringBuilder(value.Trim().ToLowerInvariant());
		for (int i = builder.Length - 1; i >= 0; i--)
		{
			char c = builder[i];
			if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
				continue;

			builder.Remove(i, 1);
		}

		string sanitized = builder.ToString();
		return sanitized.Length > 24 ? sanitized[..24] : sanitized;
	}

	private void LogMatchPresences(IMatch match)
	{
		GD.Print($"[NAKAMA] Match presences count={match.Presences.Count()} localUserId={_userId}");
		foreach (var presence in match.Presences.OrderBy(p => p.UserId))
		{
			GD.Print($"[NAKAMA] Presence userId={presence.UserId} username={presence.Username} sessionId={presence.SessionId}");
		}
	}

	private bool HasDuplicateLocalUserId(IMatch match, IMatchmakerMatched matched, out int duplicateSessions)
	{
		var localSessionIds = new HashSet<string>();

		foreach (var presence in match.Presences)
		{
			if (presence.UserId != _userId || string.IsNullOrWhiteSpace(presence.SessionId))
				continue;

			localSessionIds.Add(presence.SessionId);
		}

		if (matched?.Self?.Presence != null
			&& matched.Self.Presence.UserId == _userId
			&& !string.IsNullOrWhiteSpace(matched.Self.Presence.SessionId))
		{
			localSessionIds.Add(matched.Self.Presence.SessionId);
		}

		foreach (var user in matched?.Users ?? Enumerable.Empty<IMatchmakerUser>())
		{
			if (user?.Presence == null)
				continue;

			if (user.Presence.UserId != _userId || string.IsNullOrWhiteSpace(user.Presence.SessionId))
				continue;

			localSessionIds.Add(user.Presence.SessionId);
		}

		duplicateSessions = localSessionIds.Count;
		return duplicateSessions > 1;
	}

	private async Task AbortMatchForDuplicateIdentityAsync(string source)
	{
		string reason = $"[NAKAMA] Duplicate local userId detected from {source} for userId={_userId}. " +
			"Stop join to avoid same-camp spawn and relay self-filtering. Use distinct slots: --nakama-slot=1 and --nakama-slot=2.";
		GD.PrintErr(reason);

		if (_socket != null && !string.IsNullOrWhiteSpace(_matchId))
		{
			try
			{
				await _socket.LeaveMatchAsync(_matchId);
			}
			catch (Exception leaveEx)
			{
				GD.PrintErr($"[NAKAMA] LeaveMatchAsync after duplicate identity warning failed: {leaveEx.Message}");
			}
		}

		_matchId = "";
		_matchmakerTicket = "";
		_pendingMatchSeed = 0;
		ResetLobbyState();
		_matchPlayers.Clear();
		_userTeamMap.Clear();
		EmitMatchmakingFailedThreadSafe("Deux instances utilisent le meme user Nakama. Lance chaque instance avec un slot different (--nakama-slot=1, --nakama-slot=2).");
	}

	private static readonly JsonSerializerOptions RelayLobbyJsonReadOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[Serializable]
	private sealed class RelayLobbyTickPayload
	{
		public int SecondsRemaining { get; set; }
		public int PlayerCount { get; set; }
	}

	[Serializable]
	private sealed class RelayMatchStartPayload
	{
		public int Seed { get; set; }
		public string[] OrderedUserIds { get; set; } = Array.Empty<string>();
	}

	private static int GenerateSeedFromMatchId(string matchId)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in matchId)
				hash = hash * 31 + c;
			return hash & int.MaxValue;
		}
	}

	private void ValidateSessionOrThrow(string step)
	{
		if (_session == null)
			throw new InvalidOperationException($"Session Nakama vide apres {step}.");

		if (string.IsNullOrWhiteSpace(_session.UserId))
			throw new InvalidOperationException($"UserId Nakama vide apres {step}.");
	}

	private static string NormalizeUsername(string username)
	{
		if (string.IsNullOrWhiteSpace(username))
			return "";

		var builder = new StringBuilder(username.Trim());
		for (int i = builder.Length - 1; i >= 0; i--)
		{
			char c = builder[i];
			if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
				continue;

			builder.Remove(i, 1);
		}

		string normalized = builder.ToString();
		return normalized.Length > 16 ? normalized[..16] : normalized;
	}

	private static string LoadOrCreateDeviceId()
	{
		string filePath = ResolveDeviceIdFilePath();
		try
		{
			if (Godot.FileAccess.FileExists(filePath))
			{
				using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
				string existing = file?.GetAsText().Trim() ?? "";
				if (!string.IsNullOrWhiteSpace(existing))
					return existing;
			}

			string deviceId = Guid.NewGuid().ToString("N");
			using var writer = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
			writer?.StoreString(deviceId);
			return deviceId;
		}
		catch
		{
			return Guid.NewGuid().ToString("N");
		}
	}

	private static T GetProjectSetting<T>(string path, T defaultValue)
	{
		if (!ProjectSettings.HasSetting(path))
			return defaultValue;

		object value = ProjectSettings.GetSetting(path);
		if (value is T typed)
			return typed;

		try
		{
			return (T)Convert.ChangeType(value, typeof(T));
		}
		catch
		{
			return defaultValue;
		}
	}
}



