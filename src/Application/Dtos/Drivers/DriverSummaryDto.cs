namespace Application.Dtos.Drivers;

public record DriverSummaryDto(
    Guid Id,
    string Name,
    string Surname,
    string? Email,
    string? PhoneNumber);
