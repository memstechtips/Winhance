## 2025-05-14 - DataGrid Virtualization Bottleneck
**Learning:** In the Software & Apps section, `DataGrid` virtualization was disabled because it was triple-nested inside `ResponsiveScrollViewer` and `StackPanel` controls. In WPF, a virtualizing control must have a constrained height to function. If a `DataGrid` is in an unconstrained container, it renders all items at once, causing significant memory pressure and UI lag.
**Action:** Avoid nesting `DataGrid` or other virtualizing controls inside `StackPanel` or `ScrollViewer` at any level that would provide infinite vertical space. Use `Grid` or direct parent-child relationships instead.

## 2025-05-14 - Optimized Search Allocations
**Learning:** The `SearchHelper.cs` utility used `ToLowerInvariant()` on both the search terms and the fields for every comparison. In a real-time search interface with many fields and items, this creates high GC pressure.
**Action:** Use `.Contains(term, StringComparison.OrdinalIgnoreCase)` available in .NET 9 to perform case-insensitive comparisons without intermediate string allocations.
