using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Repositories;

public class QuoteRepository(
    QuotesDbContext db,
    ILogger<QuoteRepository> logger) : IQuoteRepository
{
    public async Task<(IReadOnlyList<Quote> Quotes, int Total)> GetPagedAsync(
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        var query = db.Quotes.AsNoTracking();

        var total = await query.CountAsync(cancellationToken);

        var quotes = await query
            .OrderBy(q => q.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Retrieved {Count} quotes from page {Page} with size {Size}",
            quotes.Count,
            page,
            size);

        return (quotes, total);
    }

    public async Task<Quote?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await db.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<Quote> AddAsync(
        Quote quote,
        CancellationToken cancellationToken)
    {
        db.Quotes.Add(quote);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created quote {QuoteId} by {Author}",
            quote.Id,
            quote.Author);

        return quote;
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var quote = await db.Quotes
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quote is null)
            return false;

        db.Quotes.Remove(quote);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Deleted quote {QuoteId}",
            id);

        return true;
    }
}