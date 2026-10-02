using Domain.Common;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Deposit : BaseEntity
{
    private Deposit()
    {
    }

    public string Address { get; private set; } = string.Empty;
    public Coordinate Coordinate { get; private set; } = new(0, 0);

    /// <summary>
    /// An inactive deposit cannot take part in a route plan: the planner never assigns one and
    /// an explicit selection is rejected, which is how a closed or out-of-service site is kept
    /// out of the proposal without deleting its history.
    /// </summary>
    public bool Active { get; private set; } = true;

    public static Deposit Create(string address, Coordinate coordinate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentNullException.ThrowIfNull(coordinate);

        if (address.Length > 200)
        {
            throw new ArgumentException("Address cannot be longer than 200 characters.", nameof(address));
        }

        if (coordinate.Latitude is < -90m or > 90m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinate),
                "The latitude must be between -90 and 90.");
        }

        if (coordinate.Longitude is < -180m or > 180m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinate),
                "The longitude must be between -180 and 180.");
        }

        return new Deposit
        {
            Address = address,
            Coordinate = coordinate
        };
    }

    public void SetActive(bool active) => Active = active;
}
