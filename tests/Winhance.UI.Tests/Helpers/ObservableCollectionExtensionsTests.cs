using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FluentAssertions;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

public class ObservableCollectionExtensionsTests
{
    private sealed class Item(string name)
    {
        public string Name { get; } = name;

        public override string ToString() => Name;
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "a,b,c")]
    [InlineData("a,b,c", "")]
    [InlineData("a,b,c", "x,y")]
    [InlineData("a,b", "x,y,z")]
    [InlineData("a,b,c", "a,b,c")]
    [InlineData("a,b,c", "c,b,a")]
    [InlineData("a,b,c,d", "b,x,d")]
    [InlineData("a,b,c", "x,a,y,c,z")]
    [InlineData("a,a,b", "b,a,a,a")]
    [InlineData("a,b,c,d,e", "e,c,a")]
    public void EndsWithExactlyTheNewContents(string before, string after)
    {
        var collection = new ObservableCollection<string>(Split(before));

        collection.ReplaceInPlace(Split(after));

        collection.Should().Equal(Split(after));
    }

    [Fact]
    public void AcceptsAQueryOverTheSameCollection()
    {
        var collection = new ObservableCollection<string>(Split("a,b,c,d"));

        collection.ReplaceInPlace(collection.Where(s => s != "b").Reverse());

        collection.Should().Equal("d", "c", "a");
    }

    [Fact]
    public void KeepsSurvivingInstancesAndNeverResets()
    {
        var a = new Item("a");
        var b = new Item("b");
        var c = new Item("c");
        var collection = new ObservableCollection<Item> { a, b, c };
        var actions = Record(collection);

        var removed = collection.ReplaceInPlace([c, new Item("x"), a]);

        collection[0].Should().BeSameAs(c);
        collection[2].Should().BeSameAs(a);
        removed.Should().ContainSingle().Which.Should().BeSameAs(b);
        actions.Should().NotContain(NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void RaisesNothingWhenTheContentsAreUnchanged()
    {
        var collection = new ObservableCollection<string>(Split("a,b,c"));
        var actions = Record(collection);

        var removed = collection.ReplaceInPlace(["a", "b", "c"]);

        actions.Should().BeEmpty();
        removed.Should().BeEmpty();
    }

    [Fact]
    public void ReplacesSlotsWhenNothingIsShared()
    {
        var collection = new ObservableCollection<string>(Split("a,b"));
        var actions = Record(collection);

        var removed = collection.ReplaceInPlace(["x", "y", "z"]);

        actions.Should().Equal(
            NotifyCollectionChangedAction.Replace,
            NotifyCollectionChangedAction.Replace,
            NotifyCollectionChangedAction.Add);
        removed.Should().Equal("a", "b");
    }

    [Fact]
    public void EmptiesWithRemovesRatherThanAReset()
    {
        var collection = new ObservableCollection<string>(Split("a,b"));
        var actions = Record(collection);

        var removed = collection.ReplaceInPlace([]);

        collection.Should().BeEmpty();
        actions.Should().OnlyContain(action => action == NotifyCollectionChangedAction.Remove);
        removed.Should().Equal("b", "a");
    }

    private static string[] Split(string list) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries);

    private static List<NotifyCollectionChangedAction> Record<T>(ObservableCollection<T> collection)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);
        return actions;
    }
}
