using System.ComponentModel;
using FluentAssertions;
using Winhance.UI.Features.Common.Controls;
using Xunit;

namespace Winhance.UI.Tests.Controls;

// The observation half of every Setting* card control. It cannot be reached through a control instance: the
// headless test host throws COMException from UserControl's constructor, so this is the only gate on it.
public class SettingObserverTests
{
    private sealed class Source : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string? propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private int _refreshes;

    private SettingObserver Watching(params string[] watched) =>
        new(() => _refreshes++, name => watched.Contains(name));

    [Fact]
    public void Observing_a_source_refreshes_once_so_the_control_draws_its_new_setting()
    {
        Watching().Observe(new Source());

        _refreshes.Should().Be(1);
    }

    [Fact]
    public void A_change_to_a_watched_property_refreshes()
    {
        var source = new Source();
        Watching("Value").Observe(source);

        source.Raise("Value");

        _refreshes.Should().Be(2);
    }

    [Fact]
    public void A_change_to_a_property_the_control_does_not_draw_is_ignored()
    {
        var source = new Source();
        Watching("Value").Observe(source);

        source.Raise("Something else");

        _refreshes.Should().Be(1);
    }

    [Fact]
    public void A_null_property_name_refreshes_whatever_the_filter_says()
    {
        var source = new Source();
        Watching("Value").Observe(source);

        source.Raise(null);

        _refreshes.Should().Be(2, "the view model raises it for everything on a language change");
    }

    [Fact]
    public void Re_observing_stops_the_source_that_was_left_behind()
    {
        var first = new Source();
        var observer = Watching("Value");
        observer.Observe(first);
        observer.Observe(new Source());
        _refreshes = 0;

        first.Raise("Value");

        _refreshes.Should().Be(0, "a rebound control must not keep drawing the card it no longer shows");
    }

    [Fact]
    public void Detaching_stops_refreshes()
    {
        var source = new Source();
        var observer = Watching("Value");
        observer.Observe(source);
        observer.Detach();

        source.Raise("Value");

        _refreshes.Should().Be(1);
    }

    [Fact]
    public void Detaching_twice_does_not_throw()
    {
        var observer = Watching();
        observer.Observe(new Source());

        var act = () =>
        {
            observer.Detach();
            observer.Detach();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Observing_nothing_drops_the_previous_source_and_still_refreshes()
    {
        var source = new Source();
        var observer = Watching("Value");
        observer.Observe(source);

        observer.Observe(null);
        source.Raise("Value");

        _refreshes.Should().Be(2, "the clearing refresh counts, the detached source does not");
    }
}
