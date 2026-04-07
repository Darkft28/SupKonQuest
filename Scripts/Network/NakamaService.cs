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
			RefreshMatchPlayers(match);
			int localTeamId = ResolveLocalTeamId(match);
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
		foreach (var joined in matchPresence.Joins)
		{
			_matchPlayers[joined.UserId] = string.IsNullOrWhiteSpace(joined.Username) ? joined.UserId : joined.Username;
		}

		foreach (var left in matchPresence.Leaves)
		{
			_matchPlayers.Remove(left.UserId);
		}
	}

	private void OnReceivedMatchState(IMatchState matchState)
	{
		string payload = Encoding.UTF8.GetString(matchState.State);
		EmitSignal(SignalName.MatchStateReceived, (long)matchState.OpCode, payload);
		NetworkCommandRouter.HandleIncomingRelayCommand((long)matchState.OpCode, payload);
	}

	private void RefreshMatchPlayers(IMatch match)
	{
		_matchPlayers.Clear();
		foreach (var presence in match.Presences.OrderBy(p => p.Username).ThenBy(p => p.UserId))
		{
			_matchPlayers[presence.UserId] = string.IsNullOrWhiteSpace(presence.Username)
				? presence.UserId
				: presence.Username;
		}
	}

	private int ResolveLocalTeamId(IMatch match)
	{
		var orderedPresences = match.Presences.OrderBy(p => p.Username).ThenBy(p => p.UserId).ToList();
		for (int i = 0; i < orderedPresences.Count; i++)
		{
			if (orderedPresences[i].UserId == _userId)
				return i == 0 ? 1 : 2;
		}

		return 1;
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
		try
		{
			if (FileAccess.FileExists(DeviceIdFilePath))
			{
				using var file = FileAccess.Open(DeviceIdFilePath, FileAccess.ModeFlags.Read);
				string existing = file?.GetAsText().Trim() ?? "";
				if (!string.IsNullOrWhiteSpace(existing))
					return existing;
			}

			string deviceId = Guid.NewGuid().ToString("N");
			using var writer = FileAccess.Open(DeviceIdFilePath, FileAccess.ModeFlags.Write);
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



