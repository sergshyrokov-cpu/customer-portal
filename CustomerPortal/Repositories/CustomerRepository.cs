using CustomerPortal.Data;
using CustomerPortal.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Repositories;

/// <summary>
/// Owns the single SaveChangesAsync call for this Story's one-write flow
/// (architecture.md AD-3; see implementation_plan v2 Architectural Changes).
/// </summary>
public class CustomerRepository(AppDbContext dbContext) : ICustomerRepository
{
    public Task<Customer?> FindByEmailAsync(string email) =>
        dbContext.Customers.SingleOrDefaultAsync(c => c.Email == email);

    public Task<bool> ExistsByEmailAsync(string email) =>
        dbContext.Customers.AnyAsync(c => c.Email == email);

    public async Task AddAsync(Customer customer)
    {
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
    }
}
