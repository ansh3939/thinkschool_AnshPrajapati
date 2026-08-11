using DependencyInjectionApi.Clock;
using DependencyInjectionApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<TransientService>();
builder.Services.AddScoped<ScopedService>();
builder.Services.AddSingleton<SingletonService>();

builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();

app.MapGet("/", (IClock clock) =>
{
    return new
    {
        UtcNow = clock.UtcNow
    };
});

app.Run();