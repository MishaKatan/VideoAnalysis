using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Infrastructure.Services;

namespace VideoAnalysis.Tests;

public sealed class AnnotationRenderServiceTests : IDisposable
{
    private readonly string _workingDirectory;

    public AnnotationRenderServiceTests()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), "video-analysis-annotation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDirectory);
    }

    [Fact]
    public async Task BuildOverlayFilterScriptAsync_IncludesEventOverlayData()
    {
        var service = new AnnotationRenderService();
        IReadOnlyList<ClipSegmentDto> segments =
        [
            new ClipSegmentDto(
                Guid.NewGuid(),
                100,
                160,
                "Гол",
                "Иванов",
                TeamSide.Home,
                "Молот",
                "P2",
                "00:04",
                "#E53935",
                "1/3")
        ];

        var scriptPath = await service.BuildOverlayFilterScriptAsync([], segments, 25, _workingDirectory, CancellationToken.None);

        Assert.NotNull(scriptPath);
        var script = await File.ReadAllTextAsync(scriptPath!, CancellationToken.None);
        Assert.Contains("ГОЛ", script, StringComparison.Ordinal);
        Assert.Contains("Молот", script, StringComparison.Ordinal);
        Assert.Contains("Иванов", script, StringComparison.Ordinal);
        Assert.Contains("P2", script, StringComparison.Ordinal);
        Assert.Contains("00\\:04", script, StringComparison.Ordinal);
        Assert.Contains("1/3", script, StringComparison.Ordinal);
        Assert.Contains("E53935", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("drawtext", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildOverlayFilterScriptAsync_WritesUtf8WithoutBom()
    {
        var service = new AnnotationRenderService();
        IReadOnlyList<ClipSegmentDto> segments =
        [
            new ClipSegmentDto(
                Guid.NewGuid(),
                100,
                160,
                "Гол",
                "Иванов",
                TeamSide.Home,
                "Молот",
                "P2",
                "00:04",
                "#E53935",
                "1/1")
        ];

        var scriptPath = await service.BuildOverlayFilterScriptAsync([], segments, 25, _workingDirectory, CancellationToken.None);

        Assert.NotNull(scriptPath);
        var bytes = await File.ReadAllBytesAsync(scriptPath!, CancellationToken.None);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, true);
        }
    }
}
