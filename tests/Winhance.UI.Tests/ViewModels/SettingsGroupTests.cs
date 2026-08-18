using System.Collections.Specialized;
using System.ComponentModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class SettingsGroupTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();

    public SettingsGroupTests()
    {
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalizationService.MirrorTryGetString();
    }

    private SettingItemViewModel CreateSettingItem(
        string settingId = "test-setting",
        string name = "Test Setting",
        string description = "Description",
        string groupName = "Group",
        bool isVisible = true)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting { Id = settingId, Display = new() { Name = name, Description = description } },
            SettingId = settingId,
            Name = name,
            Description = description,
            GroupName = groupName,
            InputType = InputType.Toggle,
            IsSelected = false,
            Icon = "Icon",
            IconPack = "Material",
        };

        var item = new SettingItemViewModel(
            config,
            SettingWriteStrategies.Selector(
                _mockSettingApplicationService.Object, _mockDialogService.Object, _mockLocalizationService.Object, _mockLogService.Object),
            _mockLogService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);

        item.IsVisible = isVisible;
        return item;
    }

    [Fact]
    public void Constructor_WithKeyAndItems_SetsKey()
    {
        var items = new[] { CreateSettingItem("s1", "Item 1") };

        var group = new SettingsGroup("TestGroup", items);

        group.Key.Should().Be("TestGroup");
    }

    [Fact]
    public void Constructor_WithNullKey_SetsEmptyKey()
    {
        var items = new[] { CreateSettingItem("s1", "Item 1") };

        var group = new SettingsGroup(null!, items);

        group.Key.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithItems_PopulatesCollection()
    {
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1"),
            CreateSettingItem("s2", "Item 2"),
            CreateSettingItem("s3", "Item 3"),
        };

        var group = new SettingsGroup("Group", items);

        group.Should().HaveCount(3);
        group[0].SettingId.Should().Be("s1");
        group[1].SettingId.Should().Be("s2");
        group[2].SettingId.Should().Be("s3");
    }

    [Fact]
    public void Constructor_WithEmptyItems_CreatesEmptyGroup()
    {
        var group = new SettingsGroup("EmptyGroup", Enumerable.Empty<SettingItemViewModel>());

        group.Should().BeEmpty();
        group.Key.Should().Be("EmptyGroup");
    }

    [Fact]
    public void HasVisibleItems_WhenAllItemsVisible_ReturnsTrue()
    {
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: true),
            CreateSettingItem("s2", "Item 2", isVisible: true),
        };

        var group = new SettingsGroup("Group", items);

        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenSomeItemsVisible_ReturnsTrue()
    {
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: true),
            CreateSettingItem("s2", "Item 2", isVisible: false),
        };

        var group = new SettingsGroup("Group", items);

        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenNoItemsVisible_ReturnsFalse()
    {
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: false),
            CreateSettingItem("s2", "Item 2", isVisible: false),
        };

        var group = new SettingsGroup("Group", items);

        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void HasVisibleItems_WhenEmpty_ReturnsFalse()
    {
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());

        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void HasVisibleItems_WhenItemBecomesInvisible_UpdatesToFalse()
    {
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        group.HasVisibleItems.Should().BeTrue();

        item.IsVisible = false;

        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void HasVisibleItems_WhenItemBecomesVisible_UpdatesToTrue()
    {
        var item = CreateSettingItem("s1", "Item 1", isVisible: false);
        var group = new SettingsGroup("Group", new[] { item });
        group.HasVisibleItems.Should().BeFalse();

        item.IsVisible = true;

        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenOneOfManyBecomesInvisible_StaysTrueIfOthersVisible()
    {
        var item1 = CreateSettingItem("s1", "Item 1", isVisible: true);
        var item2 = CreateSettingItem("s2", "Item 2", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item1, item2 });

        item1.IsVisible = false;

        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenAllBecomeInvisible_ReturnsFalse()
    {
        var item1 = CreateSettingItem("s1", "Item 1", isVisible: true);
        var item2 = CreateSettingItem("s2", "Item 2", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item1, item2 });

        item1.IsVisible = false;
        item2.IsVisible = false;

        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void HasVisibleItems_WhenChanges_RaisesPropertyChanged()
    {
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        var raisedProperties = new List<string>();
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
            raisedProperties.Add(e.PropertyName!);

        item.IsVisible = false;

        raisedProperties.Should().Contain(nameof(SettingsGroup.HasVisibleItems));
    }

    [Fact]
    public void HasVisibleItems_WhenValueDoesNotChange_DoesNotRaisePropertyChanged()
    {
        var item1 = CreateSettingItem("s1", "Item 1", isVisible: true);
        var item2 = CreateSettingItem("s2", "Item 2", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item1, item2 });
        var raisedCount = 0;
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsGroup.HasVisibleItems))
                raisedCount++;
        };

        item1.IsVisible = false;

        group.HasVisibleItems.Should().BeTrue();
        raisedCount.Should().Be(0);
    }

    [Fact]
    public void Add_NewVisibleItem_UpdatesHasVisibleItems()
    {
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());
        group.HasVisibleItems.Should().BeFalse();

        var newItem = CreateSettingItem("s1", "New Item", isVisible: true);
        group.Add(newItem);

        group.HasVisibleItems.Should().BeTrue();
        group.Should().HaveCount(1);
    }

    [Fact]
    public void Add_NewInvisibleItem_HasVisibleItemsStaysFalse()
    {
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());

        var newItem = CreateSettingItem("s1", "New Item", isVisible: false);
        group.Add(newItem);

        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void Add_NewItem_SubscribesToPropertyChanged()
    {
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());
        var newItem = CreateSettingItem("s1", "New Item", isVisible: false);
        group.Add(newItem);
        group.HasVisibleItems.Should().BeFalse();

        newItem.IsVisible = true;

        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void Remove_Item_UpdatesHasVisibleItems()
    {
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        group.HasVisibleItems.Should().BeTrue();

        group.Remove(item);

        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void Remove_Item_UnsubscribesFromPropertyChanged()
    {
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });

        group.Remove(item);

        var raisedProperties = new List<string>();
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
            raisedProperties.Add(e.PropertyName!);
        item.IsVisible = false;

        raisedProperties.Should().NotContain(nameof(SettingsGroup.HasVisibleItems));
    }

    [Fact]
    public void Clear_RemovesAllItemsAndUpdatesVisibility()
    {
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: true),
            CreateSettingItem("s2", "Item 2", isVisible: true),
        };
        var group = new SettingsGroup("Group", items);
        group.HasVisibleItems.Should().BeTrue();

        group.Clear();

        group.Should().BeEmpty();
        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void CollectionChanged_RaisedOnAdd()
    {
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());
        var changedActions = new List<NotifyCollectionChangedAction>();
        group.CollectionChanged += (_, e) => changedActions.Add(e.Action);

        group.Add(CreateSettingItem("s1", "Item"));

        changedActions.Should().Contain(NotifyCollectionChangedAction.Add);
    }

    [Fact]
    public void CollectionChanged_RaisedOnRemove()
    {
        var item = CreateSettingItem("s1", "Item");
        var group = new SettingsGroup("Group", new[] { item });
        var changedActions = new List<NotifyCollectionChangedAction>();
        group.CollectionChanged += (_, e) => changedActions.Add(e.Action);

        group.Remove(item);

        changedActions.Should().Contain(NotifyCollectionChangedAction.Remove);
    }

    [Fact]
    public void Count_ReturnsCorrectNumberOfItems()
    {
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1"),
            CreateSettingItem("s2", "Item 2"),
        };

        var group = new SettingsGroup("Group", items);

        group.Count.Should().Be(2);
    }

    [Fact]
    public void Indexer_ReturnsCorrectItem()
    {
        var item1 = CreateSettingItem("s1", "Item 1");
        var item2 = CreateSettingItem("s2", "Item 2");
        var group = new SettingsGroup("Group", new[] { item1, item2 });

        group[0].SettingId.Should().Be("s1");
        group[1].SettingId.Should().Be("s2");
    }

    [Fact]
    public void HasVisibleItems_MultipleVisibilityToggles_TracksCorrectly()
    {
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });

        item.IsVisible = false;
        group.HasVisibleItems.Should().BeFalse();

        item.IsVisible = true;
        group.HasVisibleItems.Should().BeTrue();

        item.IsVisible = false;
        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void HasVisibleItems_NonVisibilityPropertyChange_DoesNotUpdate()
    {
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        var raisedCount = 0;
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsGroup.HasVisibleItems))
                raisedCount++;
        };

        item.Name = "Updated Name";

        raisedCount.Should().Be(0);
    }
}
