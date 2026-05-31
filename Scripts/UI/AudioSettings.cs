using Godot;

public partial class AudioSettings : Node
{
	private const string MenuMusicPath = "res://Assets/Menu/Son/MainTheme.mp3";
	private const string SfxBusName = "SFX";

	public static AudioSettings Instance { get; private set; }

	[Signal]
	public delegate void AudioSettingsChangedEventHandler();

	public bool MusicEnabled { get; private set; } = true;
	public bool SfxEnabled { get; private set; } = true;

	public AudioStreamPlayer MusicPlayer { get; private set; }
	public AudioStreamPlayer SfxPlayer { get; private set; }

	public override void _Ready()
	{
		if (Instance == null)
			Instance = this;

		ProcessMode = ProcessModeEnum.Always;
		EnsureAudioNodes();
		EnsureSfxBus();
		EnsureMenuMusicStream();
		ApplyAudioState();
		EnsureMenuMusicPlaying();
	}

	private void EnsureAudioNodes()
	{
		MusicPlayer = GetNodeOrNull<AudioStreamPlayer>("MusicPlayer");
		if (MusicPlayer == null)
		{
			MusicPlayer = new AudioStreamPlayer();
			MusicPlayer.Name = "MusicPlayer";
			AddChild(MusicPlayer);
		}

		SfxPlayer = GetNodeOrNull<AudioStreamPlayer>("SfxPlayer");
		if (SfxPlayer == null)
		{
			SfxPlayer = new AudioStreamPlayer();
			SfxPlayer.Name = "SfxPlayer";
			AddChild(SfxPlayer);
		}

		SfxPlayer.Bus = SfxBusName;
	}

	public void ToggleMusic()
	{
		MusicEnabled = !MusicEnabled;
		ApplyAudioState();
		EmitSignal(SignalName.AudioSettingsChanged);
	}

	public void ToggleSfx()
	{
		SfxEnabled = !SfxEnabled;
		ApplyAudioState();
		EmitSignal(SignalName.AudioSettingsChanged);
	}

	public void SetMusicEnabled(bool enabled)
	{
		MusicEnabled = enabled;
		ApplyAudioState();
		EmitSignal(SignalName.AudioSettingsChanged);
	}

	public void SetSfxEnabled(bool enabled)
	{
		SfxEnabled = enabled;
		ApplyAudioState();
		EmitSignal(SignalName.AudioSettingsChanged);
	}

	private void ApplyAudioState()
	{
		if (MusicPlayer != null)
		{
			MusicPlayer.StreamPaused = !MusicEnabled;
			MusicPlayer.VolumeDb = MusicEnabled ? 0f : -80f;

			if (!MusicEnabled && MusicPlayer.Playing)
				MusicPlayer.Stop();

			if (MusicEnabled && MusicPlayer.Stream != null && !MusicPlayer.Playing)
				MusicPlayer.Play();
		}

		if (SfxPlayer != null)
		{
			SfxPlayer.StreamPaused = !SfxEnabled;
			SfxPlayer.VolumeDb = SfxEnabled ? 0f : -80f;
		}

		int sfxBusIndex = AudioServer.GetBusIndex(SfxBusName);
		if (sfxBusIndex >= 0)
			AudioServer.SetBusMute(sfxBusIndex, !SfxEnabled);
	}

	private void EnsureSfxBus()
	{
		int sfxBusIndex = AudioServer.GetBusIndex(SfxBusName);
		if (sfxBusIndex >= 0)
			return;

		int busCount = AudioServer.GetBusCount();
		AudioServer.AddBus(busCount);
		AudioServer.SetBusName(busCount, SfxBusName);
		AudioServer.SetBusSend(busCount, "Master");
	}

	private void EnsureMenuMusicStream()
	{
		if (MusicPlayer == null || MusicPlayer.Stream != null)
			return;

		var stream = GD.Load<AudioStream>(MenuMusicPath);
		if (stream == null)
		{
			GD.PushWarning($"AudioSettings: menu music not found at {MenuMusicPath}");
			return;
		}

		MusicPlayer.Stream = stream;
		MusicPlayer.Autoplay = false;
		MusicPlayer.Bus = "Master";
	}

	public void EnsureMenuMusicPlaying()
	{
		EnsureAudioNodes();
		EnsureMenuMusicStream();

		if (!MusicEnabled || MusicPlayer?.Stream == null)
			return;

		if (!MusicPlayer.Playing)
			MusicPlayer.Play();
	}

	public void StopMenuMusic()
	{
		if (MusicPlayer?.Playing == true)
			MusicPlayer.Stop();
	}
}
