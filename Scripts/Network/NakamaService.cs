using Godot;
using Nakama;
using System;
using System.Collections.Generic;
using System.Linq;
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
	[Signal] public delegate void MatchJoinedEventHandler(string matchId, int localTeamId, int seed);
	[Signal] public delegate void MatchStateReceivedEventHandler(long opcode, string payload);
	[Signal] public delegate void DisconnectedEventHandler();

	private const string DeviceIdFilePath = "user://nakama_device_id.txt";
	private const string ConfigDeviceSlotPath = "nakama/device_slot";
	private const string EnvDeviceSlotName = "SUPKONQUEST_NAKAMA_SLOT";
	private const string ConfigSchemePath = "nakama/scheme";
	private const string ConfigHostPath = "nakama/host";
	private const string ConfigPortPath = "nakama/port";
	private const string ConfigServerKeyPath = "nakama/server_key";
	private const string DefaultScheme = "http";
	private const string DefaultHost = "5.22.215.153";
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
	private readonly Dictionary<string, string> _matchPlayers = new();

	public bool IsAuthenticated => _session != null && !_session.IsExpired;
	public bool IsSocketConnected => _socket != null;
	public string UserId => _userId;
	public string DisplayName => _displayName;
	public string MatchId => _matchId;
	public IReadOnlyDictionary<string, string> MatchPlayers => _matchPlayers;

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
	}

	public void Configure(string scheme, string host, int port, string serverKey)
	{
		_scheme = string.IsNullOrWhiteSpace(scheme) ? DefaultScheme : scheme.Trim();
		_host = string.IsNullOrWhiteSpace(host) ? DefaultHost : host.Trim();
		_port = port > 0 ? port : DefaultPort;
		_serverKey = string.IsNullOrWhiteSpace(serverKey) ? DefaultServerKey : serverKey.Trim();
	}

	public async Task AuthenticateGuestAsync()
	{
		try
		{
			EnsureClient();
			_deviceId = LoadOrCreateDeviceId();
			_session = await _client.AuthenticateDeviceAsync(_deviceId);
			_userId = _session.UserId;
			_displayName = string.IsNullOrWhiteSpace(_session.Username)
				? $"Guest-{_userId[..8]}"
				: _session.Username;
			GD.Print($"[NAKAMA] Authenticated userId={_userId} deviceId={_deviceId[..Math.Min(8, _deviceId.Length)]}...");

			await EnsureSocketConnectedAsync();
			EmitSignal(SignalName.Authenticated, _userId, _displayName);
		}
		catch (Exception ex)
		{
			EmitSignal(SignalName.AuthenticationFailed, ex.Message);
			GD.PrintErr($"[NAKAMA] Guest auth failed: {ex.Message}");
		}
	}

	public async Task<bool> UpdateUniqueUsernameAsync(string desiredName)
	{
		string normalized = NormalizeUsername(desiredName);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			EmitSignal(SignalName.AuthenticationFailed, "Pseudo vide ou invalide");
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
			EmitSignal(SignalName.AuthenticationFailed, ex.Message);
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
			var ticket = await _socket.AddMatchmakerAsync(query, 2, 2, stringProperties);
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

	public async Task<bool> SendMatchCommandAsync<T>(long opcode, T payload)
	{
		if (_socket == null || string.IsNullOrEmpty(_matchId))
			return false;

		try
		{
			string json = JsonSerializer.Serialize(payload);
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
		try
		{
			if (_socket != null)
			{
				_socket.ReceivedMatchmakerMatched -= OnReceivedMatchmakerMatched;
				_socket.ReceivedMatchPresence -= OnReceivedMatchPresence;
				_socket.ReceivedMatchState -= OnReceivedMatchState;
				_socket.CloseAsync();
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[NAKAMA] Socket disconnect warning: {ex.Message}");
		}

		_socket = null;
		_session = null;
		_client = null;
		_matchId = "";
		_matchmakerTicket = "";
		_localSessionId = "";
		_userId = "";
		_displayName = "";
		_matchPlayers.Clear();
		EmitSignal(SignalName.Disconnected);
	}

	private async Task EnsureAuthenticatedAndSocketAsync()
	{
		if (!IsAuthenticated)
			await AuthenticateGuestAsync();

		await EnsureSocketConnectedAsync();
	}

	private async Task EnsureSocketConnectedAsync()
	{
		if (_socket != null)
			return;

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
			_matchId = match.Id;
			_matchmakerTicket = "";
			_localSessionId = matched?.Self?.Presence?.SessionId ?? "";
			RefreshMatchPlayers(match, matched);
			LogMatchPresences(match);

			if (HasDuplicateLocalUserId(match, matched, out int duplicateSessions))
			{
				await AbortMatchForDuplicateIdentityAsync($"join snapshot ({duplicateSessions} matching presences)");
				return;
			}

			int localTeamId = ResolveLocalTeamId(match, matched);
			int seed = GenerateSeedFromMatchId(match.Id);
			GD.Print($"[NAKAMA] Match joined: {_matchId} | team={localTeamId} | seed={seed}");
			CallDeferred(nameof(DeferredEmitMatchJoined), _matchId, localTeamId, seed);
		}
		catch (Exception ex)
		{
			EmitSignal(SignalName.MatchmakingFailed, ex.Message);
			GD.PrintErr($"[NAKAMA] Join match failed: {ex.Message}");
		}
	}

	private void DeferredEmitMatchJoined(string matchId, int localTeamId, int seed)
	{
		EmitSignal(SignalName.MatchJoined, matchId, localTeamId, seed);
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

		if (duplicateIdentityDetected)
		{
			_ = AbortMatchForDuplicateIdentityAsync("presence event");
		}
	}

	private void OnReceivedMatchState(IMatchState matchState)
	{
		string payload = Encoding.UTF8.GetString(matchState.State);
		EmitSignal(SignalName.MatchStateReceived, (long)matchState.OpCode, payload);
		NetworkCommandRouter.HandleIncomingRelayCommand((long)matchState.OpCode, payload);
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
		var orderedPresences = match.Presences
			.OrderBy(p => p.UserId)
			.ThenBy(p => p.SessionId)
			.ToList();

		if (orderedPresences.Count == 0 && matched != null)
		{
			if (matched.Self?.Presence != null)
				orderedPresences.Add(matched.Self.Presence);

			foreach (var user in matched.Users)
			{
				if (user?.Presence == null)
					continue;

				orderedPresences.Add(user.Presence);
			}

			orderedPresences = orderedPresences
				.OrderBy(p => p.UserId)
				.ThenBy(p => p.SessionId)
				.ToList();
		}

		for (int i = 0; i < orderedPresences.Count; i++)
		{
			if (orderedPresences[i].UserId == _userId)
				return i + 1;
		}

		GD.PrintErr($"[NAKAMA] ResolveLocalTeamId failed: local userId={_userId} not found in presences for match={_matchId}.");
		return 1;
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
		foreach (string arg in OS.GetCmdlineArgs())
		{
			const string prefix = "--nakama-slot=";
			if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return SanitizeDeviceSlot(arg[prefix.Length..]);
		}

		string envSlot = OS.GetEnvironment(EnvDeviceSlotName);
		if (!string.IsNullOrWhiteSpace(envSlot))
			return SanitizeDeviceSlot(envSlot);

		string configuredSlot = GetProjectSetting(ConfigDeviceSlotPath, "");
		return SanitizeDeviceSlot(configuredSlot);
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
		_matchPlayers.Clear();
		EmitSignal(SignalName.MatchmakingFailed, "Deux instances utilisent le meme user Nakama. Lance chaque instance avec un slot different (--nakama-slot=1, --nakama-slot=2).");
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
			if (FileAccess.FileExists(filePath))
			{
				using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
				string existing = file?.GetAsText().Trim() ?? "";
				if (!string.IsNullOrWhiteSpace(existing))
					return existing;
			}

			string deviceId = Guid.NewGuid().ToString("N");
			using var writer = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
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



