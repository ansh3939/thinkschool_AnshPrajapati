using QueryTranslationDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace QueryTranslationDemo.Data;

public class QuoteDbContext(DbContextOptions<QuoteDbContext> options) : DbContext(options)
{
    public DbSet<Quote> Quotes => Set<Quote>();
}
