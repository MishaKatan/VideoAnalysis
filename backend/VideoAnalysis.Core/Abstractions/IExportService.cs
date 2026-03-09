using VideoAnalysis.Core.Dtos;

namespace VideoAnalysis.Core.Abstractions;

public interface IExportService
{
    Task<ExportResultDto> ExportAsync(ExportRequestDto request, CancellationToken cancellationToken);
}
