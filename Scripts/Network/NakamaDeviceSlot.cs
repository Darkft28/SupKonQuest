using Godot;
using System;
using System.Text;

/// <summary>
/// Resolves --nakama-slot / env / project setting for per-instance user:// files.
/// </summary>
public static class NakamaDeviceSlot
{
	private const string ConfigDeviceSlotPath = "nakama/device_slot";
	private const string EnvDeviceSlotName = "SUPKONQUEST_NAKAMA_SLOT";

	public static string Resolve()
	{
		string fromCommandLine = ReadFromCommandLine();
		if (!string.IsNullOrWhiteSpace(fromCommandLine))
			return fromCommandLine;

		string envSlot = OS.GetEnvironment(EnvDeviceSlotName);
		if (!string.IsNullOrWhiteSpace(envSlot))
			return Sanitize(envSlot);

		string configuredSlot = ProjectSettings.GetSetting(ConfigDeviceSlotPath, "").AsString();
		return Sanitize(configuredSlot);
	}

	public static string SlottedUserFile(string baseName, string extension)
	{
		string slot = Resolve();
		if (string.IsNullOrWhiteSpace(slot))
			return $"user://{baseName}{extension}";

		return $"user://{baseName}_{slot}{extension}";
	}

	private static string ReadFromCommandLine()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			string slot = TryParseArgument(arg);
			if (!string.IsNullOrWhiteSpace(slot))
				return slot;
		}

		string[] args = OS.GetCmdlineArgs();
		for (int i = 0; i < args.Length; i++)
		{
			string slot = TryParseArgument(args[i]);
			if (!string.IsNullOrWhiteSpace(slot))
				return slot;

			if (IsSlotFlag(args[i]) && i + 1 < args.Length)
			{
				slot = Sanitize(args[i + 1]);
				if (!string.IsNullOrWhiteSpace(slot))
					return slot;
			}
		}

		return "";
	}

	private static bool IsSlotFlag(string arg) =>
		string.Equals(arg, "--nakama-slot", StringComparison.OrdinalIgnoreCase);

	private static string TryParseArgument(string arg)
	{
		if (string.IsNullOrWhiteSpace(arg))
			return "";

		const string prefix = "--nakama-slot=";
		if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			return Sanitize(arg[prefix.Length..]);

		return "";
	}

	private static string Sanitize(string value)
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
}
