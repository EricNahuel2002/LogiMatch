using Domain.Enums;

namespace Application.Dtos.Orders;

public record OrderItemDto(
    Guid OrderItemId,
    string Name,
    decimal Price,
    int Quantity,
    decimal WeightKg);

public record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    Guid? AssignedDriverId,
    DateTime? DeliveryWindowStartAt,
    DateTime? DeliveryWindowEndAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record OrderDetailsDto(
    Guid Id,
    Guid CustomerId,
    Guid? AssignedDriverId,
    DateTime? DeliveryWindowStartAt,
    DateTime? DeliveryWindowEndAt,
    OrderItemDto[] Items,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
