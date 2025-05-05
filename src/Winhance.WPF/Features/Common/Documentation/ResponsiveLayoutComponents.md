# Responsive Layout Components

This document provides an overview of the responsive layout components and how to use them in your views.

## Overview

The responsive layout components provide a consistent way to handle:
1. Scrolling with mouse wheel anywhere in the content area
2. Adaptive column layouts that adjust based on window width
3. Consistent item sizing and spacing

## Components

### 1. ResponsiveScrollViewer

A custom ScrollViewer that handles mouse wheel events regardless of cursor position.

```xml
<controls:ResponsiveScrollViewer>
    <!-- Your content here -->
</controls:ResponsiveScrollViewer>
```

### 2. ResponsiveLayoutBehavior

An attached behavior that provides properties for responsive layouts:

- `ItemWidth`: Sets the width of items (default: 200)
- `MinItemWidth`: Sets the minimum width of items (default: 150)
- `UseWrapPanel`: Automatically configures an ItemsControl to use a WrapPanel

```xml
<FrameworkElement behaviors:ResponsiveLayoutBehavior.ItemWidth="200"
                 behaviors:ResponsiveLayoutBehavior.MinItemWidth="150" />

<ItemsControl behaviors:ResponsiveLayoutBehavior.UseWrapPanel="True" />
```

### 3. Styles

- `ResponsiveItemContainerStyle`: Style for item containers with responsive width
- `ResponsiveWrapPanelStyle`: Style for WrapPanels with horizontal orientation
- `ResponsiveItemsControlStyle`: Style for ItemsControls with responsive layout

```xml
<ItemsControl Style="{StaticResource ResponsiveItemsControlStyle}" />

<FrameworkElement Style="{StaticResource ResponsiveItemContainerStyle}" />

<WrapPanel Style="{StaticResource ResponsiveWrapPanelStyle}" />
```

## How to Use in Your Views

### Basic Usage

1. Add the necessary namespace declarations:

```xml
xmlns:controls="clr-namespace:Winhance.WPF.Features.Common.Controls"
xmlns:behaviors="clr-namespace:Winhance.WPF.Features.Common.Behaviors"
```

2. Replace standard ScrollViewer with ResponsiveScrollViewer:

```xml
<controls:ResponsiveScrollViewer>
    <!-- Your content here -->
</controls:ResponsiveScrollViewer>
```

3. Use the ResponsiveItemsControlStyle for ItemsControls:

```xml
<ItemsControl ItemsSource="{Binding YourItems}"
              Style="{StaticResource ResponsiveItemsControlStyle}">
    <!-- Your ItemTemplate here -->
</ItemsControl>
```

### Advanced Usage

For more control, you can use the individual components:

```xml
<ItemsControl ItemsSource="{Binding YourItems}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel Style="{StaticResource ResponsiveWrapPanelStyle}" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemContainerStyle>
        <Style BasedOn="{StaticResource ResponsiveItemContainerStyle}" 
               TargetType="FrameworkElement" />
    </ItemsControl.ItemContainerStyle>
    <!-- Your ItemTemplate here -->
</ItemsControl>
```

## Benefits

- **Single Responsibility**: Each component has a clear, focused purpose
- **DRY Principle**: No code duplication across views
- **Maintainability**: Changes to scrolling or layout behavior only need to be made in one place
- **Consistency**: All views will behave the same way
- **Extensibility**: Easy to add new features or modify existing behavior

## Implementation Details

- The ResponsiveScrollViewer handles mouse wheel events and scrolls the content appropriately
- The ResponsiveLayoutBehavior provides attached properties for responsive layouts
- The styles provide consistent appearance and behavior across views