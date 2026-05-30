using Godot;
using System.Collections.Generic;

public partial class KeybindingsManager : Node
{
	public static KeybindingsManager Instance { get; private set; }

	public static readonly string[] UnitTypes =
		{ "Infantry", "Support", "Range", "Heal", "AntiArmor", "Mortar", "Heavy", "Tank"};
	public static readonly string[] ShipTypes =
		{ "Transport", "Fregate", "Destroyer"};
	public static readonly string[] UltimateActions =
		{ "ultimate_heal", "ultimate_support", "ultimate_cancel"};
	public const string AllOwnedUnitsAction = "unit_macro_AllOwned";

	private static readonly Key[] DefaultUnitKeys =
		{ Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8 };
	private static readonly Key[] DefaultShipKeys =
		{ Key.Key9, Key.Key0, Key.Minus };
	private static readonly Key[] DefaultUltimateKeys =
		{ Key.Key1, Key.Key2, Key.Escape };
	private const Key DefaultAllOwnedUnitsKey = Key.A;

	private const string SavePath = "user://keybindings.cfg";
	private const string UnitSection = "UnitMacros";
	private const string ShipSection = "ShipMacros";
	private const string UltimateSection = "Ultimates";

	private readonly Dictionary<string, Key> _unitBindings = new();
	private readonly Dictionary<string, Key> _shipBindings = new();
	private readonly Dictionary<string, Key> _ultimateBindings = new();
	private Key _allOwnedUnitsKey = DefaultAllOwnedUnitsKey;

	public override void _Ready()
	{
		Instance = this;

		for (int i = 0; i < UnitTypes.Length; i++)
			_unitBindings[UnitTypes[i]] = DefaultUnitKeys[i];
		for (int i = 0; i < ShipTypes.Length; i++)
			_shipBindings[ShipTypes[i]] = DefaultShipKeys[i];
		for (int i = 0; i < UltimateActions.Length; i++)
			_ultimateBindings[UltimateActions[i]] = DefaultUltimateKeys[i];
		_allOwnedUnitsKey = DefaultAllOwnedUnitsKey;

		LoadFromDisk();
		ApplyToInputMap();
	}

	private void LoadFromDisk()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(SavePath) != Error.Ok) return;
		foreach (string t in UnitTypes)
			if (cfg.HasSectionKey(UnitSection, t))
				_unitBindings[t] = (Key)(int)cfg.GetValue(UnitSection, t);
		foreach (string t in ShipTypes)
			if (cfg.HasSectionKey(ShipSection, t))
				_shipBindings[t] = (Key)(int)cfg.GetValue(ShipSection, t);
		if (cfg.HasSectionKey(UnitSection, "AllOwned"))
			_allOwnedUnitsKey = (Key)(int)cfg.GetValue(UnitSection, "AllOwned");
		foreach (string action in UltimateActions)
			if (cfg.HasSectionKey(UltimateSection, action))
				_ultimateBindings[action] = (Key)(int)cfg.GetValue(UltimateSection, action);
	}

	private void SaveToDisk()
	{
		var cfg = new ConfigFile();
		foreach (string t in UnitTypes)
			cfg.SetValue(UnitSection, t, (int)_unitBindings[t]);
		cfg.SetValue(UnitSection, "AllOwned", (int)_allOwnedUnitsKey);
		foreach (string t in ShipTypes)
			cfg.SetValue(ShipSection, t, (int)_shipBindings[t]);
		foreach (string action in UltimateActions)
			cfg.SetValue(UltimateSection, action, (int)_ultimateBindings[action]);
		cfg.Save(SavePath);
	}

	public void ApplyToInputMap()
	{
		foreach (string t in UnitTypes)
			RegisterAction($"unit_macro_{t}", _unitBindings[t]);
		RegisterAction(AllOwnedUnitsAction, _allOwnedUnitsKey);
		foreach (string t in ShipTypes)
			RegisterAction($"ship_macro_{t}", _shipBindings[t]);
		foreach (string action in UltimateActions)
			RegisterAction(action, _ultimateBindings[action]);
	}

	private static void RegisterAction(string action, Key key)
	{
		if (!InputMap.HasAction(action))
			InputMap.AddAction(action);
		else
			InputMap.ActionEraseEvents(action);
		InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
	}

	// API action-name unifiée (ex : "unit_macro_Infantry", "ship_macro_Transport")
	public Key GetBindingForAction(string action)
	{
		if (action.StartsWith("unit_macro_") && _unitBindings.TryGetValue(action[11..], out Key uk))
			return uk;
		if (action == AllOwnedUnitsAction)
			return _allOwnedUnitsKey;
		if (action.StartsWith("ship_macro_") && _shipBindings.TryGetValue(action[11..], out Key sk))
			return sk;
		if (action.StartsWith("ultimate_") && _ultimateBindings.TryGetValue(action, out Key uak))
			return uak;
		return Key.None;
	}

	public void SetBindingForAction(string action, Key key)
	{
		if (action.StartsWith("unit_macro_"))
		{
			string t = action[11..];
			if (_unitBindings.ContainsKey(t)) _unitBindings[t] = key;
		}
		else if (action == AllOwnedUnitsAction)
		{
			_allOwnedUnitsKey = key;
		}
		else if (action.StartsWith("ship_macro_"))
		{
			string t = action[11..];
			if (_shipBindings.ContainsKey(t)) _shipBindings[t] = key;
		}
		else if (action.StartsWith("ultimate_"))
		{
			if (_ultimateBindings.ContainsKey(action)) _ultimateBindings[action] = key;
		}
		SaveToDisk();
		ApplyToInputMap();
	}

	public static string KeyDisplayName(Key key) => key switch
	{
		Key.Minus      => "-",
		Key.Equal      => "=",
		Key.Semicolon  => ";",
		Key.Apostrophe => "'",
		Key.Comma      => ",",
		Key.Period     => ".",
		Key.Slash      => "/",
		Key.Backslash  => "\\",
		Key.Space      => "Espace",
		_ => key.ToString().StartsWith("Key") ? key.ToString()[3..] : key.ToString()
	};
}
