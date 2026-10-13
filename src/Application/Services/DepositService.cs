using Application.Dtos.Deposits;
using Application.Persistence;

namespace Application.Services;

public interface IDepositService
{
    Task<IReadOnlyList<DepositSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public class DepositService : IDepositService
{
    private readonly IDepositRepository _deposits;

    public DepositService(IDepositRepository deposits)
    {
        _deposits = deposits;
    }

    public async Task<IReadOnlyList<DepositSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var deposits = await _deposits.GetAllAsync(cancellationToken);
        return deposits
            .Select(d => new DepositSummaryDto(
                d.Id,
                d.Address,
                d.Coordinate.Latitude,
                d.Coordinate.Longitude,
                d.Active,
                d.CreatedAt,
                d.UpdatedAt))
            .ToList();
    }
}
