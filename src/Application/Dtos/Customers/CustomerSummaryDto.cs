namespace Application.Dtos.Customers;

public record CustomerSummaryDto(
    Guid Id,
    string Name,
    string Surname,
    string? Email,
    string? PhoneNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
