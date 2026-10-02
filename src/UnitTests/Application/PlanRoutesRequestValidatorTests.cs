using Application.Dtos.RoutePlanning;
using Application.Validators;

namespace UnitTests.Application;

public class PlanRoutesRequestValidatorTests
{
    private readonly PlanRoutesRequestValidator _validator = new();

    private static PlanRoutesRequest Request(IReadOnlyList<DriverSelectionRequest>? selections = null) => new()
    {
        DriverSelections = selections ??
        [
            new DriverSelectionRequest
            {
                DriverId = Guid.NewGuid(),
                VehicleId = Guid.NewGuid(),
                DepositId = Guid.NewGuid()
            }
        ]
    };

    private static DriverSelectionRequest Selection(
        Guid? driverId = null,
        Guid? vehicleId = null,
        Guid? depositId = null,
        bool withDeposit = true) => new()
    {
        DriverId = driverId ?? Guid.NewGuid(),
        VehicleId = vehicleId ?? Guid.NewGuid(),
        DepositId = withDeposit ? depositId ?? Guid.NewGuid() : null
    };

    [Fact]
    public void Validate_CompleteRequest_Passes()
    {
        Assert.True(_validator.Validate(Request()).IsValid);
    }

    [Fact]
    public void Validate_NoDriverSelections_Fails()
    {
        var result = _validator.Validate(Request(selections: []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PlanRoutesRequest.DriverSelections));
    }

    [Fact]
    public void Validate_DuplicatedDriver_Fails()
    {
        var driverId = Guid.NewGuid();

        var result = _validator.Validate(Request(
            [Selection(driverId: driverId), Selection(driverId: driverId)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Each driver can only be selected once.");
    }

    [Fact]
    public void Validate_VehicleAssignedToTwoDrivers_Fails()
    {
        var vehicleId = Guid.NewGuid();

        var result = _validator.Validate(Request(
            [Selection(vehicleId: vehicleId), Selection(vehicleId: vehicleId)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Each vehicle can only be assigned to one driver.");
    }

    [Fact]
    public void Validate_SharedDepositAcrossDrivers_Passes()
    {
        var depositId = Guid.NewGuid();

        var result = _validator.Validate(Request(
            [Selection(depositId: depositId), Selection(depositId: depositId)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithoutDeposit_Passes()
    {
        var result = _validator.Validate(Request(
            [Selection(withDeposit: false), Selection(withDeposit: false)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyDeposit_Fails()
    {
        var result = _validator.Validate(Request(
            [Selection(depositId: Guid.Empty)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "The deposit must be a real deposit id or omitted.");
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Validate_EmptyIdentifiers_Fails(bool emptyDriver, bool emptyVehicle, bool emptyDeposit)
    {
        var request = Request(
            [Selection(
                driverId: emptyDriver ? Guid.Empty : null,
                vehicleId: emptyVehicle ? Guid.Empty : null,
                depositId: emptyDeposit ? Guid.Empty : null)]);

        Assert.False(_validator.Validate(request).IsValid);
    }
}
