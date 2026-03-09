namespace VideoAnalysis.Core.Dtos;

public sealed record YandexS3Options(
    string ServiceUrl,
    string Bucket,
    string AccessKey,
    string SecretKey,
    string Region,
    string? Prefix);
