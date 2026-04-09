using Godot;

public partial class AudioSettings : Node
{
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
		ApplyAudioState();
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
		}

		if (SfxPlayer != null)
		{
			SfxPlayer.StreamPaused = !SfxEnabled;
			SfxPlayer.VolumeDb = SfxEnabled ? 0f : -80f;
		}
	}
}
