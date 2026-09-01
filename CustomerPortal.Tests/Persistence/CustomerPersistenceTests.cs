using CustomerPortal.Data;
using CustomerPortal.Models.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerPortal.Tests.Persistence;

public class CustomerPersistenceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        _dbContext = new AppDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SaveChanges_NewCustomer_SetsCreatedAtAndUpdatedAtInUtc()
    {
        var customer = new Customer { Email = "utc@example.com", PasswordHash = "hash" };
        _dbContext.Customers.Add(customer);

        await _dbContext.SaveChangesAsync();

        Assert.Equal(TimeSpan.Zero, customer.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, customer.UpdatedAt.Offset);
        Assert.Equal(customer.CreatedAt, customer.UpdatedAt);
    }

    [Fact]
    public async Task SaveChanges_ExactDuplicateEmail_ThrowsOnUniqueConstraint()
    {
        _dbContext.Customers.Add(new Customer { Email = "same@example.com", PasswordHash = "hash1" });
        await _dbContext.SaveChangesAsync();

        _dbContext.Customers.Add(new Customer { Email = "same@example.com", PasswordHash = "hash2" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _dbContext.SaveChangesAsync());
    }

    [Fact]
    public void CustomerConfiguration_EmailProperty_HasMaxLength254AndIsRequired()
    {
        var property = _dbContext.Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.Email))!;

        Assert.Equal(254, property.GetMaxLength());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void CustomerConfiguration_PasswordHashProperty_HasMaxLength60AndIsRequired()
    {
        var property = _dbContext.Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.PasswordHash))!;

        Assert.Equal(60, property.GetMaxLength());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void CustomerConfiguration_EmailIndex_IsUnique()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(Customer))!;
        var index = entityType.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Customer.Email));

        Assert.True(index.IsUnique);
    }
}
