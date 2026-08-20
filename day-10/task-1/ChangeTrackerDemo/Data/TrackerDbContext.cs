using ChangeTrackerDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace ChangeTrackerDemo.Data;

public class TrackerDbContext(DbContextOptions<TrackerDbContext> options) : DbContext(options)
{
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Author> Authors => Set<Author>();
}
