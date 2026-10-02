namespace Application.Dtos.RoutePlanning;

/// <summary>
/// Explicit driver and vehicle choice for a planning preview, plus an optional deposit.
/// The planner never infers the vehicle: the caller decides which resources take part.
/// </summary>
public class DriverSelectionRequest
{
    public Guid DriverId { get; init; }

    public Guid VehicleId { get; init; }

    /// <summary>
    /// Overrides the deposit for this driver. When it is left null the planner picks the
    /// nearest active deposit to the driver's current location. An empty value is rejected
    /// by the validator: omitting the field means "you decide", not "there is no deposit".
    /// </summary>
    public Guid? DepositId { get; init; }
}
