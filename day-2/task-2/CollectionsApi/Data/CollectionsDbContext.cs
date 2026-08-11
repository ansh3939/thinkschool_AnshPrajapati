using CollectionsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CollectionsApi.Data;

public class CollectionsDbContext : DbContext
{
    public CollectionsDbContext(DbContextOptions<CollectionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Collection> Collections => Set<Collection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(collection => collection.Id);

            entity.Property(collection => collection.Name)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(collection => collection.OwnerId)
                .IsRequired();

            entity.OwnsMany(
                collection => collection.Items,
                item =>
                {
                    item.HasKey(
                        "CollectionId",
                        nameof(CollectionItem.QuoteId));

                    item.Property(collectionItem => collectionItem.QuoteId)
                        .IsRequired()
                        .ValueGeneratedNever();

                    item.Property(collectionItem => collectionItem.AddedAt)
                        .IsRequired();
                });
        });
    }
}