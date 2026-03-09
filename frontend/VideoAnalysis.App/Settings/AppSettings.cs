namespace VideoAnalysis.App.Settings;

public sealed class AppSettings
{
    public string FfmpegPath { get; init; } = "ffmpeg";
    public string YandexServiceUrl { get; init; } = "https://storage.yandexcloud.net";
    public string YandexBucket { get; init; } = string.Empty;
    public string YandexAccessKey { get; init; } = string.Empty;
    public string YandexSecretKey { get; init; } = string.Empty;
    public string YandexRegion { get; init; } = "ru-central1";
    public string YandexPrefix { get; init; } = "exports";
}
