using CollectionsApi.Data;
using CollectionsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CollectionsApi.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly CollectionsDbContext _db;

    public CollectionRepository(CollectionsDbContext db)
    {
        _db = db;
    }

    public async Task<Collection?> GetById(int id)
    {
        return await _db.Collections
            .Include(collection => collection.Items)
            .FirstOrDefaultAsync(collection => collection.Id == id);
    }

    public async Task Add(Collection collection)
    {
        await _db.Collections.AddAsync(collection);
        await _db.SaveChangesAsync();
    }

    public async Task Update(Collection collection)
    {
        _db.Collections.Update(collection);
        await _db.SaveChangesAsync();
    }

    public async Task Delete(Collection collection)
    {
        _db.Collections.Remove(collection);
        await _db.SaveChangesAsync();
    }
}