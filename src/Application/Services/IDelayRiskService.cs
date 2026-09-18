namespace Application.Services;

public interface IDelayRiskService
{
    Task ExecuteRecalculationAsync(CancellationToken cancellationToken = default);
}