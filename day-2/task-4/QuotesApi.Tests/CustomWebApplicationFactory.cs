using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuotesApi.Data;

namespace QuotesApi.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<QuotesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<QuotesDbContext>>();
            services.AddDbContext<QuotesDbContext>(options =>
                options.UseInMemoryDatabase($"QuotesApiTests-{Guid.NewGuid()}"));
        });
    }
}
