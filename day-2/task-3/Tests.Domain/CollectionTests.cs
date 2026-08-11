using Collections.Domain;
using FluentAssertions;

namespace Tests.Domain;

public class CollectionTests
{
    [Fact]
    public void Empty_name_throws()
    {
        Action act = () => new Collection(1, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_over_80_characters_throws()
    {
        var name = new string('A', 81);

        Action act = () => new Collection(1, name);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Adding_51st_item_throws()
    {
        var collection = new Collection(1, "My Collection");

        for (var i = 1; i <= 50; i++)
            collection.AddItem(i);

        Action act = () => collection.AddItem(51);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Duplicate_quote_id_throws()
    {
        var collection = new Collection(1, "My Collection");

        collection.AddItem(1);

        Action act = () => collection.AddItem(1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Removing_nonexistent_item_throws()
    {
        var collection = new Collection(1, "My Collection");

        Action act = () => collection.RemoveItem(1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Adding_then_removing_leaves_zero_items()
    {
        var collection = new Collection(1, "My Collection");

        collection.AddItem(1);
        collection.RemoveItem(1);

        collection.Items.Should().BeEmpty();
    }
}