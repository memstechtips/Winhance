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
    public void Observe_Source_RefreshesOnceSoTheControlDrawsItsNewSetting()
    {
        Watching().Observe(new Source());

        _refreshes.Should().Be(1);
    }

    [Fact]
    public void Observe_ChangeToWatchedProperty_Refreshes()
    {
        var source = new Source();
        Watching("Value").Observe(source);

        source.Raise("Value");

        _refreshes.Should().Be(2);
    }

    [Fact]
    public void Observe_ChangeToPropertyTheControlDoesNotDraw_IsIgnored()
    {
        var source = new Source();
        Watching("Value").Observe(source);

        source.Raise("Something else");

        _refreshes.Should().Be(1);
    }

    [Fact]
    public void Observe_NullPropertyName_RefreshesWhateverTheFilterSays()
    {
        var source = new Source();
        Watching("Value").Observe(source);

        source.Raise(null);

        _refreshes.Should().Be(2, "the view model raises it for everything on a language change");
    }

    [Fact]
    public void Observe_AnotherSource_StopsTheSourceLeftBehind()
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
    public void Detach_Always_StopsRefreshes()
    {
        var source = new Source();
        var observer = Watching("Value");
        observer.Observe(source);
        observer.Detach();

        source.Raise("Value");

        _refreshes.Should().Be(1);
    }

    [Fact]
    public void Resume_AfterDetach_WatchesAndRedrawsAgain()
    {
        var source = new Source();
        var observer = Watching("Value");
        observer.Observe(source);
        observer.Detach();

        observer.Resume(source);
        source.Raise("Value");

        _refreshes.Should().Be(3, "the resume redraws once and the change after it refreshes");
    }

    [Fact]
    public void Resume_OnTheSourceAlreadyObserved_DoesNothing()
    {
        var source = new Source();
        var observer = Watching("Value");
        observer.Observe(source);

        observer.Resume(source);
        source.Raise("Value");

        _refreshes.Should().Be(2, "the first load must not draw the card twice or subscribe it twice");
    }

    [Fact]
    public void Detach_CalledTwice_DoesNotThrow()
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
    public void Observe_Null_DropsThePreviousSourceAndStillRefreshes()
    {
        var source = new Source();
        var observer = Watching("Value");
        observer.Observe(source);

        observer.Observe(null);
        source.Raise("Value");

        _refreshes.Should().Be(2, "the clearing refresh counts, the detached source does not");
    }
}
