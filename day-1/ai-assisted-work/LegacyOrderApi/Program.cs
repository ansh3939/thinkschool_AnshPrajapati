using LegacyOrderApi.Data;
using LegacyOrderApi.Models;
using LegacyOrderApi.Repositories;
using LegacyOrderApi.Services;
using LegacyOrderApi.Services.Discounts;
using LegacyOrderApi.Services.Rules;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseInMemoryDatabase("LegacyOrdersDb");
});

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddScoped<IOrderEligibilityRule, ActiveCustomerRule>();
builder.Services.AddScoped<IOrderEligibilityRule, RestrictedShippingCityRule>();

builder.Services.AddScoped<IDiscountStrategy, SaveTenDiscountStrategy>();
builder.Services.AddScoped<IDiscountStrategy, SaveTwentyDiscountStrategy>();
builder.Services.AddScoped<IDiscountStrategy, VipDiscountStrategy>();

var app = builder.Build();

app.MapControllers();

// Seed sample data.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Customers.Add(new Customer
    {
        Id = 1,
        Name = "Alice Johnson",
        Email = "alice@example.com",
        IsActive = true,
        Address = null
    });

    db.Customers.Add(new Customer
    {
        Id = 2,
        Name = "Bob Smith",
        Email = "bob@example.com",
        IsActive = false
    });

    db.Customers.Add(new Customer
    {
        Id = 3,
        Name = "Carol Diaz",
        Email = "carol@example.com",
        IsActive = true,
        Address = new Address
        {
            Street = "1 Main St",
            City = "Springfield",
            ZipCode = "12345"
        }
    });

    db.Products.Add(new Product
    {
        Id = 1,
        Name = "Widget",
        Price = 9.99m,
        StockQuantity = 50
    });

    db.Products.Add(new Product
    {
        Id = 2,
        Name = "Gadget",
        Price = 19.99m,
        StockQuantity = 5
    });

    db.Products.Add(new Product
    {
        Id = 3,
        Name = "Gizmo",
        Price = 4.50m,
        StockQuantity = 0
    });

    db.SaveChanges();
}

app.Run();

// Required by WebApplicationFactory integration tests.
public partial class Program
{
}