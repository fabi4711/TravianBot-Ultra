using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace System.Windows.Headless;

/// <summary>
/// A small binding engine: enough of WPF's semantics (DataContext inheritance, Path, ElementName,
/// RelativeSource, converters, StringFormat, default two-way properties) that controls mirror their view
/// models and code-behind that reads control values sees what WPF would show.
/// </summary>
public static class HeadlessBindings
{
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<string, BindingState>> Bound = new();
    private static readonly HashSet<string> TwoWayByDefault = new(StringComparer.Ordinal)
    {
        "Text", "IsChecked", "SelectedItem", "SelectedValue", "SelectedIndex", "Value", "IsSelected", "IsExpanded",
        "IsOpen", "Password", "SelectedDate",
    };

    [ThreadStatic]
    private static int _pushDepth;

    public static void SetBinding(DependencyObject target, string property, BindingBase binding)
    {
        var states = Bound.GetValue(target, static _ => new Dictionary<string, BindingState>(StringComparer.Ordinal));
        if (states.TryGetValue(property, out var old))
        {
            old.Detach();
        }

        var state = new BindingState(target, property, binding);
        states[property] = state;
        state.Apply();
    }

    public static void ClearBinding(DependencyObject target, string property)
    {
        if (Bound.TryGetValue(target, out var states) && states.Remove(property, out var state))
        {
            state.Detach();
        }
    }

    public static IReadOnlyDictionary<string, BindingBase> GetBindings(DependencyObject target)
        => Bound.TryGetValue(target, out var states)
            ? states.ToDictionary(pair => pair.Key, pair => pair.Value.Binding, StringComparer.Ordinal)
            : new Dictionary<string, BindingBase>();

    public static void Apply(DependencyObject target, string property, BindingBase binding)
    {
        if (Bound.TryGetValue(target, out var states) && states.TryGetValue(property, out var state) && ReferenceEquals(state.Binding, binding))
        {
            state.Apply();
            return;
        }

        SetBinding(target, property, binding);
    }

    public static void PushToSource(DependencyObject target, string property, BindingBase binding)
    {
        if (Bound.TryGetValue(target, out var states) && states.TryGetValue(property, out var state))
        {
            state.PushToSource();
        }
    }

    /// <summary>Re-evaluates every binding under <paramref name="root"/>, e.g. after its DataContext changed.</summary>
    public static void ReapplySubtree(DependencyObject root)
    {
        foreach (var element in HeadlessXaml.Descendants(root))
        {
            if (Bound.TryGetValue(element, out var states))
            {
                foreach (var state in states.Values.ToList())
                {
                    state.Apply();
                }
            }
        }
    }

    internal static void OnTargetPropertyChanged(DependencyObject target, string property)
    {
        if (_pushDepth > 0 || !Bound.TryGetValue(target, out var states) || !states.TryGetValue(property, out var state))
        {
            return;
        }

        if (state.IsTwoWay && !state.IsApplying)
        {
            state.PushToSource();
        }
    }

    /// <summary>Evaluates <paramref name="binding"/> against an arbitrary data item (used by renderers for cells).</summary>
    public static object? Evaluate(BindingBase binding, object? dataItem, DependencyObject? context = null)
    {
        switch (binding)
        {
            case Binding single:
                var source = single.Source ?? dataItem;
                var raw = ResolvePath(source, single.Path?.Path, out _);
                return Format(single, ConvertValue(single, raw, typeof(object)));
            case MultiBinding multi:
                var values = multi.Bindings.Select(child => Evaluate(child, dataItem, context)).ToArray();
                var converted = multi.Converter is null
                    ? values.FirstOrDefault()
                    : multi.Converter.Convert(values, typeof(object), multi.ConverterParameter, CultureInfo.CurrentCulture);
                return Format(multi, converted);
            default:
                return null;
        }
    }

    internal static object? ResolvePath(object? source, string? path, out (object Owner, PropertyInfo Property)? leaf)
    {
        leaf = null;
        if (string.IsNullOrWhiteSpace(path) || path == ".")
        {
            return source;
        }

        var current = source;
        var segments = SplitPath(path);
        for (var i = 0; i < segments.Count; i++)
        {
            if (current is null)
            {
                return null;
            }

            var segment = segments[i];
            if (segment.StartsWith('[') && segment.EndsWith(']'))
            {
                var indexText = segment[1..^1];
                current = current switch
                {
                    IList list when int.TryParse(indexText, out var index) => index >= 0 && index < list.Count ? list[index] : null,
                    IDictionary dictionary => dictionary.Contains(indexText) ? dictionary[indexText] : null,
                    _ => null,
                };
                continue;
            }

            if (segment.StartsWith('(') && segment.EndsWith(')'))
            {
                segment = HeadlessXaml.StripPrefix(segment[1..^1]);
                segment = segment[(segment.LastIndexOf('.') + 1)..];
            }

            var property = HeadlessXaml.FindProperty(current.GetType(), segment)
                           ?? current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return current is DependencyObject dependencyObject ? dependencyObject.GetNamedValue(segment) : null;
            }

            if (i == segments.Count - 1)
            {
                leaf = (current, property);
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private static List<string> SplitPath(string path)
    {
        var segments = new List<string>();
        var builder = new StringBuilder();
        var depth = 0;
        foreach (var ch in path)
        {
            if (ch is '(' or '[')
            {
                if (ch == '[' && depth == 0 && builder.Length > 0)
                {
                    segments.Add(builder.ToString());
                    builder.Clear();
                }

                depth++;
            }
            else if (ch is ')' or ']')
            {
                depth--;
            }

            if (ch == '.' && depth == 0)
            {
                if (builder.Length > 0)
                {
                    segments.Add(builder.ToString());
                }

                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0)
        {
            segments.Add(builder.ToString());
        }

        return segments;
    }

    private static object? ConvertValue(Binding binding, object? value, Type targetType)
        => binding.Converter is null
            ? value
            : binding.Converter.Convert(value, targetType, binding.ConverterParameter, binding.ConverterCulture ?? CultureInfo.CurrentCulture);

    private static object? Format(BindingBase binding, object? value)
    {
        if (value is null && binding.TargetNullValue is not null)
        {
            return binding.TargetNullValue;
        }

        if (string.IsNullOrEmpty(binding.StringFormat) || value is null)
        {
            return value;
        }

        var format = binding.StringFormat!;
        if (!format.Contains('{', StringComparison.Ordinal))
        {
            format = "{0:" + format + "}";
        }

        try
        {
            return value is object[] values
                ? string.Format(CultureInfo.CurrentCulture, format, values)
                : string.Format(CultureInfo.CurrentCulture, format, value);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    private sealed class BindingState
    {
        private readonly WeakReference<DependencyObject> _target;
        private readonly string _property;
        private readonly List<(INotifyPropertyChanged Source, PropertyChangedEventHandler Handler)> _subscriptions = new();
        private (object Owner, PropertyInfo Property)? _leaf;

        public BindingState(DependencyObject target, string property, BindingBase binding)
        {
            _target = new WeakReference<DependencyObject>(target);
            _property = property;
            Binding = binding;
            var mode = (binding as Binding)?.Mode ?? (binding as MultiBinding)?.Mode ?? BindingMode.Default;
            IsTwoWay = mode == BindingMode.TwoWay || mode == BindingMode.OneWayToSource ||
                       (mode == BindingMode.Default && TwoWayByDefault.Contains(property));
        }

        public BindingBase Binding { get; }
        public bool IsTwoWay { get; }
        public bool IsApplying { get; private set; }

        public void Detach()
        {
            foreach (var (source, handler) in _subscriptions)
            {
                source.PropertyChanged -= handler;
            }

            _subscriptions.Clear();
        }

        public void Apply()
        {
            if (!_target.TryGetTarget(out var target))
            {
                Detach();
                return;
            }

            if (IsApplying)
            {
                return;
            }

            Detach();
            object? value;
            var resolved = true;
            switch (Binding)
            {
                case Binding single:
                    var source = ResolveSource(target, single, out resolved);
                    value = resolved ? ResolveAndSubscribe(source, single.Path?.Path) : null;
                    if (resolved)
                    {
                        value = Format(single, ConvertValue(single, value, TargetType(target)));
                    }

                    break;
                case MultiBinding multi:
                    var values = new object?[multi.Bindings.Count];
                    for (var i = 0; i < multi.Bindings.Count; i++)
                    {
                        if (multi.Bindings[i] is Binding child)
                        {
                            var childSource = ResolveSource(target, child, out var childResolved);
                            values[i] = childResolved ? ConvertValue(child, ResolveAndSubscribe(childSource, child.Path?.Path, trackLeaf: false), typeof(object)) : DependencyProperty.UnsetValue;
                        }
                    }

                    value = multi.Converter is null
                        ? values.FirstOrDefault()
                        : multi.Converter.Convert(values, TargetType(target), multi.ConverterParameter, CultureInfo.CurrentCulture);
                    value = Format(multi, value);
                    break;
                default:
                    return;
            }

            if (!resolved)
            {
                if (Binding.FallbackValue is not null)
                {
                    SetTarget(target, Binding.FallbackValue);
                }

                return;
            }

            if (ReferenceEquals(value, DependencyProperty.UnsetValue))
            {
                value = Binding.FallbackValue;
            }

            SetTarget(target, value);
        }

        public void PushToSource()
        {
            if (Binding is not Binding single || !_target.TryGetTarget(out var target) || _leaf is not { } leaf || !leaf.Property.CanWrite)
            {
                return;
            }

            var value = HeadlessXaml.TryGetProperty(target, _property);
            if (single.Converter is not null)
            {
                value = single.Converter.ConvertBack(value, leaf.Property.PropertyType, single.ConverterParameter, CultureInfo.CurrentCulture);
            }

            try
            {
                _pushDepth++;
                leaf.Property.SetValue(leaf.Owner, HeadlessXaml.ConvertTo(value, leaf.Property.PropertyType));
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException or OverflowException or TargetInvocationException)
            {
                // WPF swallows conversion errors as validation errors; the source keeps its value.
            }
            finally
            {
                _pushDepth--;
            }
        }

        private void SetTarget(DependencyObject target, object? value)
        {
            if (Equals(HeadlessXaml.TryGetProperty(target, _property), value))
            {
                return;
            }

            IsApplying = true;
            try
            {
                HeadlessXaml.TrySetProperty(target, _property, value);
            }
            finally
            {
                IsApplying = false;
            }
        }

        private Type TargetType(DependencyObject target)
            => HeadlessXaml.FindProperty(target.GetType(), _property)?.PropertyType ?? typeof(object);

        private object? ResolveAndSubscribe(object? source, string? path, bool trackLeaf = true)
        {
            if (string.IsNullOrWhiteSpace(path) || path == ".")
            {
                return source;
            }

            // Subscribe to every object along the path so a change anywhere re-evaluates the binding.
            var current = source;
            var segments = SplitPath(path);
            for (var i = 0; i < segments.Count && current is not null; i++)
            {
                if (current is INotifyPropertyChanged observable)
                {
                    var segment = segments[i];
                    PropertyChangedEventHandler handler = (_, e) =>
                    {
                        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == segment ||
                            segment.StartsWith('[') || (segment.StartsWith('(') && segment.EndsWith(e.PropertyName + ")", StringComparison.Ordinal)))
                        {
                            Dispatch(Apply);
                        }
                    };
                    observable.PropertyChanged += handler;
                    _subscriptions.Add((observable, handler));
                }

                if (current is System.Collections.Specialized.INotifyCollectionChanged collection && segments[i].StartsWith('['))
                {
                    collection.CollectionChanged += (_, _) => Dispatch(Apply);
                }

                current = ResolvePath(current, segments[i], out var leaf);
                if (trackLeaf && i == segments.Count - 1)
                {
                    _leaf = leaf;
                }
            }

            return current;
        }

        private static void Dispatch(Action action)
        {
            var dispatcher = Threading.Dispatcher.Main;
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.InvokeAsync(action);
            }
        }

        private object? ResolveSource(DependencyObject target, Binding binding, out bool resolved)
        {
            resolved = true;
            if (binding.Source is not null)
            {
                return binding.Source;
            }

            if (!string.IsNullOrEmpty(binding.ElementName))
            {
                var element = HeadlessXaml.FindName(target, binding.ElementName!);
                resolved = element is not null;
                return element;
            }

            if (binding.RelativeSource is { } relative)
            {
                switch (relative.Mode)
                {
                    case RelativeSourceMode.Self:
                        return target;
                    case RelativeSourceMode.FindAncestor:
                        var level = 0;
                        for (var current = HeadlessXaml.GetParent(target); current is not null; current = HeadlessXaml.GetParent(current))
                        {
                            if (relative.AncestorType?.IsInstanceOfType(current) == true && ++level >= Math.Max(1, relative.AncestorLevel))
                            {
                                return current;
                            }
                        }

                        resolved = false;
                        return null;
                    default:
                        resolved = false;
                        return null;
                }
            }

            // A binding on DataContext itself reads the parent's context, as in WPF; objects outside the
            // element tree (resources, columns) use their inheritance context.
            var context = target is FrameworkElement framework && _property != nameof(FrameworkElement.DataContext)
                ? framework.DataContext
                : HeadlessXaml.InheritedDataContext(HeadlessXaml.GetParent(target));
            resolved = context is not null;
            return context;
        }
    }
}
