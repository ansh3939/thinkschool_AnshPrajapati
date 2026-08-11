using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    else
        await db.Database.EnsureCreatedAsync(app.Lifetime.ApplicationStopping);
}

app.MapQuoteEndpoints();

app.Run();

public partial class Program;
