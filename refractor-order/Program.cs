using LegacyOrderApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// TODO: move this to appsettings.json someday (never happened)
var connString = "Server=localhost;Database=LegacyOrdersDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // swapped to InMemory so this project actually runs without a real SQL Server
    options.UseInMemoryDatabase("LegacyOrdersDb");
});

var app = builder.Build();

app.MapControllers();

// seed some junk data so the endpoint has something to chew on
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Customers.Add(new LegacyOrderApi.Models.Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com", IsActive = true });
    db.Customers.Add(new LegacyOrderApi.Models.Customer { Id = 2, Name = "Bob Smith", Email = "bob@example.com", IsActive = false });

    db.Products.Add(new LegacyOrderApi.Models.Product { Id = 1, Name = "Widget", Price = 9.99m, StockQuantity = 50 });
    db.Products.Add(new LegacyOrderApi.Models.Product { Id = 2, Name = "Gadget", Price = 19.99m, StockQuantity = 5 });
    db.Products.Add(new LegacyOrderApi.Models.Product { Id = 3, Name = "Gizmo", Price = 4.50m, StockQuantity = 0 });

    db.SaveChanges();
}

app.Run();
