using Application.Dtos.Customers;
using Application.Persistence;

namespace Application.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public class CustomerService : ICustomerService
{
    private readonly IUserRepository _users;

    public CustomerService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<IReadOnlyList<CustomerSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _users.GetAllCustomersAsync(cancellationToken);
        return customers
            .Select(c => new CustomerSummaryDto(c.Id, c.Name, c.Surname, c.Email, c.PhoneNumber, c.CreatedAt, c.UpdatedAt))
            .ToList();
    }
}
