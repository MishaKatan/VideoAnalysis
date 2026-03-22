using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Core.Abstractions;

public interface IMediaPlaybackService
{
    event EventHandler? PlaybackStateChanged;
    event EventHandler<long>? FrameChanged;

    bool IsPlaying { get; }
    bool IsMuted { get; }
    long CurrentFrame { get; }
    long DurationFrames { get; }
    double FramesPerSecond { get; }
    int Volume { get; }
    double PlaybackRate { get; }

    Task<MediaMetadata> OpenAsync(string filePath, CancellationToken cancellationToken);
    void Play();
    void Pause();
    void SeekToFrame(long frame);
    void StepFrameForward();
    void StepFrameBackward();
    void SetVolume(int volume);
    void SetPlaybackRate(double playbackRate);
    void ToggleMute();
}
