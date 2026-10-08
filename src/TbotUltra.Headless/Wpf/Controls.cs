using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Headless;
using System.Windows.Input;
using System.Windows.Media;

namespace System.Windows.Controls;

public enum Orientation
{
    Horizontal,
    Vertical,
}

public enum ScrollBarVisibility
{
    Disabled,
    Auto,
    Hidden,
    Visible,
}

public enum SelectionMode
{
    Single,
    Multiple,
    Extended,
}

public enum Dock
{
    Left,
    Top,
    Right,
    Bottom,
}

public enum CharacterCasing
{
    Normal,
    Lower,
    Upper,
}

public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

public class SelectionChangedEventArgs : RoutedEventArgs
{
    public SelectionChangedEventArgs(RoutedEvent? id, IList removedItems, IList addedItems)
        : base(id)
    {
        RemovedItems = removedItems;
        AddedItems = addedItems;
    }

    public IList AddedItems { get; }
    public IList RemovedItems { get; }
}

public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);

public class TextChangedEventArgs : RoutedEventArgs
{
    public TextChangedEventArgs(RoutedEvent? id, UndoAction action)
        : base(id)
    {
    }

    public ICollection<TextChange> Changes { get; } = new List<TextChange>();
}

public sealed class TextChange
{
}

public enum UndoAction
{
    None,
    Merge,
    Undo,
    Redo,
    Clear,
    Create,
}

public delegate void ToolTipEventHandler(object sender, ToolTipEventArgs e);

public class ToolTipEventArgs : RoutedEventArgs
{
}

public delegate void ContextMenuEventHandler(object sender, ContextMenuEventArgs e);

public class ContextMenuEventArgs : RoutedEventArgs
{
}

public delegate void ScrollChangedEventHandler(object sender, ScrollChangedEventArgs e);

public class ScrollChangedEventArgs : RoutedEventArgs
{
    public double VerticalChange { get; init; }
    public double VerticalOffset { get; init; }
    public double ExtentHeight { get; init; }
    public double ViewportHeight { get; init; }
}

public class Control : FrameworkElement
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(Control));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(Control));
    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(nameof(BorderBrush), typeof(Brush), typeof(Control));
    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(Control));
    public static readonly DependencyProperty IsTabStopProperty = DependencyProperty.Register(nameof(IsTabStop), typeof(bool), typeof(Control));
    public static readonly DependencyProperty TemplateProperty = DependencyProperty.Register(nameof(Template), typeof(ControlTemplate), typeof(Control));
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(Control));
    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(Control));
    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(Control));
    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(nameof(BorderThickness), typeof(Thickness), typeof(Control));

    public Brush? Background { get => Get<Brush?>(null); set => Set(value); }
    public Brush? Foreground { get => Get<Brush?>(null); set => Set(value); }
    public Brush? BorderBrush { get => Get<Brush?>(null); set => Set(value); }
    public Thickness BorderThickness { get => Get(default(Thickness)); set => Set(value); }
    public Thickness Padding { get => Get(default(Thickness)); set => Set(value); }
    public double FontSize { get => Get(12d); set => Set(value); }
    public FontWeight FontWeight { get => Get(FontWeight.Normal); set => Set(value); }
    public FontStyle FontStyle { get => Get(FontStyle.Normal); set => Set(value); }
    public FontFamily? FontFamily { get => Get<FontFamily?>(null); set => Set(value); }
    public bool IsTabStop { get => Get(true); set => Set(value); }
    public int TabIndex { get => Get(0); set => Set(value); }
    public HorizontalAlignment HorizontalContentAlignment { get => Get(HorizontalAlignment.Left); set => Set(value); }
    public VerticalAlignment VerticalContentAlignment { get => Get(VerticalAlignment.Top); set => Set(value); }
    public ControlTemplate? Template { get => Get<ControlTemplate?>(null); set => Set(value); }

    public event MouseButtonEventHandler? MouseDoubleClick { add => AddNamed(nameof(MouseDoubleClick), value); remove => RemoveNamed(nameof(MouseDoubleClick), value); }
    public event MouseButtonEventHandler? PreviewMouseDoubleClick { add => AddNamed(nameof(PreviewMouseDoubleClick), value); remove => RemoveNamed(nameof(PreviewMouseDoubleClick), value); }
}

public class ContentControl : Control
{
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(ContentControl));

    public object? Content
    {
        get => Get<object?>(null);
        set
        {
            if (Get<object?>(null, nameof(Content)) is UIElement oldElement && ReferenceEquals(oldElement.HeadlessParent, this))
            {
                oldElement.HeadlessParent = null;
            }

            if (value is UIElement element)
            {
                element.HeadlessParent = this;
            }

            Set(value);
        }
    }

    public DataTemplate? ContentTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
    public string? ContentStringFormat { get => Get<string?>(null); set => Set(value); }
    public bool HasContent => Content is not null;
}

public class UserControl : ContentControl
{
}

public class Label : ContentControl
{
    public UIElement? Target { get => Get<UIElement?>(null); set => Set(value); }
}

public class HeaderedContentControl : ContentControl
{
    public object? Header
    {
        get => Get<object?>(null);
        set
        {
            if (value is UIElement element)
            {
                element.HeadlessParent = this;
            }

            Set(value);
        }
    }

    public DataTemplate? HeaderTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
}

public class Expander : HeaderedContentControl
{
    public bool IsExpanded { get => Get(false); set => Set(value); }

    public event RoutedEventHandler? Expanded { add => AddNamed(nameof(Expanded), value); remove => RemoveNamed(nameof(Expanded), value); }
    public event RoutedEventHandler? Collapsed { add => AddNamed(nameof(Collapsed), value); remove => RemoveNamed(nameof(Collapsed), value); }
}

public class GroupBox : HeaderedContentControl
{
}

public class TabItem : HeaderedContentControl
{
    public bool IsSelected
    {
        get => Get(false);
        set
        {
            if (Set(value) && value && HeadlessParent is TabControl tabControl)
            {
                tabControl.SelectedItem = this;
            }
        }
    }

    internal void SetSelectedFromParent(bool value) => Set(value, nameof(IsSelected));
}

public class Button : ButtonBase
{
    public bool IsDefault { get => Get(false); set => Set(value); }
    public bool IsCancel { get => Get(false); set => Set(value); }
}

public class CheckBox : ToggleButton
{
}

public class RadioButton : ToggleButton
{
    public string GroupName { get => Get(string.Empty); set => Set(value); }

    protected override void OnCheckedChanged(bool? value)
    {
        if (value == true)
        {
            HeadlessXaml.UncheckRadioGroup(this);
        }

        base.OnCheckedChanged(value);
    }
}

public class TextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextBlock));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(TextBlock));
    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(TextBlock));
    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(TextBlock));
    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(TextBlock));
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(TextBlock));

    public TextBlock()
    {
        Inlines = new Documents.InlineCollection(this);
    }

    public TextBlock(Documents.Inline inline)
        : this()
    {
        Inlines.Add(inline);
    }

    public string Text
    {
        get => Inlines.Count > 0 ? Inlines.ToText() : Get(string.Empty);
        set
        {
            if (Inlines.Count > 0)
            {
                Inlines.ClearSilently();
            }

            Set(value ?? string.Empty);
        }
    }

    public Documents.InlineCollection Inlines { get; }
    public Brush? Foreground { get => Get<Brush?>(null); set => Set(value); }
    public Brush? Background { get => Get<Brush?>(null); set => Set(value); }
    public TextWrapping TextWrapping { get => Get(TextWrapping.NoWrap); set => Set(value); }
    public TextTrimming TextTrimming { get => Get(TextTrimming.None); set => Set(value); }
    public TextAlignment TextAlignment { get => Get(TextAlignment.Left); set => Set(value); }
    public double FontSize { get => Get(12d); set => Set(value); }
    public FontWeight FontWeight { get => Get(FontWeight.Normal); set => Set(value); }
    public FontStyle FontStyle { get => Get(FontStyle.Normal); set => Set(value); }
    public FontFamily? FontFamily { get => Get<FontFamily?>(null); set => Set(value); }
    public Thickness Padding { get => Get(default(Thickness)); set => Set(value); }
    public double LineHeight { get => Get(double.NaN); set => Set(value); }
    public TextDecorationCollection? TextDecorations { get => Get<TextDecorationCollection?>(null); set => Set(value); }

    internal void NotifyInlinesChanged() => RaisePropertyChanged(nameof(Text));
}

public class TextDecorationCollection
{
}

public class TextBox : TextBoxBase
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextBox));
    public static readonly RoutedEvent TextChangedEvent = EventManager.RegisterRoutedEvent(nameof(TextChanged), RoutingStrategy.Bubble, typeof(TextChangedEventHandler), typeof(TextBox));

    public string Text
    {
        get => Get(string.Empty);
        set
        {
            if (Set(value ?? string.Empty))
            {
                RaiseHeadlessEvent(nameof(TextChanged), new TextChangedEventArgs(TextChangedEvent, UndoAction.None) { Source = this });
            }
        }
    }

    public int MaxLength { get => Get(0); set => Set(value); }
    public int MaxLines { get => Get(int.MaxValue); set => Set(value); }
    public int MinLines { get => Get(1); set => Set(value); }
    public int CaretIndex { get => Get(0); set => Set(value); }
    public int SelectionStart { get => Get(0); set => Set(value); }
    public int SelectionLength { get => Get(0); set => Set(value); }
    public string SelectedText { get => Get(string.Empty); set => Set(value); }
    public TextWrapping TextWrapping { get => Get(TextWrapping.NoWrap); set => Set(value); }
    public TextAlignment TextAlignment { get => Get(TextAlignment.Left); set => Set(value); }
    public CharacterCasing CharacterCasing { get => Get(CharacterCasing.Normal); set => Set(value); }
    public int LineCount => Text.Split('\n').Length;

    public void Clear() => Text = string.Empty;

    public void Select(int start, int length)
    {
        SelectionStart = start;
        SelectionLength = length;
    }

    public void ScrollToLine(int lineIndex)
    {
    }
}

public sealed class PasswordBox : Control
{
    public string Password
    {
        get => Get(string.Empty);
        set
        {
            if (Set(value ?? string.Empty))
            {
                RaiseHeadlessEvent(nameof(PasswordChanged), new RoutedEventArgs(null, this));
            }
        }
    }

    public char PasswordChar { get => Get('●'); set => Set(value); }
    public int MaxLength { get => Get(0); set => Set(value); }

    public event RoutedEventHandler? PasswordChanged { add => AddNamed(nameof(PasswordChanged), value); remove => RemoveNamed(nameof(PasswordChanged), value); }

    public void Clear() => Password = string.Empty;

    public void SelectAll()
    {
    }
}

public sealed class ItemCollection : IList, INotifyCollectionChanged, ICollectionView
{
    private readonly ItemsControl _owner;
    private readonly ObservableCollection<object?> _items = new();
    private IEnumerable? _source;
    private Predicate<object>? _filter;

    internal ItemCollection(ItemsControl owner)
    {
        _owner = owner;
        _items.CollectionChanged += (_, e) =>
        {
            if (_source is null)
            {
                CollectionChanged?.Invoke(this, e);
                _owner.OnItemsChangedInternal();
            }
        };
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public event EventHandler? CurrentChanged;

    public event CurrentChangingEventHandler? CurrentChanging;

    public bool IsUsingItemsSource => _source is not null;

    public object this[int index]
    {
        get => Snapshot()[index]!;
        set
        {
            EnsureDirect();
            _items[index] = value;
        }
    }

    public int Count => Snapshot().Count;

    public bool IsEmpty => Count == 0;

    public bool IsReadOnly => _source is not null;

    public bool IsFixedSize => false;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    public System.Globalization.CultureInfo? Culture { get; set; }

    public IEnumerable SourceCollection => _source ?? _items;

    public Predicate<object>? Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            Refresh();
        }
    }

    public bool CanFilter => true;

    public SortDescriptionCollection SortDescriptions { get; } = new();

    public bool CanSort => true;

    public bool CanGroup => false;

    public ObservableCollection<GroupDescription> GroupDescriptions { get; } = new();

    public ReadOnlyObservableCollection<object>? Groups => null;

    public object? CurrentItem => _owner is Selector selector ? selector.SelectedItem : null;

    public int CurrentPosition => _owner is Selector selector ? selector.SelectedIndex : -1;

    public bool IsCurrentAfterLast => false;

    public bool IsCurrentBeforeFirst => CurrentPosition < 0;

    public bool NeedsRefresh => false;

    internal void SetSource(IEnumerable? source)
    {
        if (_source is INotifyCollectionChanged oldObservable)
        {
            oldObservable.CollectionChanged -= OnSourceChanged;
        }

        _source = source;
        if (_source is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnSourceChanged;
        }

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
        _owner.OnItemsChangedInternal();
    }

    /// <summary>The visible items: the source (or direct items) after filter and sort.</summary>
    public IReadOnlyList<object?> Snapshot()
    {
        IEnumerable<object?> items = (_source ?? _items).Cast<object?>();
        var sourceView = _source is not null ? CollectionViewSource.TryGetExistingView(_source) : null;
        if (sourceView?.Filter is { } sourceFilter)
        {
            items = items.Where(item => item is not null && sourceFilter(item));
        }

        if (_filter is { } filter)
        {
            items = items.Where(item => item is not null && filter(item));
        }

        var sorts = SortDescriptions.Count > 0 ? SortDescriptions : sourceView?.SortDescriptions;
        if (sorts is { Count: > 0 })
        {
            items = HeadlessCollections.Sort(items, sorts);
        }

        return items.ToList();
    }

    public int Add(object? value)
    {
        EnsureDirect();
        if (value is UIElement element)
        {
            element.HeadlessParent = _owner;
        }

        _items.Add(value);
        return _items.Count - 1;
    }

    public void Clear()
    {
        EnsureDirect();
        _items.Clear();
    }

    public bool Contains(object? value) => Snapshot().Contains(value);

    public int IndexOf(object? value)
    {
        var snapshot = Snapshot();
        for (var i = 0; i < snapshot.Count; i++)
        {
            if (Equals(snapshot[i], value))
            {
                return i;
            }
        }

        return -1;
    }

    public void Insert(int index, object? value)
    {
        EnsureDirect();
        if (value is UIElement element)
        {
            element.HeadlessParent = _owner;
        }

        _items.Insert(index, value);
    }

    public void Remove(object? value)
    {
        EnsureDirect();
        _items.Remove(value);
    }

    public void RemoveAt(int index)
    {
        EnsureDirect();
        _items.RemoveAt(index);
    }

    public void CopyTo(Array array, int index)
    {
        foreach (var item in Snapshot())
        {
            array.SetValue(item, index++);
        }
    }

    public IEnumerator GetEnumerator() => Snapshot().GetEnumerator();

    public void Refresh()
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        _owner.OnItemsChangedInternal();
    }

    public IDisposable DeferRefresh() => new HeadlessCollections.DeferredRefresh(Refresh);

    public bool MoveCurrentTo(object? item)
    {
        if (_owner is Selector selector)
        {
            selector.SelectedItem = item;
        }

        return true;
    }

    public bool MoveCurrentToFirst() => MoveCurrentToPosition(0);

    public bool MoveCurrentToLast() => MoveCurrentToPosition(Count - 1);

    public bool MoveCurrentToNext() => MoveCurrentToPosition(CurrentPosition + 1);

    public bool MoveCurrentToPrevious() => MoveCurrentToPosition(CurrentPosition - 1);

    public bool MoveCurrentToPosition(int position)
    {
        if (_owner is Selector selector)
        {
            selector.SelectedIndex = position;
        }

        return true;
    }

    private void EnsureDirect()
    {
        if (_source is not null)
        {
            throw new InvalidOperationException("Operation is not valid while ItemsSource is in use. Access and modify elements with ItemsControl.ItemsSource instead.");
        }
    }

    internal void RaiseCurrentChanged()
    {
        CurrentChanging?.Invoke(this, new CurrentChangingEventArgs());
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class ItemsControl : Control
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ItemsControl));

    public ItemsControl()
    {
        Items = new ItemCollection(this);
    }

    public ItemCollection Items { get; }

    public IEnumerable? ItemsSource
    {
        get => Get<IEnumerable?>(null);
        set
        {
            if (Set(value))
            {
                Items.SetSource(value);
                OnItemsSourceChanged();
            }
        }
    }

    public DataTemplate? ItemTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
    public DataTemplateSelector? ItemTemplateSelector { get => Get<DataTemplateSelector?>(null); set => Set(value); }
    public Style? ItemContainerStyle { get => Get<Style?>(null); set => Set(value); }
    public ItemsPanelTemplate? ItemsPanel { get => Get<ItemsPanelTemplate?>(null); set => Set(value); }
    public string DisplayMemberPath { get => Get(string.Empty); set => Set(value); }
    public string? ItemStringFormat { get => Get<string?>(null); set => Set(value); }
    public int AlternationCount { get => Get(0); set => Set(value); }
    public bool HasItems => Items.Count > 0;
    public ItemContainerGenerator ItemContainerGenerator => new(this);
    public ObservableCollection<GroupStyle> GroupStyle { get; } = new();

    public static ItemsControl? ItemsControlFromItemContainer(DependencyObject container)
        => (container as UIElement)?.HeadlessParent as ItemsControl;

    public static ItemsControl? GetItemsOwner(DependencyObject element) => element as ItemsControl;

    internal virtual void OnItemsChangedInternal() => RaisePropertyChanged(nameof(Items));

    protected virtual void OnItemsSourceChanged()
    {
    }
}

public class GroupStyle
{
    public DataTemplate? HeaderTemplate { get; set; }
}

public class DataTemplateSelector
{
    public virtual DataTemplate? SelectTemplate(object item, DependencyObject container) => null;
}

public sealed class ItemContainerGenerator
{
    private readonly ItemsControl _owner;

    internal ItemContainerGenerator(ItemsControl owner)
    {
        _owner = owner;
    }

    public GeneratorStatus Status => GeneratorStatus.ContainersGenerated;

    public event EventHandler? StatusChanged;

    public DependencyObject? ContainerFromItem(object? item) => item as DependencyObject;

    public DependencyObject? ContainerFromIndex(int index)
        => index >= 0 && index < _owner.Items.Count ? _owner.Items[index] as DependencyObject : null;

    public object? ItemFromContainer(DependencyObject container) => container;

    public int IndexFromContainer(DependencyObject container) => _owner.Items.IndexOf(container);
}

public enum GeneratorStatus
{
    NotStarted,
    GeneratingContainers,
    ContainersGenerated,
    Error,
}

public class ComboBox : Selector
{
    public bool IsEditable { get => Get(false); set => Set(value); }
    public bool IsReadOnly { get => Get(false); set => Set(value); }
    public bool IsDropDownOpen { get => Get(false); set => Set(value); }
    public double MaxDropDownHeight { get => Get(360d); set => Set(value); }
    public bool StaysOpenOnEdit { get => Get(false); set => Set(value); }
    public bool IsTextSearchEnabled { get => Get(true); set => Set(value); }

    public string Text
    {
        get => Get<string?>(null) ?? HeadlessCollections.DisplayText(SelectedItem, DisplayMemberPath);
        set => Set(value);
    }

    public event EventHandler? DropDownOpened { add => AddNamed(nameof(DropDownOpened), value); remove => RemoveNamed(nameof(DropDownOpened), value); }
    public event EventHandler? DropDownClosed { add => AddNamed(nameof(DropDownClosed), value); remove => RemoveNamed(nameof(DropDownClosed), value); }
}

public class ComboBoxItem : ListBoxItem
{
}

public class ListBox : Selector
{
    private readonly ObservableCollection<object?> _selectedItems = new();

    public SelectionMode SelectionMode { get => Get(SelectionMode.Single); set => Set(value); }
    public IList SelectedItems => _selectedItems;

    public void ScrollIntoView(object item)
    {
    }

    public void SelectAll()
    {
        _selectedItems.Clear();
        foreach (var item in Items)
        {
            _selectedItems.Add(item);
        }
    }

    public void UnselectAll()
    {
        _selectedItems.Clear();
        SelectedItem = null;
    }

    protected override void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (SelectionMode == SelectionMode.Single || !_selectedItems.Contains(newItem))
        {
            _selectedItems.Clear();
            if (newItem is not null)
            {
                _selectedItems.Add(newItem);
            }
        }
    }
}

public class ListBoxItem : ContentControl
{
    public bool IsSelected
    {
        get => Get(false);
        set
        {
            if (Set(value) && value && HeadlessParent is Selector selector && !ReferenceEquals(selector.SelectedItem, this))
            {
                selector.SelectedItem = this;
            }
        }
    }

    internal void SetSelectedFromParent(bool value) => Set(value, nameof(IsSelected));
}

public class ListView : ListBox
{
    public ViewBase? View { get => Get<ViewBase?>(null); set => Set(value); }
}

public class ViewBase
{
}

public class TabControl : Selector
{
    public Dock TabStripPlacement { get => Get(Dock.Top); set => Set(value); }

    public object? SelectedContent => (SelectedItem as TabItem)?.Content ?? SelectedItem;

    protected override void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (oldItem is TabItem oldTab)
        {
            oldTab.SetSelectedFromParent(false);
        }

        if (newItem is TabItem newTab)
        {
            newTab.SetSelectedFromParent(true);
        }
    }

    internal override void OnItemsChangedInternal()
    {
        base.OnItemsChangedInternal();
        if (SelectedItem is null)
        {
            var preselected = Items.OfType<TabItem>().FirstOrDefault(tab => tab.IsSelected && tab.IsEnabled)
                              ?? Items.OfType<TabItem>().FirstOrDefault(tab => tab.Visibility == Visibility.Visible && tab.IsEnabled);
            if (preselected is not null)
            {
                SelectedItem = preselected;
            }
        }
    }
}

public abstract class Panel : FrameworkElement
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(Panel));

    protected Panel()
    {
        Children = new UIElementCollection(this);
    }

    public UIElementCollection Children { get; }
    protected internal UIElementCollection InternalChildren => Children;
    public Brush? Background { get => Get<Brush?>(null); set => Set(value); }
    public bool IsItemsHost { get => Get(false); set => Set(value); }
}

public sealed class UIElementCollection : IList, IEnumerable<UIElement>
{
    private readonly FrameworkElement _owner;
    private readonly ObservableCollection<UIElement> _items = new();

    internal UIElementCollection(FrameworkElement owner)
    {
        _owner = owner;
        _items.CollectionChanged += (_, _) => HeadlessXaml.NotifyChanged(_owner, "Children");
    }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public bool IsFixedSize => false;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    object? IList.this[int index]
    {
        get => _items[index];
        set => _items[index] = Adopt((UIElement)value!);
    }

    public UIElement this[int index]
    {
        get => _items[index];
        set => _items[index] = Adopt(value);
    }

    public int Add(UIElement element)
    {
        _items.Add(Adopt(element));
        return _items.Count - 1;
    }

    int IList.Add(object? value) => Add((UIElement)value!);

    public void Insert(int index, UIElement element) => _items.Insert(index, Adopt(element));

    void IList.Insert(int index, object? value) => Insert(index, (UIElement)value!);

    public void Remove(UIElement element)
    {
        if (_items.Remove(element))
        {
            element.HeadlessParent = null;
        }
    }

    void IList.Remove(object? value) => Remove((UIElement)value!);

    public void RemoveAt(int index)
    {
        _items[index].HeadlessParent = null;
        _items.RemoveAt(index);
    }

    public void RemoveRange(int index, int count)
    {
        for (var i = 0; i < count; i++)
        {
            RemoveAt(index);
        }
    }

    public void Clear()
    {
        foreach (var item in _items)
        {
            item.HeadlessParent = null;
        }

        _items.Clear();
    }

    public bool Contains(UIElement element) => _items.Contains(element);

    bool IList.Contains(object? value) => value is UIElement element && _items.Contains(element);

    public int IndexOf(UIElement element) => _items.IndexOf(element);

    int IList.IndexOf(object? value) => value is UIElement element ? _items.IndexOf(element) : -1;

    public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    public IEnumerator<UIElement> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    private UIElement Adopt(UIElement element)
    {
        element.HeadlessParent = _owner;
        if (_owner.IsLoaded && element is FrameworkElement framework)
        {
            HeadlessXaml.RaiseLoadedTree(framework);
        }

        return element;
    }
}

public class StackPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(StackPanel));

    public Orientation Orientation { get => Get(Orientation.Vertical); set => Set(value); }
}

public class WrapPanel : Panel
{
    public Orientation Orientation { get => Get(Orientation.Horizontal); set => Set(value); }
    public double ItemWidth { get => Get(double.NaN); set => Set(value); }
    public double ItemHeight { get => Get(double.NaN); set => Set(value); }
}

public class VirtualizingStackPanel : StackPanel
{
    public static readonly DependencyProperty IsVirtualizingProperty = DependencyProperty.RegisterAttached("IsVirtualizing", typeof(bool), typeof(VirtualizingStackPanel));

    public static void SetIsVirtualizing(DependencyObject element, bool value) => element.SetValue(IsVirtualizingProperty, value);
}

public class DockPanel : Panel
{
    public bool LastChildFill { get => Get(true); set => Set(value); }

    public static void SetDock(UIElement element, Dock dock) => HeadlessXaml.SetAttached(element, "DockPanel.Dock", dock);

    public static Dock GetDock(UIElement element) => HeadlessXaml.GetAttached(element, "DockPanel.Dock", Dock.Left);
}

public class Canvas : Panel
{
    public static void SetLeft(UIElement element, double length) => HeadlessXaml.SetAttached(element, "Canvas.Left", length);

    public static void SetTop(UIElement element, double length) => HeadlessXaml.SetAttached(element, "Canvas.Top", length);

    public static void SetRight(UIElement element, double length) => HeadlessXaml.SetAttached(element, "Canvas.Right", length);

    public static void SetBottom(UIElement element, double length) => HeadlessXaml.SetAttached(element, "Canvas.Bottom", length);

    public static double GetLeft(UIElement element) => HeadlessXaml.GetAttached(element, "Canvas.Left", double.NaN);

    public static double GetTop(UIElement element) => HeadlessXaml.GetAttached(element, "Canvas.Top", double.NaN);

    public static void SetZIndex(UIElement element, int value) => HeadlessXaml.SetAttached(element, "Panel.ZIndex", value);
}

public class Grid : Panel
{
    public ColumnDefinitionCollection ColumnDefinitions { get; } = new();
    public RowDefinitionCollection RowDefinitions { get; } = new();
    public bool ShowGridLines { get => Get(false); set => Set(value); }

    public static void SetRow(UIElement element, int value) => HeadlessXaml.SetAttached(element, "Grid.Row", value);

    public static void SetColumn(UIElement element, int value) => HeadlessXaml.SetAttached(element, "Grid.Column", value);

    public static void SetRowSpan(UIElement element, int value) => HeadlessXaml.SetAttached(element, "Grid.RowSpan", value);

    public static void SetColumnSpan(UIElement element, int value) => HeadlessXaml.SetAttached(element, "Grid.ColumnSpan", value);

    public static int GetRow(UIElement element) => HeadlessXaml.GetAttached(element, "Grid.Row", 0);

    public static int GetColumn(UIElement element) => HeadlessXaml.GetAttached(element, "Grid.Column", 0);

    public static int GetRowSpan(UIElement element) => HeadlessXaml.GetAttached(element, "Grid.RowSpan", 1);

    public static int GetColumnSpan(UIElement element) => HeadlessXaml.GetAttached(element, "Grid.ColumnSpan", 1);

    public static void SetIsSharedSizeScope(UIElement element, bool value)
    {
    }
}

public abstract class DefinitionBase : FrameworkContentElement
{
    public string SharedSizeGroup { get => Get(string.Empty); set => Set(value); }
}

public class FrameworkContentElement : DependencyObject
{
    public Cursor? Cursor { get => Get<Cursor?>(null); set => Set(value); }
    public DependencyObject? Parent { get; internal set; }

    public string Name { get => Get(string.Empty); set => Set(value); }
    public object? Tag { get => Get<object?>(null); set => Set(value); }
}

public class ColumnDefinition : DefinitionBase
{
    public GridLength Width { get => Get(new GridLength(1, GridUnitType.Star)); set => Set(value); }
    public double MinWidth { get => Get(0d); set => Set(value); }
    public double MaxWidth { get => Get(double.PositiveInfinity); set => Set(value); }
    public double ActualWidth => Width.Value;
}

public class RowDefinition : DefinitionBase
{
    public GridLength Height { get => Get(new GridLength(1, GridUnitType.Star)); set => Set(value); }
    public double MinHeight { get => Get(0d); set => Set(value); }
    public double MaxHeight { get => Get(double.PositiveInfinity); set => Set(value); }
    public double ActualHeight => Height.Value;
}

public sealed class ColumnDefinitionCollection : Collection<ColumnDefinition>
{
}

public sealed class RowDefinitionCollection : Collection<RowDefinition>
{
}

public class Decorator : FrameworkElement
{
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
}

public class Border : Decorator
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(Border));
    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(nameof(BorderBrush), typeof(Brush), typeof(Border));

    public Brush? Background { get => Get<Brush?>(null); set => Set(value); }
    public Brush? BorderBrush { get => Get<Brush?>(null); set => Set(value); }
    public Thickness BorderThickness { get => Get(default(Thickness)); set => Set(value); }
    public CornerRadius CornerRadius { get => Get(default(CornerRadius)); set => Set(value); }
    public Thickness Padding { get => Get(default(Thickness)); set => Set(value); }
}

public class Viewbox : Decorator
{
    public Stretch Stretch { get => Get(Stretch.Uniform); set => Set(value); }
}

public class ScrollViewer : ContentControl
{
    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = DependencyProperty.RegisterAttached(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer));
    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = DependencyProperty.RegisterAttached(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer));

    public ScrollBarVisibility HorizontalScrollBarVisibility { get => Get(ScrollBarVisibility.Disabled); set => Set(value); }
    public ScrollBarVisibility VerticalScrollBarVisibility { get => Get(ScrollBarVisibility.Visible); set => Set(value); }
    public double VerticalOffset { get => Get(0d); private set => Set(value); }
    public double HorizontalOffset { get => Get(0d); private set => Set(value); }
    public double ScrollableHeight => 0;
    public double ScrollableWidth => 0;
    public double ExtentHeight => 0;
    public double ViewportHeight => 0;
    public bool CanContentScroll { get => Get(false); set => Set(value); }

    public event ScrollChangedEventHandler? ScrollChanged { add => AddNamed(nameof(ScrollChanged), value); remove => RemoveNamed(nameof(ScrollChanged), value); }

    public void ScrollToEnd()
    {
    }

    public void ScrollToHome()
    {
    }

    public void ScrollToTop() => VerticalOffset = 0;

    public void ScrollToBottom()
    {
    }

    public void ScrollToVerticalOffset(double offset) => VerticalOffset = offset;

    public void ScrollToHorizontalOffset(double offset) => HorizontalOffset = offset;

    public void LineDown()
    {
    }

    public void LineUp()
    {
    }

    public static ScrollBarVisibility GetHorizontalScrollBarVisibility(DependencyObject element)
        => element is ScrollViewer viewer ? viewer.HorizontalScrollBarVisibility : element.GetValue(HorizontalScrollBarVisibilityProperty) as ScrollBarVisibility? ?? ScrollBarVisibility.Disabled;

    public static ScrollBarVisibility GetVerticalScrollBarVisibility(DependencyObject element)
        => element is ScrollViewer viewer ? viewer.VerticalScrollBarVisibility : element.GetValue(VerticalScrollBarVisibilityProperty) as ScrollBarVisibility? ?? ScrollBarVisibility.Visible;

    public static void SetHorizontalScrollBarVisibility(DependencyObject element, ScrollBarVisibility value)
        => element.SetValue(HorizontalScrollBarVisibilityProperty, value);

    public static void SetVerticalScrollBarVisibility(DependencyObject element, ScrollBarVisibility value)
        => element.SetValue(VerticalScrollBarVisibilityProperty, value);
}

public class ProgressBar : RangeBase
{
    public bool IsIndeterminate { get => Get(false); set => Set(value); }
    public Orientation Orientation { get => Get(Orientation.Horizontal); set => Set(value); }
}

public class Slider : RangeBase
{
    public double TickFrequency { get => Get(1d); set => Set(value); }
    public bool IsSnapToTickEnabled { get => Get(false); set => Set(value); }
}

public class Image : FrameworkElement
{
    public ImageSource? Source { get => Get<ImageSource?>(null); set => Set(value); }
    public Stretch Stretch { get => Get(Stretch.Uniform); set => Set(value); }
}

public class Separator : Control
{
}

public class ToolTip : ContentControl
{
    public bool IsOpen { get => Get(false); set => Set(value); }
    public PlacementMode Placement { get => Get(PlacementMode.Mouse); set => Set(value); }
}


public static class ToolTipService
{
    public static void SetShowDuration(DependencyObject element, int value)
    {
    }

    public static void SetInitialShowDelay(DependencyObject element, int value)
    {
    }

    public static void SetIsEnabled(DependencyObject element, bool value)
    {
    }

    public static void SetShowOnDisabled(DependencyObject element, bool value)
    {
    }

    public static void SetPlacement(DependencyObject element, PlacementMode value)
    {
    }

    public static void SetBetweenShowDelay(DependencyObject element, int value)
    {
    }
}

public class ContextMenu : ItemsControl
{
    public bool IsOpen { get => Get(false); set => Set(value); }
    public UIElement? PlacementTarget { get => Get<UIElement?>(null); set => Set(value); }
    public PlacementMode Placement { get => Get(PlacementMode.Mouse); set => Set(value); }
}

public class Menu : ItemsControl
{
}

public class MenuItem : ItemsControl
{
    public object? Header { get => Get<object?>(null); set => Set(value); }
    public object? Icon { get => Get<object?>(null); set => Set(value); }
    public bool IsCheckable { get => Get(false); set => Set(value); }
    public bool IsChecked { get => Get(false); set => Set(value); }
    public ICommand? Command { get => Get<ICommand?>(null); set => Set(value); }
    public object? CommandParameter { get => Get<object?>(null); set => Set(value); }
    public string? InputGestureText { get => Get<string?>(null); set => Set(value); }

    public event RoutedEventHandler? Click { add => AddNamed(nameof(Click), value); remove => RemoveNamed(nameof(Click), value); }
}

public class ContentPresenter : FrameworkElement
{
    public object? Content { get => Get<object?>(null); set => Set(value); }
    public DataTemplate? ContentTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
}

public class Page : FrameworkElement
{
}

public class Frame : ContentControl
{
}

public class DatePicker : Control
{
    public DateTime? SelectedDate { get => Get<DateTime?>(null); set => Set(value); }
}

public class InkCanvas : FrameworkElement
{
}

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is Visibility.Visible;
}

public enum VirtualizationMode
{
    Standard,
    Recycling,
}

public enum ScrollUnit
{
    Pixel,
    Item,
}

public abstract class VirtualizingPanel : Panel
{
    public static void SetVirtualizationMode(DependencyObject element, VirtualizationMode value) => element.SetNamedValue("VirtualizingPanel.VirtualizationMode", value);

    public static void SetIsVirtualizing(DependencyObject element, bool value) => element.SetNamedValue("VirtualizingPanel.IsVirtualizing", value);

    public static void SetScrollUnit(DependencyObject element, ScrollUnit value) => element.SetNamedValue("VirtualizingPanel.ScrollUnit", value);

    public static void SetIsContainerVirtualizable(DependencyObject element, bool value)
    {
    }
}
