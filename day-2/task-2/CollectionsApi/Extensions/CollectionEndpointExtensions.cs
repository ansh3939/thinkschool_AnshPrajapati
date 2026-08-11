using CollectionsApi.Models;
using CollectionsApi.Repositories;

namespace CollectionsApi.Extensions;

public static class CollectionEndpointExtensions
{
    public static void MapCollectionEndpoints(this WebApplication app)
    {
        app.MapPost("/collections", async (
            CreateCollectionRequest request,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = new Collection(
                request.OwnerId,
                request.Name);

            await repository.Add(collection, cancellationToken);

            return Results.Created(
                $"/collections/{collection.Id}",
                collection);
        });

        app.MapPost("/collections/{collectionId:int}/items/{quoteId:int}", async (
            int collectionId,
            int quoteId,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetById(collectionId, cancellationToken);

            if (collection is null)
                return Results.NotFound();

            collection.AddItem(quoteId);

            await repository.Update(collection, cancellationToken);

            return Results.Ok(collection);
        });

        app.MapDelete("/collections/{collectionId:int}/items/{quoteId:int}", async (
            int collectionId,
            int quoteId,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetById(collectionId, cancellationToken);

            if (collection is null)
                return Results.NotFound();

            collection.RemoveItem(quoteId);

            await repository.Update(collection, cancellationToken);

            return Results.NoContent();
        });
    }
}

public record CreateCollectionRequest(
    int OwnerId,
    string Name);
