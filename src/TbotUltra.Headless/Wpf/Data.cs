using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Headless;

namespace System.ComponentModel
{
    public interface ICollectionView : IEnumerable, INotifyCollectionChanged
    {
        CultureInfo? Culture { get; set; }
        IEnumerable SourceCollection { get; }
        Predicate<object>? Filter { get; set; }
        bool CanFilter { get; }
        SortDescriptionCollection SortDescriptions { get; }
        bool CanSort { get; }
        bool CanGroup { get; }
        ObservableCollection<GroupDescription> GroupDescriptions { get; }
        ReadOnlyObservableCollection<object>? Groups { get; }
        bool IsEmpty { get; }
        object? CurrentItem { get; }
        int CurrentPosition { get; }
        bool IsCurrentAfterLast { get; }
        bool IsCurrentBeforeFirst { get; }

        event CurrentChangingEventHandler? CurrentChanging;

        event EventHandler? CurrentChanged;

        bool Contains(object? item);

        void Refresh();

        IDisposable DeferRefresh();

        bool MoveCurrentToFirst();

        bool MoveCurrentToLast();

        bool MoveCurrentToNext();

        bool MoveCurrentToPrevious();

        bool MoveCurrentTo(object? item);

        bool MoveCurrentToPosition(int position);
    }

    public interface ICollectionViewLiveShaping
    {
        bool CanChangeLiveSorting { get; }
        bool? IsLiveSorting { get; set; }
        ObservableCollection<string> LiveSortingProperties { get; }
    }

    public delegate void CurrentChangingEventHandler(object sender, CurrentChangingEventArgs e);

    public class CurrentChangingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public bool IsCancelable => true;
    }

    public readonly struct SortDescription
    {
        public SortDescription(string propertyName, ListSortDirection direction)
        {
            PropertyName = propertyName;
            Direction = direction;
        }

        public string PropertyName { get; }
        public ListSortDirection Direction { get; }
    }

    public sealed class SortDescriptionCollection : Collection<SortDescription>, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        protected override void InsertItem(int index, SortDescription item)
        {
            base.InsertItem(index, item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public abstract class GroupDescription
    {
    }
}

namespace System.Windows.Data
{
    using System.ComponentModel;

    public enum BindingMode
    {
        TwoWay,
        OneWay,
        OneTime,
        OneWayToSource,
        Default,
    }

    public enum UpdateSourceTrigger
    {
        Default,
        PropertyChanged,
        LostFocus,
        Explicit,
    }

    public enum RelativeSourceMode
    {
        PreviousData,
        TemplatedParent,
        Self,
        FindAncestor,
    }

    public interface IValueConverter
    {
        object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);

        object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
    }

    public interface IMultiValueConverter
    {
        object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture);

        object?[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ValueConversionAttribute : Attribute
    {
        public ValueConversionAttribute(Type sourceType, Type targetType)
        {
        }
    }

    public sealed class RelativeSource
    {
        public RelativeSource()
        {
        }

        public RelativeSource(RelativeSourceMode mode)
        {
            Mode = mode;
        }

        public RelativeSource(RelativeSourceMode mode, Type ancestorType, int ancestorLevel)
        {
            Mode = mode;
            AncestorType = ancestorType;
            AncestorLevel = ancestorLevel;
        }

        public RelativeSourceMode Mode { get; set; }
        public Type? AncestorType { get; set; }
        public int AncestorLevel { get; set; } = 1;
        public static RelativeSource Self => new(RelativeSourceMode.Self);
        public static RelativeSource TemplatedParent => new(RelativeSourceMode.TemplatedParent);
    }

    public abstract class BindingBase
    {
        public string? StringFormat { get; set; }
        public object? FallbackValue { get; set; }
        public object? TargetNullValue { get; set; }
        public string? BindingGroupName { get; set; }
        public int Delay { get; set; }
    }

    public class Binding : BindingBase
    {
        public Binding()
        {
        }

        public Binding(string path)
        {
            Path = new PropertyPath(path);
        }

        public PropertyPath? Path { get; set; }

        /// <summary>Resource key of a <c>Source={StaticResource …}</c> resolved when the binding applies.</summary>
        public object? HeadlessSourceKey { get; set; }

        /// <summary>Resource key of a <c>Converter={StaticResource …}</c> resolved when the binding applies.</summary>
        public object? HeadlessConverterKey { get; set; }

        public BindingMode Mode { get; set; } = BindingMode.Default;
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
        public IValueConverter? Converter { get; set; }
        public object? ConverterParameter { get; set; }
        public CultureInfo? ConverterCulture { get; set; }
        public object? Source { get; set; }
        public string? ElementName { get; set; }
        public RelativeSource? RelativeSource { get; set; }
        public bool NotifyOnSourceUpdated { get; set; }
        public bool NotifyOnTargetUpdated { get; set; }
        public bool NotifyOnValidationError { get; set; }
        public bool ValidatesOnDataErrors { get; set; }
        public bool ValidatesOnExceptions { get; set; }
        public bool IsAsync { get; set; }
        public string? XPath { get; set; }
    }

    public class MultiBinding : BindingBase
    {
        public Collection<BindingBase> Bindings { get; } = new();
        public IMultiValueConverter? Converter { get; set; }
        public object? ConverterParameter { get; set; }
        public BindingMode Mode { get; set; } = BindingMode.Default;
    }

    public abstract class BindingExpressionBase
    {
        public virtual void UpdateTarget()
        {
        }

        public virtual void UpdateSource()
        {
        }

        public bool HasError => false;
    }

    public sealed class BindingExpression : BindingExpressionBase
    {
        private readonly DependencyObject _target;
        private readonly DependencyProperty _property;

        internal BindingExpression(DependencyObject target, DependencyProperty property, BindingBase binding)
        {
            _target = target;
            _property = property;
            ParentBindingBase = binding;
        }

        public BindingBase ParentBindingBase { get; }
        public Binding? ParentBinding => ParentBindingBase as Binding;
        public object? DataItem => (_target as FrameworkElement)?.DataContext;
        public object? ResolvedSource => DataItem;

        public override void UpdateTarget() => HeadlessBindings.Apply(_target, _property.Name, ParentBindingBase);

        public override void UpdateSource() => HeadlessBindings.PushToSource(_target, _property.Name, ParentBindingBase);
    }

    public static class BindingOperations
    {
        public static BindingExpression? GetBindingExpression(DependencyObject target, DependencyProperty dp)
            => (target as FrameworkElement)?.GetBindingExpression(dp);

        public static BindingExpressionBase SetBinding(DependencyObject target, DependencyProperty dp, BindingBase binding)
            => target is FrameworkElement element
                ? element.SetBinding(dp, binding)
                : throw new NotSupportedException("Bindings are only supported on framework elements in the headless host.");

        public static void ClearBinding(DependencyObject target, DependencyProperty dp)
        {
        }

        public static void EnableCollectionSynchronization(IEnumerable collection, object lockObject)
        {
        }
    }

    public delegate void FilterEventHandler(object sender, FilterEventArgs e);

    public class FilterEventArgs : EventArgs
    {
        public FilterEventArgs(object item)
        {
            Item = item;
        }

        public object Item { get; }
        public bool Accepted { get; set; } = true;
    }

    /// <summary>
    /// Tracks one default view per source collection, as WPF does, so a filter or sort set through
    /// <see cref="GetDefaultView"/> applies wherever that collection is shown.
    /// </summary>
    public class CollectionViewSource : DependencyObject
    {
        private static readonly ConditionalWeakTable<object, ListCollectionView> Views = new();
        private object? _source;
        private ListCollectionView? _view;

        public object? Source
        {
            get => _source;
            set
            {
                _source = value;
                _view = value is IEnumerable enumerable ? new ListCollectionView(enumerable, this) : null;
                RaisePropertyChanged(nameof(Source));
                RaisePropertyChanged(nameof(View));
            }
        }

        public ICollectionView? View => _view;

        public SortDescriptionCollection SortDescriptions { get; } = new();

        public ObservableCollection<GroupDescription> GroupDescriptions { get; } = new();

        public bool IsLiveSortingRequested { get; set; }

        public event FilterEventHandler? Filter;

        internal bool HasFilter => Filter is not null;

        internal bool Accepts(object item)
        {
            if (Filter is null)
            {
                return true;
            }

            var args = new FilterEventArgs(item);
            Filter(this, args);
            return args.Accepted;
        }

        public static ICollectionView GetDefaultView(object source)
        {
            if (source is ICollectionView view)
            {
                return view;
            }

            if (source is CollectionViewSource collectionViewSource)
            {
                return collectionViewSource.View!;
            }

            return Views.GetValue(source, static key => new ListCollectionView((IEnumerable)key, null));
        }

        internal static ListCollectionView? TryGetExistingView(object source)
            => Views.TryGetValue(source, out var view) ? view : null;

        public void DeferRefresh()
        {
        }
    }

    public class CollectionView : ICollectionView
    {
        private readonly IEnumerable _source;
        private Predicate<object>? _filter;
        private readonly CollectionViewSource? _owner;
        private int _currentPosition = -1;

        public CollectionView(IEnumerable collection)
            : this(collection, null)
        {
        }

        internal CollectionView(IEnumerable collection, CollectionViewSource? owner)
        {
            _source = collection;
            _owner = owner;
            if (collection is INotifyCollectionChanged observable)
            {
                observable.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
            }

            SortDescriptions.CollectionChanged += (_, _) => Refresh();
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public event CurrentChangingEventHandler? CurrentChanging;

        public event EventHandler? CurrentChanged;

        public CultureInfo? Culture { get; set; }

        public IEnumerable SourceCollection => _source;

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

        public SortDescriptionCollection SortDescriptions => _owner?.SortDescriptions ?? _ownSorts;

        private readonly SortDescriptionCollection _ownSorts = new();

        public bool CanSort => true;

        public bool CanGroup => false;

        public ObservableCollection<GroupDescription> GroupDescriptions { get; } = new();

        public ReadOnlyObservableCollection<object>? Groups => null;

        public bool IsEmpty => !Items().Any();

        public int Count => Items().Count();

        public object? CurrentItem => _currentPosition >= 0 ? Items().ElementAtOrDefault(_currentPosition) : null;

        public int CurrentPosition => _currentPosition;

        public bool IsCurrentAfterLast => _currentPosition >= Count;

        public bool IsCurrentBeforeFirst => _currentPosition < 0;

        public bool CanChangeLiveSorting => false;

        public bool? IsLiveSorting { get; set; }

        public ObservableCollection<string> LiveSortingProperties { get; } = new();

        public bool Contains(object? item) => Items().Contains(item);

        public void Refresh() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        public IDisposable DeferRefresh() => new HeadlessCollections.DeferredRefresh(Refresh);

        public bool MoveCurrentToFirst() => MoveCurrentToPosition(0);

        public bool MoveCurrentToLast() => MoveCurrentToPosition(Count - 1);

        public bool MoveCurrentToNext() => MoveCurrentToPosition(_currentPosition + 1);

        public bool MoveCurrentToPrevious() => MoveCurrentToPosition(_currentPosition - 1);

        public bool MoveCurrentTo(object? item) => MoveCurrentToPosition(Items().ToList().IndexOf(item));

        public bool MoveCurrentToPosition(int position)
        {
            CurrentChanging?.Invoke(this, new CurrentChangingEventArgs());
            _currentPosition = position;
            CurrentChanged?.Invoke(this, EventArgs.Empty);
            return CurrentItem is not null;
        }

        public IEnumerator GetEnumerator() => Items().GetEnumerator();

        internal IEnumerable<object?> Items()
        {
            var items = _source.Cast<object?>();
            if (_owner is { HasFilter: true })
            {
                items = items.Where(item => item is not null && _owner.Accepts(item));
            }

            if (_filter is not null)
            {
                items = items.Where(item => item is not null && _filter(item));
            }

            return SortDescriptions.Count > 0 ? HeadlessCollections.Sort(items, SortDescriptions) : items;
        }
    }

    public class ListCollectionView : CollectionView, ICollectionViewLiveShaping
    {
        public ListCollectionView(IList list)
            : base(list)
        {
        }

        internal ListCollectionView(IEnumerable collection, CollectionViewSource? owner)
            : base(collection, owner)
        {
        }

        public IComparer? CustomSort { get; set; }
    }

    public sealed class PropertyGroupDescription : GroupDescription
    {
        public PropertyGroupDescription()
        {
        }

        public PropertyGroupDescription(string propertyName)
        {
            PropertyName = propertyName;
        }

        public string? PropertyName { get; set; }
    }
}
