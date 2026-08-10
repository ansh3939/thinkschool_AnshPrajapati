using CollectionsApi.Data;
using CollectionsApi.Extensions;
using CollectionsApi.Middleware;
using CollectionsApi.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CollectionsDbContext>(options =>
    options.UseSqlite("Data Source=collections.db"));

builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();

app.MapCollectionEndpoints();

app.Run();