using CustomerPortal.Models.Entities;

namespace CustomerPortal.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> FindByEmailAsync(string email);
    Task<bool> ExistsByEmailAsync(string email);
    Task AddAsync(Customer customer);
}
