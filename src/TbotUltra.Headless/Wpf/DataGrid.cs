using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace System.Windows.Controls;

public enum DataGridSelectionMode
{
    Single,
    Extended,
}

public enum DataGridSelectionUnit
{
    Cell,
    FullRow,
    CellOrRowHeader,
}

public enum DataGridHeadersVisibility
{
    None = 0,
    Column = 1,
    Row = 2,
    All = 3,
}

public enum DataGridGridLinesVisibility
{
    None,
    Horizontal,
    Vertical,
    All,
}

public enum DataGridLengthUnitType
{
    Auto,
    Pixel,
    SizeToCells,
    SizeToHeader,
    Star,
}

public enum DataGridEditingUnit
{
    Cell,
    Row,
}

public enum DataGridEditAction
{
    Cancel,
    Commit,
}

public readonly struct DataGridLength
{
    public DataGridLength(double pixels)
        : this(pixels, DataGridLengthUnitType.Pixel)
    {
    }

    public DataGridLength(double value, DataGridLengthUnitType type)
    {
        Value = value;
        UnitType = type;
    }

    public double Value { get; }
    public DataGridLengthUnitType UnitType { get; }
    public bool IsStar => UnitType == DataGridLengthUnitType.Star;
    public bool IsAuto => UnitType == DataGridLengthUnitType.Auto;
    public bool IsAbsolute => UnitType == DataGridLengthUnitType.Pixel;
    public double DisplayValue => Value;
    public static DataGridLength Auto => new(1, DataGridLengthUnitType.Auto);
    public static DataGridLength SizeToCells => new(1, DataGridLengthUnitType.SizeToCells);
    public static DataGridLength SizeToHeader => new(1, DataGridLengthUnitType.SizeToHeader);

    public static implicit operator DataGridLength(double value) => new(value);
}

public abstract class DataGridColumn : DependencyObject
{
    public object? Header { get => Get<object?>(null); set => Set(value); }
    public Style? HeaderStyle { get => Get<Style?>(null); set => Set(value); }
    public DataTemplate? HeaderTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
    public Style? CellStyle { get => Get<Style?>(null); set => Set(value); }
    public DataGridLength Width { get => Get(DataGridLength.Auto); set => Set(value); }
    public double MinWidth { get => Get(20d); set => Set(value); }
    public double MaxWidth { get => Get(double.PositiveInfinity); set => Set(value); }
    public double ActualWidth => Width.Value;
    public bool IsReadOnly { get => Get(false); set => Set(value); }
    public bool CanUserSort { get => Get(true); set => Set(value); }
    public bool CanUserResize { get => Get(true); set => Set(value); }
    public bool CanUserReorder { get => Get(true); set => Set(value); }
    public string? SortMemberPath { get => Get<string?>(null); set => Set(value); }
    public ComponentModel.ListSortDirection? SortDirection { get => Get<ComponentModel.ListSortDirection?>(null); set => Set(value); }
    public Visibility Visibility { get => Get(Visibility.Visible); set => Set(value); }
    public int DisplayIndex { get => Get(-1); set => Set(value); }
    public DataGrid? DataGridOwner { get; internal set; }
}

public abstract class DataGridBoundColumn : DataGridColumn
{
    public BindingBase? Binding { get => Get<BindingBase?>(null); set => Set(value); }
    public Style? ElementStyle { get => Get<Style?>(null); set => Set(value); }
    public Style? EditingElementStyle { get => Get<Style?>(null); set => Set(value); }
}

public class DataGridTextColumn : DataGridBoundColumn
{
    public Brush? Foreground { get => Get<Brush?>(null); set => Set(value); }
    public FontWeight FontWeight { get => Get(FontWeight.Normal); set => Set(value); }
    public double FontSize { get => Get(12d); set => Set(value); }
}

public class DataGridCheckBoxColumn : DataGridBoundColumn
{
    public bool IsThreeState { get => Get(false); set => Set(value); }
}

public class DataGridComboBoxColumn : DataGridColumn
{
    public BindingBase? SelectedItemBinding { get => Get<BindingBase?>(null); set => Set(value); }
    public BindingBase? SelectedValueBinding { get => Get<BindingBase?>(null); set => Set(value); }
    public IEnumerable? ItemsSource { get => Get<IEnumerable?>(null); set => Set(value); }
    public string DisplayMemberPath { get => Get(string.Empty); set => Set(value); }
}

public class DataGridTemplateColumn : DataGridColumn
{
    public DataTemplate? CellTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
    public DataTemplate? CellEditingTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
    public DataTemplateSelector? CellTemplateSelector { get => Get<DataTemplateSelector?>(null); set => Set(value); }
}

public sealed class DataGridColumnCollection : ObservableCollection<DataGridColumn>
{
    private readonly DataGrid _owner;

    internal DataGridColumnCollection(DataGrid owner)
    {
        _owner = owner;
    }

    protected override void InsertItem(int index, DataGridColumn item)
    {
        item.DataGridOwner = _owner;
        base.InsertItem(index, item);
        for (var i = 0; i < Count; i++)
        {
            this[i].DisplayIndex = i;
        }
    }
}

public class DataGridRow : Control
{
    public object? Item { get; internal set; }
    public bool IsSelected { get => Get(false); set => Set(value); }

    public int GetIndex() => (HeadlessParent as DataGrid)?.Items.IndexOf(Item) ?? -1;
}

public class DataGridCell : ContentControl
{
    public DataGridColumn? Column { get; internal set; }
    public bool IsSelected { get => Get(false); set => Set(value); }
    public bool IsEditing { get => Get(false); set => Set(value); }
    public bool IsReadOnly => Column?.IsReadOnly ?? false;
}

public class DataGridCellInfo
{
    public DataGridCellInfo(object item, DataGridColumn column)
    {
        Item = item;
        Column = column;
    }

    public object Item { get; }
    public DataGridColumn Column { get; }
}

public delegate void DataGridRowEventHandler(object sender, DataGridRowEventArgs e);

public class DataGridRowEventArgs : EventArgs
{
    public DataGridRowEventArgs(DataGridRow row)
    {
        Row = row;
    }

    public DataGridRow Row { get; }
}

public class DataGridCellEditEndingEventArgs : EventArgs
{
    public DataGridCellEditEndingEventArgs(DataGridColumn column, DataGridRow row, FrameworkElement editingElement, DataGridEditAction editAction)
    {
        Column = column;
        Row = row;
        EditingElement = editingElement;
        EditAction = editAction;
    }

    public DataGridColumn Column { get; }
    public DataGridRow Row { get; }
    public FrameworkElement EditingElement { get; }
    public DataGridEditAction EditAction { get; }
    public bool Cancel { get; set; }
}

public class DataGridRowEditEndingEventArgs : EventArgs
{
    public DataGridRowEditEndingEventArgs(DataGridRow row, DataGridEditAction editAction)
    {
        Row = row;
        EditAction = editAction;
    }

    public DataGridRow Row { get; }
    public DataGridEditAction EditAction { get; }
    public bool Cancel { get; set; }
}

public class DataGridBeginningEditEventArgs : EventArgs
{
    public DataGridBeginningEditEventArgs(DataGridColumn column, DataGridRow row, RoutedEventArgs editingEventArgs)
    {
        Column = column;
        Row = row;
        EditingEventArgs = editingEventArgs;
    }

    public DataGridColumn Column { get; }
    public DataGridRow Row { get; }
    public RoutedEventArgs EditingEventArgs { get; }
    public bool Cancel { get; set; }
}

public class DataGridSortingEventArgs : EventArgs
{
    public DataGridSortingEventArgs(DataGridColumn column)
    {
        Column = column;
    }

    public DataGridColumn Column { get; }
    public bool Handled { get; set; }
}

public delegate void DataGridSortingEventHandler(object sender, DataGridSortingEventArgs e);

public class DataGrid : MultiSelector
{
    private readonly ObservableCollection<object?> _selectedItems = new();

    public DataGrid()
    {
        Columns = new DataGridColumnCollection(this);
    }

    public ObservableCollection<DataGridColumn> Columns { get; }
    public IList SelectedItems => _selectedItems;
    public IList<DataGridCellInfo> SelectedCells { get; } = new List<DataGridCellInfo>();
    public DataGridCellInfo? CurrentCell { get; set; }
    public object? CurrentItem { get => Get<object?>(null); set => Set(value); }
    public DataGridColumn? CurrentColumn { get => Get<DataGridColumn?>(null); set => Set(value); }
    public bool AutoGenerateColumns { get => Get(true); set => Set(value); }
    public bool IsReadOnly { get => Get(false); set => Set(value); }
    public bool CanUserAddRows { get => Get(true); set => Set(value); }
    public bool CanUserDeleteRows { get => Get(true); set => Set(value); }
    public bool CanUserSortColumns { get => Get(true); set => Set(value); }
    public bool CanUserResizeColumns { get => Get(true); set => Set(value); }
    public bool CanUserReorderColumns { get => Get(true); set => Set(value); }
    public bool CanUserResizeRows { get => Get(true); set => Set(value); }
    public DataGridSelectionMode SelectionMode { get => Get(DataGridSelectionMode.Extended); set => Set(value); }
    public DataGridSelectionUnit SelectionUnit { get => Get(DataGridSelectionUnit.FullRow); set => Set(value); }
    public DataGridHeadersVisibility HeadersVisibility { get => Get(DataGridHeadersVisibility.All); set => Set(value); }
    public DataGridGridLinesVisibility GridLinesVisibility { get => Get(DataGridGridLinesVisibility.All); set => Set(value); }
    public double RowHeight { get => Get(double.NaN); set => Set(value); }
    public double ColumnHeaderHeight { get => Get(double.NaN); set => Set(value); }
    public double RowHeaderWidth { get => Get(double.NaN); set => Set(value); }
    public Style? RowStyle { get => Get<Style?>(null); set => Set(value); }
    public Style? CellStyle { get => Get<Style?>(null); set => Set(value); }
    public Style? ColumnHeaderStyle { get => Get<Style?>(null); set => Set(value); }
    public DataTemplate? RowDetailsTemplate { get => Get<DataTemplate?>(null); set => Set(value); }
    public Brush? RowBackground { get => Get<Brush?>(null); set => Set(value); }
    public Brush? AlternatingRowBackground { get => Get<Brush?>(null); set => Set(value); }
    public Brush? HorizontalGridLinesBrush { get => Get<Brush?>(null); set => Set(value); }
    public Brush? VerticalGridLinesBrush { get => Get<Brush?>(null); set => Set(value); }
    public bool EnableRowVirtualization { get => Get(true); set => Set(value); }
    public bool EnableColumnVirtualization { get => Get(false); set => Set(value); }
    public ScrollBarVisibility HorizontalScrollBarVisibility { get => Get(ScrollBarVisibility.Auto); set => Set(value); }
    public ScrollBarVisibility VerticalScrollBarVisibility { get => Get(ScrollBarVisibility.Auto); set => Set(value); }
    public double FrozenColumnCount { get => Get(0d); set => Set(value); }

    public event EventHandler<DataGridRowEventArgs>? LoadingRow { add => AddNamed(nameof(LoadingRow), value); remove => RemoveNamed(nameof(LoadingRow), value); }
    public event EventHandler<DataGridCellEditEndingEventArgs>? CellEditEnding { add => AddNamed(nameof(CellEditEnding), value); remove => RemoveNamed(nameof(CellEditEnding), value); }
    public event EventHandler<DataGridRowEditEndingEventArgs>? RowEditEnding { add => AddNamed(nameof(RowEditEnding), value); remove => RemoveNamed(nameof(RowEditEnding), value); }
    public event EventHandler<DataGridBeginningEditEventArgs>? BeginningEdit { add => AddNamed(nameof(BeginningEdit), value); remove => RemoveNamed(nameof(BeginningEdit), value); }
    public event DataGridSortingEventHandler? Sorting { add => AddNamed(nameof(Sorting), value); remove => RemoveNamed(nameof(Sorting), value); }
    public event EventHandler? CurrentCellChanged { add => AddNamed(nameof(CurrentCellChanged), value); remove => RemoveNamed(nameof(CurrentCellChanged), value); }

    public bool CommitEdit() => true;

    public bool CommitEdit(DataGridEditingUnit editingUnit, bool exitEditingMode) => true;

    public bool CancelEdit() => true;

    public bool CancelEdit(DataGridEditingUnit editingUnit) => true;

    public bool BeginEdit() => true;

    public void ScrollIntoView(object item)
    {
    }

    public void ScrollIntoView(object item, DataGridColumn column)
    {
    }

    public void SelectAll()
    {
        _selectedItems.Clear();
        foreach (var item in Items)
        {
            _selectedItems.Add(item);
        }

        RaisePropertyChanged(nameof(SelectedItems));
    }

    public void UnselectAll()
    {
        _selectedItems.Clear();
        SelectedItem = null;
        RaisePropertyChanged(nameof(SelectedItems));
    }

    public void UnselectAllCells() => UnselectAll();

    /// <summary>Multi-selection from the web UI. The first item becomes <see cref="Selector.SelectedItem"/>.</summary>
    public void HeadlessSelectItems(IEnumerable<object?> items)
    {
        var list = items.Where(item => item is not null).ToList();
        _selectedItems.Clear();
        foreach (var item in list)
        {
            _selectedItems.Add(item);
        }

        HeadlessSelect(list.FirstOrDefault());
        RaisePropertyChanged(nameof(SelectedItems));
    }

    protected override void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (newItem is null)
        {
            _selectedItems.Clear();
        }
        else if (!_selectedItems.Contains(newItem))
        {
            _selectedItems.Clear();
            _selectedItems.Add(newItem);
        }

        CurrentItem = newItem;
    }

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(DataGrid));
}
