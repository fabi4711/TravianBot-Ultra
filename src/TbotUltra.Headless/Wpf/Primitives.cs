using System.Collections;
using System.Windows.Headless;
using System.Windows.Input;
using System.Windows.Media;

namespace System.Windows.Controls.Primitives;

public abstract class ButtonBase : ContentControl
{
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ButtonBase));

    public ICommand? Command { get => Get<ICommand?>(null); set => Set(value); }
    public object? CommandParameter { get => Get<object?>(null); set => Set(value); }
    public IInputElement? CommandTarget { get => Get<IInputElement?>(null); set => Set(value); }
    public ClickMode ClickMode { get => Get(ClickMode.Release); set => Set(value); }
    public bool IsPressed => false;

    public event RoutedEventHandler? Click { add => AddNamed(nameof(Click), value); remove => RemoveNamed(nameof(Click), value); }

    /// <summary>What a user click does: raise Click (bubbling) and run the bound command.</summary>
    public virtual void PerformClick()
    {
        if (!IsEnabled)
        {
            return;
        }

        OnClick();
    }

    protected virtual void OnClick()
    {
        RaiseHeadlessEvent(nameof(Click), new RoutedEventArgs(ClickEvent, this));
        var command = Command;
        var parameter = CommandParameter;
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }
}

public enum ClickMode
{
    Release,
    Press,
    Hover,
}

public class ToggleButton : ButtonBase
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool?), typeof(ToggleButton), new PropertyMetadata(false));
    public static readonly RoutedEvent CheckedEvent = EventManager.RegisterRoutedEvent(nameof(Checked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToggleButton));
    public static readonly RoutedEvent UncheckedEvent = EventManager.RegisterRoutedEvent(nameof(Unchecked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToggleButton));
    public static readonly RoutedEvent IndeterminateEvent = EventManager.RegisterRoutedEvent(nameof(Indeterminate), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ToggleButton));

    public bool? IsChecked
    {
        get => Get<bool?>(false);
        set
        {
            if (Set(value))
            {
                OnCheckedChanged(value);
            }
        }
    }

    public bool IsThreeState { get => Get(false); set => Set(value); }

    public event RoutedEventHandler? Checked { add => AddNamed(nameof(Checked), value); remove => RemoveNamed(nameof(Checked), value); }
    public event RoutedEventHandler? Unchecked { add => AddNamed(nameof(Unchecked), value); remove => RemoveNamed(nameof(Unchecked), value); }
    public event RoutedEventHandler? Indeterminate { add => AddNamed(nameof(Indeterminate), value); remove => RemoveNamed(nameof(Indeterminate), value); }

    protected virtual void OnCheckedChanged(bool? value)
    {
        var (name, routedEvent) = value switch
        {
            true => (nameof(Checked), CheckedEvent),
            false => (nameof(Unchecked), UncheckedEvent),
            null => (nameof(Indeterminate), IndeterminateEvent),
        };
        RaiseHeadlessEvent(name, new RoutedEventArgs(routedEvent, this));
    }

    protected override void OnClick()
    {
        IsChecked = IsThreeState
            ? IsChecked switch { true => null, null => false, false => true }
            : IsChecked != true;
        base.OnClick();
    }
}

public abstract class Selector : ItemsControl
{
    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(nameof(SelectionChanged), RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(Selector));
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(Selector));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(Selector), new PropertyMetadata(-1));
    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(Selector));

    private bool _changingSelection;

    public object? SelectedItem
    {
        get => Get<object?>(null);
        set => ChangeSelection(value);
    }

    public int SelectedIndex
    {
        get => SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        set => ChangeSelection(value >= 0 && value < Items.Count ? Items[value] : null);
    }

    public string SelectedValuePath { get => Get(string.Empty); set => Set(value); }

    public object? SelectedValue
    {
        get => SelectedItem is null ? null : HeadlessCollections.GetPathValue(SelectedItem, SelectedValuePath);
        set
        {
            object? match = null;
            foreach (var item in Items)
            {
                if (Equals(HeadlessCollections.GetPathValue(item, SelectedValuePath), value))
                {
                    match = item;
                    break;
                }
            }

            ChangeSelection(match);
        }
    }

    public bool? IsSynchronizedWithCurrentItem { get => Get<bool?>(null); set => Set(value); }

    public event SelectionChangedEventHandler? SelectionChanged { add => AddNamed(nameof(SelectionChanged), value); remove => RemoveNamed(nameof(SelectionChanged), value); }

    public static bool GetIsSelected(DependencyObject element) => element is TabItem { IsSelected: true } || element is ListBoxItem { IsSelected: true };

    protected virtual void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
    }

    protected override void OnItemsSourceChanged() => ValidateSelection();

    internal override void OnItemsChangedInternal()
    {
        base.OnItemsChangedInternal();
        ValidateSelection();
    }

    /// <summary>
    /// Selecting from the web UI: same path as a user click, so SelectionChanged handlers run with the
    /// usual Added/Removed items.
    /// </summary>
    public void HeadlessSelect(object? item) => ChangeSelection(item);

    private void ValidateSelection()
    {
        var selected = SelectedItem;
        if (selected is not null && !Items.Contains(selected))
        {
            ChangeSelection(null);
        }
    }

    private void ChangeSelection(object? item)
    {
        var old = Get<object?>(null, nameof(SelectedItem));
        if (Equals(old, item) || _changingSelection)
        {
            return;
        }

        if (item is not null && !Items.Contains(item))
        {
            // WPF ignores a SelectedItem that is not in Items (after a source change it is re-validated).
            return;
        }

        _changingSelection = true;
        try
        {
            Set(item, nameof(SelectedItem));
            RaisePropertyChanged(nameof(SelectedIndex));
            RaisePropertyChanged(nameof(SelectedValue));
            OnSelectedItemChanged(old, item);
            Items.RaiseCurrentChanged();
        }
        finally
        {
            _changingSelection = false;
        }

        var removed = old is null ? Array.Empty<object>() : new[] { old };
        var added = item is null ? Array.Empty<object>() : new[] { item };
        RaiseHeadlessEvent(nameof(SelectionChanged), new SelectionChangedEventArgs(SelectionChangedEvent, removed, added) { Source = this });
    }
}

public abstract class MultiSelector : Selector
{
}

public abstract class RangeBase : Control
{
    public double Minimum { get => Get(0d); set => Set(value); }
    public double Maximum { get => Get(100d); set => Set(value); }
    public double SmallChange { get => Get(1d); set => Set(value); }
    public double LargeChange { get => Get(10d); set => Set(value); }

    public double Value
    {
        get => Get(0d);
        set
        {
            var old = Value;
            if (Set(value))
            {
                RaiseHeadlessEvent(nameof(ValueChanged), new RoutedPropertyChangedEventArgs<double>(old, value));
            }
        }
    }

    public event RoutedPropertyChangedEventHandler<double>? ValueChanged { add => AddNamed(nameof(ValueChanged), value); remove => RemoveNamed(nameof(ValueChanged), value); }
}

public class Popup : FrameworkElement
{
    public bool IsOpen { get => Get(false); set => Set(value); }
    public bool StaysOpen { get => Get(true); set => Set(value); }
    public UIElement? PlacementTarget { get => Get<UIElement?>(null); set => Set(value); }
    public PlacementMode Placement { get => Get(PlacementMode.Bottom); set => Set(value); }
    public bool AllowsTransparency { get => Get(false); set => Set(value); }

    public UIElement? Child
    {
        get => Get<UIElement?>(null);
        set
        {
            if (value is not null)
            {
                value.HeadlessParent = this;
            }

            Set(value);
        }
    }

    public event EventHandler? Opened;
    public event EventHandler? Closed;
}

public enum PlacementMode
{
    Absolute,
    Relative,
    Bottom,
    Center,
    Right,
    AbsolutePoint,
    RelativePoint,
    Mouse,
    MousePoint,
    Left,
    Top,
    Custom,
}

public class ScrollBar : RangeBase
{
}

public class Thumb : Control
{
}

public abstract class TextBoxBase : Control
{
    public bool IsReadOnly { get => Get(false); set => Set(value); }
    public bool AcceptsReturn { get => Get(false); set => Set(value); }
    public bool AcceptsTab { get => Get(false); set => Set(value); }
    public bool IsUndoEnabled { get => Get(true); set => Set(value); }
    public ScrollBarVisibility VerticalScrollBarVisibility { get => Get(ScrollBarVisibility.Hidden); set => Set(value); }
    public ScrollBarVisibility HorizontalScrollBarVisibility { get => Get(ScrollBarVisibility.Hidden); set => Set(value); }
    public Brush? CaretBrush { get => Get<Brush?>(null); set => Set(value); }
    public Brush? SelectionBrush { get => Get<Brush?>(null); set => Set(value); }

    public event TextChangedEventHandler? TextChanged { add => AddNamed(nameof(TextChanged), value); remove => RemoveNamed(nameof(TextChanged), value); }
    public event RoutedEventHandler? SelectionChanged { add => AddNamed(nameof(SelectionChanged), value); remove => RemoveNamed(nameof(SelectionChanged), value); }

    public void SelectAll()
    {
    }

    public void ScrollToEnd()
    {
    }

    public void ScrollToHome()
    {
    }

    public void Copy()
    {
    }

    public void Paste()
    {
    }

    public bool Undo() => false;
}


public class StatusBar : ItemsControl
{
}

public class UniformGrid : Panel
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int), typeof(UniformGrid));
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(int), typeof(UniformGrid));

    public int Columns { get => Get(0); set => Set(value); }
    public int Rows { get => Get(0); set => Set(value); }
}


public class TabPanel : Panel
{
}

public static class LayoutInformation
{
}
