using System.Collections.Specialized;
using System.ComponentModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingsGroupTests : IDisposable
{
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();

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
    }

    public void Dispose()
    {
        // Intentionally empty.
    }

    private SettingItemViewModel CreateSettingItem(
        string settingId = "test-setting",
        string name = "Test Setting",
        string description = "Description",
        string groupName = "Group",
        bool isVisible = true)
    {
        var settingDef = new SettingDefinition
        {
            Id = settingId,
            Name = name,
            Description = description,
            InputType = InputType.Toggle,
        };

        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = settingDef,
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
            _mockSettingApplicationService.Object,
            _mockLogService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockEventBus.Object);

        item.IsVisible = isVisible;
        return item;
    }

    // ── Constructor Tests ──

    [Fact]
    public void Constructor_WithKeyAndItems_SetsKey()
    {
        // Arrange
        var items = new[] { CreateSettingItem("s1", "Item 1") };

        // Act
        var group = new SettingsGroup("TestGroup", items);

        // Assert
        group.Key.Should().Be("TestGroup");
    }

    [Fact]
    public void Constructor_WithNullKey_SetsEmptyKey()
    {
        // Arrange
        var items = new[] { CreateSettingItem("s1", "Item 1") };

        // Act
        var group = new SettingsGroup(null!, items);

        // Assert
        group.Key.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithItems_PopulatesCollection()
    {
        // Arrange
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1"),
            CreateSettingItem("s2", "Item 2"),
            CreateSettingItem("s3", "Item 3"),
        };

        // Act
        var group = new SettingsGroup("Group", items);

        // Assert
        group.Should().HaveCount(3);
        group[0].SettingId.Should().Be("s1");
        group[1].SettingId.Should().Be("s2");
        group[2].SettingId.Should().Be("s3");
    }

    [Fact]
    public void Constructor_WithEmptyItems_CreatesEmptyGroup()
    {
        // Act
        var group = new SettingsGroup("EmptyGroup", Enumerable.Empty<SettingItemViewModel>());

        // Assert
        group.Should().BeEmpty();
        group.Key.Should().Be("EmptyGroup");
    }

    // ── HasVisibleItems ──

    [Fact]
    public void HasVisibleItems_WhenAllItemsVisible_ReturnsTrue()
    {
        // Arrange
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: true),
            CreateSettingItem("s2", "Item 2", isVisible: true),
        };

        // Act
        var group = new SettingsGroup("Group", items);

        // Assert
        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenSomeItemsVisible_ReturnsTrue()
    {
        // Arrange
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: true),
            CreateSettingItem("s2", "Item 2", isVisible: false),
        };

        // Act
        var group = new SettingsGroup("Group", items);

        // Assert
        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenNoItemsVisible_ReturnsFalse()
    {
        // Arrange
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: false),
            CreateSettingItem("s2", "Item 2", isVisible: false),
        };

        // Act
        var group = new SettingsGroup("Group", items);

        // Assert
        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void HasVisibleItems_WhenEmpty_ReturnsFalse()
    {
        // Act
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());

        // Assert
        group.HasVisibleItems.Should().BeFalse();
    }

    // ── HasVisibleItems Updates on Item Visibility Change ──

    [Fact]
    public void HasVisibleItems_WhenItemBecomesInvisible_UpdatesToFalse()
    {
        // Arrange
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        group.HasVisibleItems.Should().BeTrue();

        // Act
        item.IsVisible = false;

        // Assert
        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void HasVisibleItems_WhenItemBecomesVisible_UpdatesToTrue()
    {
        // Arrange
        var item = CreateSettingItem("s1", "Item 1", isVisible: false);
        var group = new SettingsGroup("Group", new[] { item });
        group.HasVisibleItems.Should().BeFalse();

        // Act
        item.IsVisible = true;

        // Assert
        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenOneOfManyBecomesInvisible_StaysTrueIfOthersVisible()
    {
        // Arrange
        var item1 = CreateSettingItem("s1", "Item 1", isVisible: true);
        var item2 = CreateSettingItem("s2", "Item 2", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item1, item2 });

        // Act
        item1.IsVisible = false;

        // Assert - item2 is still visible
        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void HasVisibleItems_WhenAllBecomeInvisible_ReturnsFalse()
    {
        // Arrange
        var item1 = CreateSettingItem("s1", "Item 1", isVisible: true);
        var item2 = CreateSettingItem("s2", "Item 2", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item1, item2 });

        // Act
        item1.IsVisible = false;
        item2.IsVisible = false;

        // Assert
        group.HasVisibleItems.Should().BeFalse();
    }

    // ── PropertyChanged for HasVisibleItems ──

    [Fact]
    public void HasVisibleItems_WhenChanges_RaisesPropertyChanged()
    {
        // Arrange
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        var raisedProperties = new List<string>();
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
            raisedProperties.Add(e.PropertyName!);

        // Act
        item.IsVisible = false;

        // Assert
        raisedProperties.Should().Contain(nameof(SettingsGroup.HasVisibleItems));
    }

    [Fact]
    public void HasVisibleItems_WhenValueDoesNotChange_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var item1 = CreateSettingItem("s1", "Item 1", isVisible: true);
        var item2 = CreateSettingItem("s2", "Item 2", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item1, item2 });
        var raisedCount = 0;
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsGroup.HasVisibleItems))
                raisedCount++;
        };

        // Act - making one invisible still keeps HasVisibleItems true
        item1.IsVisible = false;

        // Assert - HasVisibleItems is still true, so it should not have raised
        group.HasVisibleItems.Should().BeTrue();
        raisedCount.Should().Be(0);
    }

    // ── Collection Modification Behavior ──

    [Fact]
    public void Add_NewVisibleItem_UpdatesHasVisibleItems()
    {
        // Arrange
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());
        group.HasVisibleItems.Should().BeFalse();

        // Act
        var newItem = CreateSettingItem("s1", "New Item", isVisible: true);
        group.Add(newItem);

        // Assert
        group.HasVisibleItems.Should().BeTrue();
        group.Should().HaveCount(1);
    }

    [Fact]
    public void Add_NewInvisibleItem_HasVisibleItemsStaysFalse()
    {
        // Arrange
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());

        // Act
        var newItem = CreateSettingItem("s1", "New Item", isVisible: false);
        group.Add(newItem);

        // Assert
        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void Add_NewItem_SubscribesToPropertyChanged()
    {
        // Arrange
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());
        var newItem = CreateSettingItem("s1", "New Item", isVisible: false);
        group.Add(newItem);
        group.HasVisibleItems.Should().BeFalse();

        // Act - changing visibility should be tracked
        newItem.IsVisible = true;

        // Assert
        group.HasVisibleItems.Should().BeTrue();
    }

    [Fact]
    public void Remove_Item_UpdatesHasVisibleItems()
    {
        // Arrange
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        group.HasVisibleItems.Should().BeTrue();

        // Act
        group.Remove(item);

        // Assert
        group.HasVisibleItems.Should().BeFalse();
    }

    [Fact]
    public void Remove_Item_UnsubscribesFromPropertyChanged()
    {
        // Arrange
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });

        // Remove the item
        group.Remove(item);

        // Act - changing visibility on removed item
        var raisedProperties = new List<string>();
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
            raisedProperties.Add(e.PropertyName!);
        item.IsVisible = false;

        // Assert - group should not react to changes on removed items
        // HasVisibleItems is already false from removal, no change expected
        raisedProperties.Should().NotContain(nameof(SettingsGroup.HasVisibleItems));
    }

    [Fact]
    public void Clear_RemovesAllItemsAndUpdatesVisibility()
    {
        // Arrange
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1", isVisible: true),
            CreateSettingItem("s2", "Item 2", isVisible: true),
        };
        var group = new SettingsGroup("Group", items);
        group.HasVisibleItems.Should().BeTrue();

        // Act
        group.Clear();

        // Assert
        group.Should().BeEmpty();
        group.HasVisibleItems.Should().BeFalse();
    }

    // ── ObservableCollection Behavior ──

    [Fact]
    public void CollectionChanged_RaisedOnAdd()
    {
        // Arrange
        var group = new SettingsGroup("Group", Enumerable.Empty<SettingItemViewModel>());
        var changedActions = new List<NotifyCollectionChangedAction>();
        group.CollectionChanged += (_, e) => changedActions.Add(e.Action);

        // Act
        group.Add(CreateSettingItem("s1", "Item"));

        // Assert
        changedActions.Should().Contain(NotifyCollectionChangedAction.Add);
    }

    [Fact]
    public void CollectionChanged_RaisedOnRemove()
    {
        // Arrange
        var item = CreateSettingItem("s1", "Item");
        var group = new SettingsGroup("Group", new[] { item });
        var changedActions = new List<NotifyCollectionChangedAction>();
        group.CollectionChanged += (_, e) => changedActions.Add(e.Action);

        // Act
        group.Remove(item);

        // Assert
        changedActions.Should().Contain(NotifyCollectionChangedAction.Remove);
    }

    [Fact]
    public void Count_ReturnsCorrectNumberOfItems()
    {
        // Arrange
        var items = new[]
        {
            CreateSettingItem("s1", "Item 1"),
            CreateSettingItem("s2", "Item 2"),
        };

        // Act
        var group = new SettingsGroup("Group", items);

        // Assert
        group.Count.Should().Be(2);
    }

    [Fact]
    public void Indexer_ReturnsCorrectItem()
    {
        // Arrange
        var item1 = CreateSettingItem("s1", "Item 1");
        var item2 = CreateSettingItem("s2", "Item 2");
        var group = new SettingsGroup("Group", new[] { item1, item2 });

        // Act & Assert
        group[0].SettingId.Should().Be("s1");
        group[1].SettingId.Should().Be("s2");
    }

    // ── Multiple Visibility Transitions ──

    [Fact]
    public void HasVisibleItems_MultipleVisibilityToggles_TracksCorrectly()
    {
        // Arrange
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });

        // Act & Assert - toggle multiple times
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
        // Arrange
        var item = CreateSettingItem("s1", "Item 1", isVisible: true);
        var group = new SettingsGroup("Group", new[] { item });
        var raisedCount = 0;
        ((INotifyPropertyChanged)group).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsGroup.HasVisibleItems))
                raisedCount++;
        };

        // Act - change a non-visibility property
        item.Name = "Updated Name";

        // Assert - HasVisibleItems should not fire
        raisedCount.Should().Be(0);
    }
}
