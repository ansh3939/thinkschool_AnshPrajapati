namespace CollectionsApi.Models;

public class Collection
{
    private readonly List<CollectionItem> _items = new();

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int OwnerId { get; private set; }

    public IReadOnlyCollection<CollectionItem> Items => _items.AsReadOnly();

    private Collection()
    {
    }

    public Collection(int ownerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Name is required.",
                nameof(name));

        if (name.Length < 3 || name.Length > 80)
            throw new ArgumentException(
                "Name must be between 3 and 80 characters.",
                nameof(name));

        OwnerId = ownerId;
        Name = name;
    }

    public void AddItem(int quoteId)
    {
        if (_items.Count >= 50)
            throw new InvalidOperationException(
                "A collection cannot contain more than 50 items.");

        if (_items.Any(item => item.QuoteId == quoteId))
            throw new InvalidOperationException(
                "The quote is already in this collection.");

        _items.Add(new CollectionItem(
            quoteId,
            DateTime.UtcNow));
    }

    public void RemoveItem(int quoteId)
    {
        var item = _items.FirstOrDefault(
            item => item.QuoteId == quoteId);

        if (item is null)
            throw new InvalidOperationException(
                "The quote is not in this collection.");

        _items.Remove(item);
    }
}