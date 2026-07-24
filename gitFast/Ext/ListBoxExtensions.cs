using System;
using Avalonia;
using Avalonia.Controls;

namespace gitFast.Ext;

public static class ListBoxExtensions
{
    public static readonly AttachedProperty<object?> ScrollToItemProperty =
        AvaloniaProperty.RegisterAttached<ListBox, object?>("ScrollToItem", typeof(ListBoxExtensions));

    public static void SetScrollToItem(ListBox element, object? value) => element.SetValue(ScrollToItemProperty, value);
    public static object? GetScrollToItem(ListBox element) => element.GetValue(ScrollToItemProperty);

    static ListBoxExtensions()
    {
        ScrollToItemProperty.Changed.Subscribe(OnScrollToItemChanged);
    }

    private static void OnScrollToItemChanged(AvaloniaPropertyChangedEventArgs<object?> e)
    {
        if (e is { Sender: ListBox listBox, NewValue.Value: not null })
        {
            listBox.ScrollIntoView(e.NewValue.Value);
        }
    }
}