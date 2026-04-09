using Godot;

public partial class Unit
{
	private const string SfxBusName = "SFX";
	private const float SupportBattleHornInterval = 10f;
	private const float UnitSfxMaxDistance = 7500f;
	private const float UnitSfxAttenuation = 1.6f;
	private const int SupportHornPoolSize = 12;
	private const int SupportHornLocalMaxSimultaneous = 2;
	private const float SupportHornLocalRadius = 3200f;

	private static readonly string[] InfantrySfxPaths =
	{
		"res://Assets/Units/SonUnits/Infantry/sword-on-flesh.mp3",
		"res://Assets/Units/SonUnits/Infantry/sword-on-flech-extreme.mp3",
		"res://Assets/Units/SonUnits/Infantry/sword-on-shield.mp3",
		"res://Assets/Units/SonUnits/Infantry/sword-on-sword.mp3"
	};

	private const string ArcherSfxPath = "res://Assets/Units/SonUnits/Archer/Arrow-wood-impact.mp3";
	private const string HealerSfxPath = "res://Assets/Units/SonUnits/Healer/Healing-Magic.mp3";
	private const string SupportSfxPath = "res://Assets/Units/SonUnits/Support/War-horn.mp3";

	private static AudioStreamPlayer2D[] _infantrySfxPlayers;
	private static AudioStreamPlayer2D[] _archerSfxPlayers;
	private static AudioStreamPlayer2D[] _healerSfxPlayers;
	private static AudioStreamPlayer2D[] _supportSfxPlayers;

	private static int _infantryRoundRobinIndex;
	private static int _archerRoundRobinIndex;
	private static int _healerRoundRobinIndex;

	private static AudioStream[] _infantrySfxStreams;
	private static AudioStream _archerSfxStream;
	private static AudioStream _healerSfxStream;
	private static AudioStream _supportSfxStream;

	private float _supportBattleHornTimer;

	private void ProcessSupportBattleHorn(double delta)
	{
		if (UnitType != "Support")
			return;

		if (!IsSupportInBattle())
		{
			_supportBattleHornTimer = 0f;
			return;
		}

		_supportBattleHornTimer += (float)delta;
		if (_supportBattleHornTimer < SupportBattleHornInterval)
			return;

		_supportBattleHornTimer = 0f;
		PlaySupportSfx();
	}

	private bool IsSupportInBattle()
	{
		if (_currentState == UnitState.Attacking || _currentState == UnitState.MovingToTarget || _currentState == UnitState.AttackingCamp)
			return true;

		if (_currentTarget != null && IsTargetValid())
			return true;

		return _campTarget != null
			&& IsInstanceValid(_campTarget)
			&& _campTarget.IsInsideTree()
			&& _campTarget.GetTeamId() != TeamId;
	}

	private void PlayAttackSfx()
	{
		switch (UnitType)
		{
			case "Infantry":
				PlayInfantrySfx();
				break;
			case "Range":
				PlayArcherSfx();
				break;
		}
	}

	private void PlayHealerSfx()
	{
		if (!EnsureUnitSfxReady())
			return;

		if (_healerSfxStream == null)
			_healerSfxStream = LoadSfx(HealerSfxPath);

		PlaySfx2D(_healerSfxPlayers, _healerSfxStream, GlobalPosition, interruptIfFull: true, ref _healerRoundRobinIndex);
	}

	private void PlaySupportSfx()
	{
		if (!EnsureUnitSfxReady())
			return;

		if (_supportSfxStream == null)
			_supportSfxStream = LoadSfx(SupportSfxPath);

		PlaySupportHornSfxLocalLimit(GlobalPosition);
	}

	private void PlaySupportHornSfxLocalLimit(Vector2 emitterPosition)
	{
		if (_supportSfxPlayers == null || _supportSfxPlayers.Length == 0 || _supportSfxStream == null)
			return;

		int localPlayingCount = 0;
		for (int i = 0; i < _supportSfxPlayers.Length; i++)
		{
			var player = _supportSfxPlayers[i];
			if (!IsInstanceValid(player) || !player.Playing)
				continue;

			if (player.GlobalPosition.DistanceTo(emitterPosition) <= SupportHornLocalRadius)
				localPlayingCount++;
		}

		if (localPlayingCount >= SupportHornLocalMaxSimultaneous)
			return;

		for (int i = 0; i < _supportSfxPlayers.Length; i++)
		{
			var player = _supportSfxPlayers[i];
			if (!IsInstanceValid(player) || player.Playing)
				continue;

			player.GlobalPosition = emitterPosition;
			player.Stream = _supportSfxStream;
			player.Play();
			return;
		}
	}

	private void PlayInfantrySfx()
	{
		if (!EnsureUnitSfxReady())
			return;

		if (_infantrySfxStreams == null)
		{
			_infantrySfxStreams = new AudioStream[InfantrySfxPaths.Length];
			for (int i = 0; i < InfantrySfxPaths.Length; i++)
				_infantrySfxStreams[i] = LoadSfx(InfantrySfxPaths[i]);
		}

		int availableCount = 0;
		for (int i = 0; i < _infantrySfxStreams.Length; i++)
		{
			if (_infantrySfxStreams[i] != null)
				availableCount++;
		}

		if (availableCount == 0)
			return;

		int targetIndex = (int)(GD.Randi() % (uint)availableCount);
		int currentIndex = 0;
		for (int i = 0; i < _infantrySfxStreams.Length; i++)
		{
			if (_infantrySfxStreams[i] == null)
				continue;

			if (currentIndex == targetIndex)
			{
				PlaySfx2D(_infantrySfxPlayers, _infantrySfxStreams[i], GlobalPosition, interruptIfFull: true, ref _infantryRoundRobinIndex);
				return;
			}

			currentIndex++;
		}
	}

	private void PlayArcherSfx()
	{
		if (!EnsureUnitSfxReady())
			return;

		if (_archerSfxStream == null)
			_archerSfxStream = LoadSfx(ArcherSfxPath);

		PlaySfx2D(_archerSfxPlayers, _archerSfxStream, GlobalPosition, interruptIfFull: true, ref _archerRoundRobinIndex);
	}

	private bool EnsureUnitSfxReady()
	{
		if (AudioSettings.Instance != null && !AudioSettings.Instance.SfxEnabled)
			return false;

		var tree = GetTree();
		var root = tree?.CurrentScene;
		if (root == null)
			return false;

		EnsurePlayerPool(ref _infantrySfxPlayers, 5, "UnitSfxInfantry", root);
		EnsurePlayerPool(ref _archerSfxPlayers, 5, "UnitSfxArcher", root);
		EnsurePlayerPool(ref _healerSfxPlayers, 3, "UnitSfxHealer", root);
		EnsurePlayerPool(ref _supportSfxPlayers, SupportHornPoolSize, "UnitSfxSupport", root);

		return true;
	}

	private static void EnsurePlayerPool(ref AudioStreamPlayer2D[] pool, int count, string namePrefix, Node parent)
	{
		if (pool == null || pool.Length != count)
		{
			pool = new AudioStreamPlayer2D[count];
		}

		for (int i = 0; i < pool.Length; i++)
		{
			if (IsInstanceValid(pool[i]))
				continue;

			pool[i] = CreateSfxPlayer2D($"{namePrefix}_{i}", parent);
		}
	}

	private static AudioStreamPlayer2D CreateSfxPlayer2D(string name, Node parent)
	{
		var player = new AudioStreamPlayer2D();
		player.Name = name;
		player.Bus = SfxBusName;
		player.MaxDistance = UnitSfxMaxDistance;
		player.Attenuation = UnitSfxAttenuation;
		player.MaxPolyphony = 1;
		parent.AddChild(player);
		return player;
	}

	private static AudioStream LoadSfx(string path)
	{
		var stream = GD.Load<AudioStream>(path);
		if (stream == null)
			GD.PushWarning($"Unit SFX not found: {path}");
		return stream;
	}

	private static void PlaySfx2D(AudioStreamPlayer2D[] pool, AudioStream stream, Vector2 emitterPosition, bool interruptIfFull, ref int roundRobinIndex)
	{
		if (pool == null || pool.Length == 0 || stream == null)
			return;

		for (int i = 0; i < pool.Length; i++)
		{
			var candidate = pool[i];
			if (!IsInstanceValid(candidate) || candidate.Playing)
				continue;

			candidate.GlobalPosition = emitterPosition;
			candidate.Stream = stream;
			candidate.Play();
			return;
		}

		if (!interruptIfFull)
			return;

		roundRobinIndex = Mathf.PosMod(roundRobinIndex, pool.Length);
		var selected = pool[roundRobinIndex];
		roundRobinIndex = (roundRobinIndex + 1) % pool.Length;

		if (!IsInstanceValid(selected))
			return;

		selected.Stop();
		selected.GlobalPosition = emitterPosition;
		selected.Stream = stream;
		selected.Play();
	}
}
