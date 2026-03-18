using LibVLCSharp.Shared;
using System.Runtime.InteropServices;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Media;

public sealed class LibVlcMediaPlaybackService : IMediaPlaybackService, IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private LibVLCSharp.Shared.Media? _currentMedia;
    private IntPtr _preferredVideoHandle;
    private bool _disposed;

    public LibVlcMediaPlaybackService()
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);

        _mediaPlayer.TimeChanged += OnTimeChanged;
        _mediaPlayer.LengthChanged += OnLengthChanged;
        _mediaPlayer.Playing += (_, _) => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.Paused += (_, _) => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.Stopped += (_, _) => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler<long>? FrameChanged;

    public bool IsPlaying => _mediaPlayer.IsPlaying;
    public bool IsMuted => _mediaPlayer.Mute;
    public long CurrentFrame { get; private set; }
    public long DurationFrames { get; private set; }
    public double FramesPerSecond { get; private set; } = 30d;
    public int Volume => _mediaPlayer.Volume;
    public MediaPlayer MediaPlayer => _mediaPlayer;

    public Task<MediaMetadata> OpenAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Video file not found.", filePath);
        }

        _currentMedia?.Dispose();
        _currentMedia = new LibVLCSharp.Shared.Media(_libVlc, new Uri(filePath));
        var media = _currentMedia;
        media.Parse(MediaParseOptions.ParseLocal);

        if (media.Tracks is { Length: > 0 } tracks)
        {
            foreach (var track in tracks)
            {
                if (track.TrackType != TrackType.Video)
                {
                    continue;
                }

                if (track.Data.Video.FrameRateDen > 0 && track.Data.Video.FrameRateNum > 0)
                {
                    FramesPerSecond = (double)track.Data.Video.FrameRateNum / track.Data.Video.FrameRateDen;
                }

                break;
            }
        }

        _mediaPlayer.Media = media;
        UpdateDuration(media.Duration);
        CurrentFrame = 0;

        return Task.FromResult(new MediaMetadata(filePath, FramesPerSecond, DurationFrames, 0, 0));
    }

    public void Play()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            _mediaPlayer.Hwnd == IntPtr.Zero &&
            _preferredVideoHandle != IntPtr.Zero)
        {
            _mediaPlayer.Hwnd = _preferredVideoHandle;
        }

        _mediaPlayer.Play();
    }
    public void Pause() => _mediaPlayer.Pause();

    public void SeekToFrame(long frame)
    {
        var safeFrame = Math.Max(0, Math.Min(frame, DurationFrames));
        var milliseconds = (long)Math.Round((safeFrame / FramesPerSecond) * 1000d);
        _mediaPlayer.Time = milliseconds;
        CurrentFrame = safeFrame;
        FrameChanged?.Invoke(this, CurrentFrame);
    }

    public void StepFrameForward() => SeekToFrame(CurrentFrame + 1);
    public void StepFrameBackward() => SeekToFrame(CurrentFrame - 1);
    public void SetVolume(int volume) => _mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
    public void ToggleMute() => _mediaPlayer.Mute = !_mediaPlayer.Mute;

    public void SetVideoOutputHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        _preferredVideoHandle = handle;
        _mediaPlayer.Hwnd = handle;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mediaPlayer.TimeChanged -= OnTimeChanged;
        _mediaPlayer.LengthChanged -= OnLengthChanged;
        _currentMedia?.Dispose();
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
        _disposed = true;
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs args)
    {
        var frame = (long)Math.Round((args.Time / 1000d) * FramesPerSecond);
        if (frame == CurrentFrame)
        {
            return;
        }

        CurrentFrame = frame;
        FrameChanged?.Invoke(this, CurrentFrame);
    }

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs args)
    {
        UpdateDuration(args.Length);
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateDuration(long durationMilliseconds)
    {
        DurationFrames = Math.Max(1, (long)Math.Round((Math.Max(0, durationMilliseconds) / 1000d) * FramesPerSecond));
    }
}
