using VideoAnalysis.Core.Dtos;

namespace VideoAnalysis.Core.Abstractions;

public interface IProjectSetupService
{
    Task<CreateProjectResultDto> CreateProjectWithVideoAsync(
        CreateProjectRequestDto request,
        CancellationToken cancellationToken);
}
