using Microsoft.EntityFrameworkCore;
using VulnerableAPI.Models;

namespace VulnerableAPI.Data;

/// <summary>
/// Database context with intentional security flaws for educational purposes
/// </summary>
public class VulnerableDbContext : DbContext
{
    public VulnerableDbContext(DbContextOptions<VulnerableDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }

    public DbSet<Document> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed initial data with weak passwords
        modelBuilder.Entity<User>().HasData(
  new User
            {
          Id = 1,
    Username = "admin",
          Email = "admin@vulnerable.api",
        // VULNERABILITY: Plain text password storage (API2:2023)
           Password = "admin123",
                IsAdmin = true,
   FullName = "System Administrator",
       PhoneNumber = "555-0001",
        Address = "123 Admin Street",
          CreditCardNumber = "4532-1234-5678-9010",
         CreatedAt = DateTime.UtcNow
  },
        new User
      {
     Id = 2,
            Username = "john",
    Email = "john@vulnerable.api",
        // VULNERABILITY: Weak password (API2:2023)
                Password = "12345",
      IsAdmin = false,
  FullName = "John Doe",
     PhoneNumber = "555-0002",
             Address = "456 User Lane",
     CreditCardNumber = "4532-9876-5432-1098",
       CreatedAt = DateTime.UtcNow
},
    new User
            {
       Id = 3,
        Username = "jane",
        Email = "jane@vulnerable.api",
             Password = "password",
   IsAdmin = false,
    FullName = "Jane Smith",
     PhoneNumber = "555-0003",
   Address = "789 Customer Blvd",
          CreditCardNumber = "4532-1111-2222-3333",
        CreatedAt = DateTime.UtcNow
}
        );

        modelBuilder.Entity<Product>().HasData(
    new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, Stock = 50, Category = "Electronics" },
      new Product { Id = 2, Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, Stock = 200, Category = "Electronics" },
            new Product { Id = 3, Name = "Keyboard", Description = "Mechanical keyboard", Price = 89.99m, Stock = 150, Category = "Electronics" },
  new Product { Id = 4, Name = "Monitor", Description = "4K Monitor 27 inch", Price = 399.99m, Stock = 75, Category = "Electronics" },
   new Product { Id = 5, Name = "USB Cable", Description = "USB-C cable 2m", Price = 12.99m, Stock = 500, Category = "Accessories" }
        );
    }
}
