using Godot;
using Nakama;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public enum AuthType
{
	Guest,
	Email
}

/// <summary>
/// Persists Nakama session tokens encrypted on disk (never passwords).
/// </summary>
public static class AuthSessionStore
{
	private const string SessionFilePath = "user://nakama_auth_session.dat";
	private const string DeviceKeyFilePath = "user://device_key.txt";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	public static bool HasStoredSession => Godot.FileAccess.FileExists(SessionFilePath);

	public static void Save(ISession session, AuthType authType)
	{
		if (session == null
			|| string.IsNullOrWhiteSpace(session.AuthToken)
			|| string.IsNullOrWhiteSpace(session.RefreshToken))
		{
			GD.PrintErr("[AUTH] Refusing to save session: missing tokens.");
			return;
		}

		var payload = new SessionPayload
		{
			AuthToken = session.AuthToken,
			RefreshToken = session.RefreshToken,
			AuthType = authType == AuthType.Email ? "email" : "guest"
		};

		string json = JsonSerializer.Serialize(payload, JsonOptions);
		if (!TryWriteEncrypted(json))
			GD.PrintErr("[AUTH] Failed to write encrypted session file.");
	}

	public static bool TryLoad(out ISession session, out AuthType authType)
	{
		session = null;
		authType = AuthType.Guest;

		if (!HasStoredSession)
			return false;

		if (!TryReadEncrypted(out string json))
		{
			Clear();
			return false;
		}

		SessionPayload payload;
		try
		{
			payload = JsonSerializer.Deserialize<SessionPayload>(json, JsonOptions);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[AUTH] Corrupt session file: {ex.Message}");
			Clear();
			return false;
		}

		if (payload == null
			|| string.IsNullOrWhiteSpace(payload.AuthToken)
			|| string.IsNullOrWhiteSpace(payload.RefreshToken))
		{
			Clear();
			return false;
		}

		authType = string.Equals(payload.AuthType, "email", StringComparison.OrdinalIgnoreCase)
			? AuthType.Email
			: AuthType.Guest;

		session = Session.Restore(payload.AuthToken, payload.RefreshToken);
		return session != null;
	}

	public static void Clear()
	{
		if (Godot.FileAccess.FileExists(SessionFilePath))
		{
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SessionFilePath));
		}
	}

	private static bool TryWriteEncrypted(string plaintext)
	{
		string passphrase = GetEncryptionPassphrase();
		if (string.IsNullOrEmpty(passphrase))
			return false;

		using var file = Godot.FileAccess.OpenEncryptedWithPass(
			SessionFilePath,
			Godot.FileAccess.ModeFlags.Write,
			passphrase);

		if (file == null)
			return false;

		file.StoreString(plaintext);
		return true;
	}

	private static bool TryReadEncrypted(out string plaintext)
	{
		plaintext = "";
		string passphrase = GetEncryptionPassphrase();
		if (string.IsNullOrEmpty(passphrase))
			return false;

		if (!Godot.FileAccess.FileExists(SessionFilePath))
			return false;

		using var file = Godot.FileAccess.OpenEncryptedWithPass(
			SessionFilePath,
			Godot.FileAccess.ModeFlags.Read,
			passphrase);

		if (file == null)
			return false;

		plaintext = file.GetAsText();
		return !string.IsNullOrWhiteSpace(plaintext);
	}

	private static string GetEncryptionPassphrase()
	{
		string uniqueId = OS.GetUniqueId();
		if (!string.IsNullOrWhiteSpace(uniqueId))
			return HashPassphrase(uniqueId);

		string deviceKey = LoadOrCreateDeviceKey();
		if (string.IsNullOrWhiteSpace(deviceKey))
			return "";

		return HashPassphrase(deviceKey);
	}

	private static string HashPassphrase(string material)
	{
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material.Trim()));
		return Convert.ToHexString(hash);
	}

	private static string LoadOrCreateDeviceKey()
	{
		try
		{
			if (Godot.FileAccess.FileExists(DeviceKeyFilePath))
			{
				using var read = Godot.FileAccess.Open(DeviceKeyFilePath, Godot.FileAccess.ModeFlags.Read);
				string existing = read?.GetAsText().Trim() ?? "";
				if (!string.IsNullOrWhiteSpace(existing))
					return existing;
			}

			string guid = Guid.NewGuid().ToString("N");
			using var write = Godot.FileAccess.Open(DeviceKeyFilePath, Godot.FileAccess.ModeFlags.Write);
			write?.StoreString(guid);
			return guid;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[AUTH] device_key.txt error: {ex.Message}");
			return "";
		}
	}

	private sealed class SessionPayload
	{
		public string AuthToken { get; set; } = "";
		public string RefreshToken { get; set; } = "";
		public string AuthType { get; set; } = "guest";
	}
}
