namespace Application.Dtos.Deposits;

public record DepositSummaryDto(
    Guid Id,
    string Address,
    decimal Latitude,
    decimal Longitude,
    bool Active,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
