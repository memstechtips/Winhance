using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace Winhance.WPF.Features.Common.Behaviors
{
    public static class GridViewColumnResizeBehavior
    {
        #region DependencyProperties

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(GridViewColumnResizeBehavior),
                new UIPropertyMetadata(false, OnEnabledChanged));

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.RegisterAttached(
                "Stretch",
                typeof(bool),
                typeof(GridViewColumnResizeBehavior),
                new UIPropertyMetadata(false));

        public static readonly DependencyProperty StretchColumnIndexProperty =
            DependencyProperty.RegisterAttached(
                "StretchColumnIndex",
                typeof(int),
                typeof(GridViewColumnResizeBehavior),
                new UIPropertyMetadata(-1));

        #endregion

        #region Attached Property Getters/Setters

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        public static bool GetStretch(DependencyObject obj)
        {
            return (bool)obj.GetValue(StretchProperty);
        }

        public static void SetStretch(DependencyObject obj, bool value)
        {
            obj.SetValue(StretchProperty, value);
        }

        public static int GetStretchColumnIndex(DependencyObject obj)
        {
            return (int)obj.GetValue(StretchColumnIndexProperty);
        }

        public static void SetStretchColumnIndex(DependencyObject obj, int value)
        {
            obj.SetValue(StretchColumnIndexProperty, value);
        }

        #endregion

        #region Event Handlers

        private static void OnEnabledChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (obj is ListView listView)
            {
                bool oldValue = (bool)e.OldValue;
                bool newValue = (bool)e.NewValue;

                if (oldValue && !newValue)
                {
                    listView.Loaded -= OnListViewLoaded;
                    listView.SizeChanged -= OnListViewSizeChanged;
                }
                else if (!oldValue && newValue)
                {
                    listView.Loaded += OnListViewLoaded;
                    listView.SizeChanged += OnListViewSizeChanged;
                }
            }
        }

        private static void OnListViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            if (listView?.View is GridView gridView)
            {
                ResizeColumns(listView, gridView);
            }
        }

        private static void OnListViewLoaded(object sender, RoutedEventArgs e)
        {
            ListView listView = sender as ListView;
            if (listView?.View is GridView gridView)
            {
                ResizeColumns(listView, gridView);
            }
        }

        #endregion

        #region Helper Methods

        private static void ResizeColumns(ListView listView, GridView gridView)
        {
            bool stretch = GetStretch(listView);
            int stretchColumnIndex = GetStretchColumnIndex(listView);

            if (gridView.Columns.Count == 0)
                return;

            // Get the width of the ListView's viewport
            double viewportWidth = GetViewportWidth(listView);
            
            // Account for vertical scrollbar if present
            double scrollBarWidth = SystemParameters.VerticalScrollBarWidth;
            ScrollViewer scrollViewer = GetScrollViewer(listView);
            bool hasVerticalScrollBar = scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible;
            // Add a small buffer to ensure the column fills the entire width without leaving a gap
            double availableWidth = viewportWidth - (hasVerticalScrollBar ? scrollBarWidth : 0) + 1; // add 1 to ensure no gap
            
            // First measure the total width of all columns except the stretch column
            double totalFixedColumnsWidth = 0;
            for (int i = 0; i < gridView.Columns.Count; i++)
            {
                if (stretch && i == stretchColumnIndex)
                    continue;
                    
                if (stretch && stretchColumnIndex < 0 && i == gridView.Columns.Count - 1)
                    continue;
                    
                totalFixedColumnsWidth += gridView.Columns[i].ActualWidth;
            }
            
            // If we have a specific stretch column
            if (stretch && stretchColumnIndex >= 0 && stretchColumnIndex < gridView.Columns.Count)
            {
                // Calculate the width for the stretch column
                double stretchColumnWidth = Math.Max(100, availableWidth - totalFixedColumnsWidth);
                gridView.Columns[stretchColumnIndex].Width = stretchColumnWidth;
            }
            // Otherwise, stretch the last column to fill remaining space
            else if (stretch)
            {
                // Make the last column fill any remaining space
                int lastColumnIndex = gridView.Columns.Count - 1;
                double lastColumnWidth = Math.Max(100, availableWidth - totalFixedColumnsWidth);
                gridView.Columns[lastColumnIndex].Width = lastColumnWidth;
            }
        }

        private static double GetViewportWidth(ListView listView)
        {
            ScrollViewer scrollViewer = GetScrollViewer(listView);
            if (scrollViewer != null)
            {
                return scrollViewer.ViewportWidth;
            }
            return listView.ActualWidth;
        }

        private static ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        #endregion
    }
}
