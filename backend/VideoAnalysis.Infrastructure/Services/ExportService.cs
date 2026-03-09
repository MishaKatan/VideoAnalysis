using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;

namespace VideoAnalysis.Infrastructure.Services;

public sealed class ExportService : IExportService
{
    private readonly IClipComposerService _clipComposerService;
    private readonly IAnnotationRenderService _annotationRenderService;

    public ExportService(IClipComposerService clipComposerService, IAnnotationRenderService annotationRenderService)
    {
        _clipComposerService = clipComposerService;
        _annotationRenderService = annotationRenderService;
    }

    public async Task<ExportResultDto> ExportAsync(ExportRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "video-analysis", "export", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var overlayScript = await _annotationRenderService.BuildOverlayFilterScriptAsync(
                request.Annotations,
                request.FramesPerSecond,
                tempRoot,
                cancellationToken);

            var outputPath = await _clipComposerService.ComposeAsync(
                request.SourceVideoPath,
                request.Segments,
                request.OutputPath,
                request.FramesPerSecond,
                overlayScript,
                cancellationToken);

            string? objectKey = null;
            string? remoteUrl = null;

            if (request.UploadToCloud)
            {
                if (request.Yandex is null)
                {
                    throw new InvalidOperationException("Yandex options are required for cloud export.");
                }

                objectKey = await UploadToYandexAsync(outputPath, request.Yandex, cancellationToken);
                remoteUrl = BuildRemoteUrl(request.Yandex.ServiceUrl, request.Yandex.Bucket, objectKey);
            }

            return new ExportResultDto(true, outputPath, objectKey, remoteUrl, null);
        }
        catch (Exception ex)
        {
            return new ExportResultDto(false, request.OutputPath, null, null, ex.Message);
        }
    }

    private static async Task<string> UploadToYandexAsync(string outputPath, YandexS3Options options, CancellationToken cancellationToken)
    {
        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,
            SignatureVersion = "4",
            AuthenticationRegion = string.IsNullOrWhiteSpace(options.Region) ? "ru-central1" : options.Region
        };

        var objectKey = BuildObjectKey(options.Prefix, Path.GetFileName(outputPath));
        using var client = new AmazonS3Client(credentials, config);
        using var stream = File.OpenRead(outputPath);

        var putRequest = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = "video/mp4"
        };

        await client.PutObjectAsync(putRequest, cancellationToken);
        return objectKey;
    }

    private static string BuildObjectKey(string? prefix, string fileName)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return fileName;
        }

        return $"{prefix.Trim().TrimEnd('/')}/{fileName}";
    }

    private static string BuildRemoteUrl(string serviceUrl, string bucket, string objectKey)
    {
        return $"{serviceUrl.TrimEnd('/')}/{bucket}/{objectKey}";
    }
}
