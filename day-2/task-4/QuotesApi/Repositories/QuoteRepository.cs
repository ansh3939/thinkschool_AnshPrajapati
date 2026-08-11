using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Repositories;

public class QuoteRepository(QuotesDbContext db) : IQuoteRepository
{
    public async Task<(IReadOnlyList<Quote> Quotes, int Total)> GetPagedAsync(
        int page, int size, CancellationToken cancellationToken)
    {
        var query = db.Quotes.AsNoTracking().Where(quote => !quote.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var quotes = await query.OrderBy(quote => quote.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (quotes, total);
    }

    public Task<Quote?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Quotes.AsNoTracking()
            .FirstOrDefaultAsync(quote => quote.Id == id && !quote.IsDeleted, cancellationToken);

    public async Task<Quote> AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        db.Quotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);
        return quote;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var quote = await db.Quotes
            .FirstOrDefaultAsync(quote => quote.Id == id && !quote.IsDeleted, cancellationToken);

        if (quote is null)
            return false;

        quote.Delete();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
