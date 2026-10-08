using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace System.Windows.Headless;

/// <summary>Per-component bookkeeping: the markup file, its named elements and every element it created.</summary>
public sealed class HeadlessComponent
{
    internal HeadlessComponent(object root, string fileName)
    {
        Root = root;
        FileName = fileName;
    }

    public object Root { get; }
    public string FileName { get; }
    public Dictionary<string, object> Named { get; } = new(StringComparer.Ordinal);
    public Dictionary<object, string> Ids { get; } = new(ReferenceEqualityComparer.Instance);
    internal List<(Style Style, string EventName, HeadlessHandler Handler)> StyleHandlers { get; } = new();
}

/// <summary>Markup and handlers of a template; templates are not expanded in the headless host.</summary>
public sealed class HeadlessTemplateInfo
{
    public string Markup { get; internal set; } = string.Empty;

    /// <summary>Handlers keyed by (descendant index in the template markup, event name).</summary>
    public List<(int ElementIndex, string EventName, HeadlessHandler Handler)> Handlers { get; } = new();
}

/// <summary>Runtime behind <c>XamlHeadlessGenerator</c>'s generated <c>InitializeComponent</c>.</summary>
public static class HeadlessXaml
{
    private static readonly ConditionalWeakTable<object, HeadlessComponent> Components = new();
    private static readonly ConditionalWeakTable<object, HeadlessComponent> OwnerComponent = new();
    private static readonly ConditionalWeakTable<object, HeadlessTemplateInfo> Templates = new();
    private static readonly ConditionalWeakTable<object, Dictionary<string, string>> PendingMarkup = new();
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> PropertyCache = new();
    private static readonly HashSet<string> BubblingEvents = new(StringComparer.Ordinal)
    {
        "Click", "Checked", "Unchecked", "Indeterminate", "SelectionChanged", "TextChanged", "MouseLeftButtonDown",
        "MouseLeftButtonUp", "PreviewMouseLeftButtonDown", "PreviewMouseLeftButtonUp", "MouseDoubleClick",
        "PreviewMouseMove", "MouseMove", "KeyDown", "PreviewKeyDown", "KeyUp", "Drop", "DragOver", "DragEnter",
        "DragLeave", "MouseDown", "MouseUp", "PreviewMouseDown", "PreviewMouseUp", "PreviewMouseWheel", "MouseWheel",
        "GotFocus", "LostFocus", "RequestNavigate", "ValueChanged", "ScrollChanged",
    };

    /// <summary>Any property change on any headless element. The web UI listens to re-render.</summary>
    public static event Action<object, string>? Changed;

    public static void BeginComponent(object root, string fileName)
    {
        var component = new HeadlessComponent(root, fileName);
        Components.AddOrUpdate(root, component);
        OwnerComponent.AddOrUpdate(root, component);
    }

    public static void EndComponent(object root)
    {
        if (!Components.TryGetValue(root, out var component))
        {
            return;
        }

        ResolvePendingMarkup(root);
        foreach (var element in component.Ids.Keys.ToList())
        {
            ResolvePendingMarkup(element);
        }

        ApplyStyleHandlers(component);
        if (root is DependencyObject rootObject)
        {
            HeadlessBindings.ReapplySubtree(rootObject);
        }
    }

    public static HeadlessComponent? GetComponent(object root) => Components.TryGetValue(root, out var component) ? component : null;

    public static HeadlessComponent? GetOwningComponent(object element)
    {
        for (object? current = element; current is not null; current = (current as UIElement)?.HeadlessParent)
        {
            if (OwnerComponent.TryGetValue(current, out var component))
            {
                return component;
            }
        }

        return null;
    }

    public static HeadlessTemplateInfo? GetTemplateInfo(object template) => Templates.TryGetValue(template, out var info) ? info : null;

    // ---- element construction -------------------------------------------------------------------

    public static void Attach(object root, object element, string id, object? parent, string? parentProperty, string? key)
    {
        if (Components.TryGetValue(root, out var component))
        {
            component.Ids[element] = id;
            OwnerComponent.AddOrUpdate(element, component);
            if (!id.Contains('#', StringComparison.Ordinal))
            {
                component.Named[id] = element;
            }
        }

        if (parent is null)
        {
            return;
        }

        if (key is not null || parentProperty == "Resources")
        {
            var resources = parent switch
            {
                FrameworkElement framework => framework.Resources,
                Application application => application.Resources,
                Style style => style.Resources,
                ResourceDictionary dictionary => dictionary,
                _ => null,
            };
            if (resources is not null)
            {
                resources[key ?? KeyFor(element)] = element;
                if (element is UIElement resourceElement && parent is UIElement parentElement)
                {
                    resourceElement.HeadlessParent = parentElement;
                }

                return;
            }
        }

        if (parentProperty is not null)
        {
            AddToProperty(parent, parentProperty, element);
            return;
        }

        AddContent(parent, element);
    }

    public static void AddValue(object root, object? parent, string? parentProperty, string? key, string typeName, string text)
    {
        object value = typeName switch
        {
            "Double" => double.Parse(text, CultureInfo.InvariantCulture),
            "Int32" => int.Parse(text, CultureInfo.InvariantCulture),
            "Boolean" => bool.Parse(text),
            "TimeSpan" => TimeSpan.Parse(text, CultureInfo.InvariantCulture),
            _ => text,
        };
        Attach(root, value, $"{typeName}#value", parent, parentProperty, key);
    }

    public static Style CreateStyle(object root, string? key, string targetType, string? basedOn)
    {
        var style = new Style { HeadlessKey = key, HeadlessTargetTypeName = StripPrefix(targetType) };
        if (basedOn is not null && MarkupParser.TryParse(basedOn, out var node) && node.Positional.FirstOrDefault() is string baseKey)
        {
            style.BasedOn = (root as FrameworkElement)?.TryFindResource(baseKey) as Style ?? HeadlessResources.Find(baseKey) as Style;
        }

        return style;
    }

    public static void AddStyleSetter(Style style, string property, string value)
        => style.HeadlessSetters[StripPrefix(property)] = value;

    public static void AddStyleHandler(object root, Style style, string eventName, HeadlessHandler handler)
    {
        if (Components.TryGetValue(root, out var component))
        {
            component.StyleHandlers.Add((style, eventName, handler));
        }
    }

    public static void SetTemplateMarkup(object template, string markup)
        => Templates.GetValue(template, static _ => new HeadlessTemplateInfo()).Markup = markup;

    public static void AddTemplateHandler(object template, int elementIndex, string eventName, HeadlessHandler handler)
        => Templates.GetValue(template, static _ => new HeadlessTemplateInfo()).Handlers.Add((elementIndex, eventName, handler));

    public static void AddHandler(object target, string eventName, HeadlessHandler handler)
    {
        var eventInfo = target.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (eventInfo?.EventHandlerType is { } handlerType)
        {
            eventInfo.AddEventHandler(target, Adapt(handlerType, handler));
            return;
        }

        if (target is UIElement element)
        {
            element.AddNamed(eventName, handler);
        }
    }

    public static T Args<T>(EventArgs args)
        where T : class
    {
        if (args is T typed)
        {
            return typed;
        }

        return (T)HeadlessEventArgs.Create(typeof(T), args);
    }

    public static void SetLiteral(object target, string property, string value)
    {
        if (property == "__text")
        {
            SetText(target, value);
            return;
        }

        if (property.Contains('.', StringComparison.Ordinal))
        {
            if (target is DependencyObject attachedTarget)
            {
                SetAttached(attachedTarget, StripPrefix(property), ConvertAttached(value));
            }

            return;
        }

        TrySetProperty(target, property, value);
    }

    public static void SetMarkup(object target, string property, string markup)
    {
        if (!MarkupParser.TryParse(markup, out var node))
        {
            SetLiteral(target, property, markup);
            return;
        }

        switch (node.Name)
        {
            case "x:Null":
                TrySetProperty(target, property, null);
                return;
            case "Binding":
            case "TemplateBinding":
            case "MultiBinding":
                var binding = MarkupParser.ToBinding(node, target);
                var propertyInfo = FindProperty(target.GetType(), property);
                if (propertyInfo is not null && typeof(BindingBase).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    propertyInfo.SetValue(target, binding);
                }
                else if (target is FrameworkElement element)
                {
                    element.SetHeadlessBinding(property, binding);
                }
                else if (target is DependencyObject dependencyObject)
                {
                    HeadlessBindings.SetBinding(dependencyObject, property, binding);
                }

                return;
            default:
                // StaticResource/DynamicResource/x:Static resolve once the component's resources exist.
                PendingMarkup.GetValue(target, static _ => new Dictionary<string, string>(StringComparer.Ordinal))[property] = markup;
                return;
        }
    }

    public static void AddContent(object parent, object child)
    {
        switch (parent)
        {
            case DataGrid dataGrid when child is DataGridColumn column:
                dataGrid.Columns.Add(column);
                break;
            case Window window when child is UIElement:
                window.Content = child;
                break;
            case ItemsControl itemsControl:
                itemsControl.Items.Add(child);
                break;
            case HeaderedContentControl headered when child is not UIElement && headered.Content is null && headered.Header is null:
                headered.Header = child;
                break;
            case ContentControl contentControl:
                contentControl.Content = child;
                break;
            case Panel panel when child is UIElement element:
                panel.Children.Add(element);
                break;
            case Decorator decorator when child is UIElement element:
                decorator.Child = element;
                break;
            case Popup popup when child is UIElement element:
                popup.Child = element;
                break;
            case TextBlock textBlock when child is Inline inline:
                textBlock.Inlines.Add(inline);
                break;
            case Span span when child is Inline inline:
                span.Inlines.Add(inline);
                break;
            case CollectionViewSource viewSource when child is SortDescription sort:
                viewSource.SortDescriptions.Add(sort);
                break;
            case Grid grid when child is ColumnDefinition columnDefinition:
                grid.ColumnDefinitions.Add(columnDefinition);
                break;
            case Grid grid when child is RowDefinition rowDefinition:
                grid.RowDefinitions.Add(rowDefinition);
                break;
            case ContentPresenter presenter:
                presenter.Content = child;
                break;
            case UIElement parentElement when child is UIElement childElement:
                childElement.HeadlessParent = parentElement;
                break;
        }

        if (child is UIElement attached && attached.HeadlessParent is null && parent is DependencyObject owner)
        {
            attached.HeadlessParent = owner;
        }
    }

    private static void AddToProperty(object parent, string property, object child)
    {
        if (child is BindingBase binding)
        {
            if (parent is FrameworkElement element)
            {
                element.SetHeadlessBinding(property, binding);
            }

            return;
        }

        var propertyInfo = FindProperty(parent.GetType(), property);
        if (propertyInfo is null)
        {
            return;
        }

        var current = propertyInfo.CanRead ? propertyInfo.GetValue(parent) : null;
        if (current is IList list && !(child is IList) && !propertyInfo.PropertyType.IsInstanceOfType(child))
        {
            list.Add(child);
        }
        else if (current is ResourceDictionary dictionary)
        {
            dictionary[KeyFor(child)] = child;
        }
        else if (propertyInfo.CanWrite)
        {
            propertyInfo.SetValue(parent, child);
        }
        else if (current is IList readOnlyList)
        {
            readOnlyList.Add(child);
        }

        if (child is UIElement childElement && childElement.HeadlessParent is null && parent is DependencyObject owner)
        {
            childElement.HeadlessParent = owner;
        }
    }

    private static object KeyFor(object element)
        => element switch
        {
            Style { HeadlessTargetTypeName: { } typeName } => typeName,
            DataTemplate { DataType: { } dataType } => dataType,
            _ => element,
        };

    private static void SetText(object target, string value)
    {
        switch (target)
        {
            case TextBlock textBlock:
                textBlock.Text = value;
                break;
            case TextBox textBox:
                textBox.Text = value;
                break;
            case Run run:
                run.Text = value;
                break;
            case Span span:
                span.Inlines.Add(new Run(value));
                break;
            case ContentControl contentControl:
                contentControl.Content = value;
                break;
            case ItemsControl itemsControl:
                itemsControl.Items.Add(value);
                break;
            case SolidColorBrush brush when ColorConverter.TryParse(value, out var color):
                brush.Color = color;
                break;
        }
    }

    private static void ResolvePendingMarkup(object target)
    {
        if (!PendingMarkup.TryGetValue(target, out var pending))
        {
            return;
        }

        PendingMarkup.Remove(target);
        foreach (var (property, markup) in pending)
        {
            if (!MarkupParser.TryParse(markup, out var node))
            {
                continue;
            }

            var value = MarkupParser.Evaluate(node, target);
            if (value is not null)
            {
                if (property.Contains('.', StringComparison.Ordinal) && target is DependencyObject attachedTarget)
                {
                    SetAttached(attachedTarget, StripPrefix(property), value);
                }
                else
                {
                    TrySetProperty(target, property, value);
                }
            }
        }
    }

    private static void ApplyStyleHandlers(HeadlessComponent component)
    {
        if (component.StyleHandlers.Count == 0)
        {
            return;
        }

        foreach (var element in component.Ids.Keys.OfType<UIElement>())
        {
            var explicitStyle = (element as FrameworkElement)?.Style;
            foreach (var (style, eventName, handler) in component.StyleHandlers)
            {
                var applies = ReferenceEquals(explicitStyle, style) ||
                              (style.HeadlessKey is null && explicitStyle is null &&
                               style.HeadlessTargetTypeName is { } typeName &&
                               string.Equals(element.GetType().Name, typeName, StringComparison.Ordinal));
                if (applies)
                {
                    element.AddNamed(eventName, handler);
                }
            }
        }
    }

    // ---- lookups --------------------------------------------------------------------------------

    public static object? FindName(object scope, string name)
    {
        for (object? current = scope; current is not null; current = (current as UIElement)?.HeadlessParent)
        {
            if (Components.TryGetValue(current, out var component) && component.Named.TryGetValue(name, out var element))
            {
                return element;
            }
        }

        return GetOwningComponent(scope)?.Named.GetValueOrDefault(name);
    }

    public static void RegisterName(object scope, string name, object element)
    {
        var component = GetOwningComponent(scope);
        if (component is not null)
        {
            component.Named[name] = element;
        }
    }

    /// <summary>The logical children of an element, in markup order.</summary>
    public static IReadOnlyList<DependencyObject> GetLogicalChildren(DependencyObject parent)
    {
        var children = new List<DependencyObject>();
        void AddIfElement(object? candidate)
        {
            if (candidate is DependencyObject dependencyObject && candidate is UIElement or FrameworkContentElement)
            {
                children.Add(dependencyObject);
            }
        }

        switch (parent)
        {
            case Panel panel:
                children.AddRange(panel.Children);
                break;
            case HeaderedContentControl headered:
                AddIfElement(headered.Header);
                AddIfElement(headered.Content);
                break;
            case ItemsControl itemsControl when !itemsControl.Items.IsUsingItemsSource:
                foreach (var item in itemsControl.Items)
                {
                    AddIfElement(item);
                }

                break;
            case ContentControl contentControl:
                AddIfElement(contentControl.Content);
                break;
            case Decorator decorator:
                AddIfElement(decorator.Child);
                break;
            case Popup popup:
                AddIfElement(popup.Child);
                break;
            case ContentPresenter presenter:
                AddIfElement(presenter.Content);
                break;
        }

        return children;
    }

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            yield return current;
            var children = GetLogicalChildren(current);
            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }

    internal static void RaiseLoadedTree(FrameworkElement root)
    {
        foreach (var element in Descendants(root).OfType<FrameworkElement>().ToList())
        {
            if (element is Window window && !ReferenceEquals(window, root))
            {
                continue;
            }

            element.RaiseLoaded();
        }
    }

    internal static void RaiseUnloadedTree(FrameworkElement root)
    {
        foreach (var element in Descendants(root).OfType<FrameworkElement>().ToList())
        {
            element.RaiseUnloaded();
        }
    }

    internal static bool IsBubbling(string eventName) => BubblingEvents.Contains(eventName);

    internal static void NotifyChanged(object element, string property)
    {
        Changed?.Invoke(element, property);
        if (property is "DataContext" && element is DependencyObject dependencyObject)
        {
            HeadlessBindings.ReapplySubtree(dependencyObject);
        }
        else if (element is DependencyObject source)
        {
            HeadlessBindings.OnTargetPropertyChanged(source, property);
        }
    }

    internal static void UncheckRadioGroup(RadioButton source)
    {
        IEnumerable<RadioButton> candidates;
        if (!string.IsNullOrEmpty(source.GroupName))
        {
            var root = (DependencyObject?)Window.GetWindow(source) ?? TopMost(source);
            candidates = Descendants(root).OfType<RadioButton>().Where(radio => radio.GroupName == source.GroupName);
        }
        else
        {
            candidates = source.HeadlessParent is DependencyObject parent
                ? GetLogicalChildren(parent).OfType<RadioButton>().Where(radio => string.IsNullOrEmpty(radio.GroupName))
                : Array.Empty<RadioButton>();
        }

        foreach (var radio in candidates.Where(radio => !ReferenceEquals(radio, source)).ToList())
        {
            radio.IsChecked = false;
        }
    }

    private static DependencyObject TopMost(UIElement element)
    {
        DependencyObject current = element;
        while ((current as UIElement)?.HeadlessParent is { } parent)
        {
            current = parent;
        }

        return current;
    }

    // ---- attached properties and reflection -----------------------------------------------------

    public static void SetAttached(DependencyObject element, string name, object? value) => element.SetNamedValue(name, value);

    public static T GetAttached<T>(DependencyObject element, string name, T defaultValue)
        => element.GetNamedValue(name) is T value ? value : defaultValue;

    private static object ConvertAttached(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) ? real
            : bool.TryParse(value, out var flag) ? flag
            : value;

    internal static bool TryGetShimProperty(DependencyObject target, string name, out object? value)
    {
        var property = FindProperty(target.GetType(), name);
        if (property is { CanRead: true } && IsShimType(property.DeclaringType))
        {
            value = property.GetValue(target);
            return true;
        }

        value = null;
        return false;
    }

    internal static bool TrySetShimProperty(DependencyObject target, string name, object? value)
    {
        var property = FindProperty(target.GetType(), name);
        if (property is { CanWrite: true } && IsShimType(property.DeclaringType))
        {
            property.SetValue(target, ConvertTo(value, property.PropertyType));
            return true;
        }

        return false;
    }

    public static bool TrySetProperty(object target, string name, object? value)
    {
        var property = FindProperty(target.GetType(), name);
        if (property is null || !property.CanWrite)
        {
            if (target is DependencyObject dependencyObject)
            {
                dependencyObject.SetNamedValue(name, value);
                return true;
            }

            return false;
        }

        try
        {
            property.SetValue(target, ConvertTo(value, property.PropertyType));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException or OverflowException)
        {
            return false;
        }
    }

    public static object? TryGetProperty(object target, string name)
    {
        var property = FindProperty(target.GetType(), name);
        return property is { CanRead: true } ? property.GetValue(target) : (target as DependencyObject)?.GetNamedValue(name);
    }

    internal static PropertyInfo? FindProperty(Type type, string name)
        => PropertyCache.GetOrAdd((type, name), static key =>
        {
            for (var current = key.Item1; current is not null; current = current.BaseType)
            {
                var property = current.GetProperty(
                    key.Item2,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (property is not null && property.GetIndexParameters().Length == 0)
                {
                    return property;
                }
            }

            return null;
        });

    private static bool IsShimType(Type? type)
        => type?.Namespace is { } ns && type.Assembly == typeof(HeadlessXaml).Assembly &&
           (ns.StartsWith("System.Windows", StringComparison.Ordinal) || ns.StartsWith("System.ComponentModel", StringComparison.Ordinal));

    public static object? ConvertTo(object? value, Type targetType)
    {
        if (value is null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is string text)
        {
            return ConvertFromString(text, underlying);
        }

        if (underlying == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.CurrentCulture);
        }

        if (underlying == typeof(object))
        {
            return value;
        }

        if (underlying.IsEnum)
        {
            return Enum.ToObject(underlying, value);
        }

        if (value is Color color && underlying == typeof(Brush))
        {
            return new SolidColorBrush(color);
        }

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(underlying))
        {
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static object? ConvertFromString(string text, Type type)
    {
        if (type == typeof(string) || type == typeof(object))
        {
            return text;
        }

        if (type == typeof(bool))
        {
            return bool.Parse(text);
        }

        if (type == typeof(double))
        {
            return text is "Auto" or "NaN" ? double.NaN : double.Parse(text, CultureInfo.InvariantCulture);
        }

        if (type == typeof(int))
        {
            return int.Parse(text, CultureInfo.InvariantCulture);
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, text, ignoreCase: true);
        }

        if (type == typeof(Thickness))
        {
            var parts = text.Split(',', ' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            return parts.Length switch
            {
                1 => new Thickness(parts[0]),
                2 => new Thickness(parts[0], parts[1], parts[0], parts[1]),
                4 => new Thickness(parts[0], parts[1], parts[2], parts[3]),
                _ => default(Thickness),
            };
        }

        if (type == typeof(CornerRadius))
        {
            var parts = text.Split(',', ' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            return parts.Length == 4 ? new CornerRadius(parts[0], parts[1], parts[2], parts[3]) : new CornerRadius(parts.FirstOrDefault());
        }

        if (type == typeof(GridLength))
        {
            if (text == "Auto")
            {
                return GridLength.Auto;
            }

            if (text.EndsWith('*'))
            {
                var factor = text.Length == 1 ? 1 : double.Parse(text[..^1], CultureInfo.InvariantCulture);
                return new GridLength(factor, GridUnitType.Star);
            }

            return new GridLength(double.Parse(text, CultureInfo.InvariantCulture));
        }

        if (type == typeof(DataGridLength))
        {
            return text switch
            {
                "Auto" => DataGridLength.Auto,
                "SizeToCells" => DataGridLength.SizeToCells,
                "SizeToHeader" => DataGridLength.SizeToHeader,
                _ when text.EndsWith('*') => new DataGridLength(text.Length == 1 ? 1 : double.Parse(text[..^1], CultureInfo.InvariantCulture), DataGridLengthUnitType.Star),
                _ => new DataGridLength(double.Parse(text, CultureInfo.InvariantCulture)),
            };
        }

        if (type == typeof(Brush) || type == typeof(SolidColorBrush))
        {
            return ColorConverter.TryParse(text, out var color) ? new SolidColorBrush(color) : null;
        }

        if (type == typeof(Color))
        {
            return ColorConverter.TryParse(text, out var color) ? color : default;
        }

        if (type == typeof(FontWeight))
        {
            return Enum.TryParse<FontWeight>(text, out var weight) ? weight : (text is "Medium" or "DemiBold" ? FontWeight.SemiBold : FontWeight.Normal);
        }

        if (type == typeof(FontFamily))
        {
            return new FontFamily(text);
        }

        if (type == typeof(Uri))
        {
            return new Uri(text, UriKind.RelativeOrAbsolute);
        }

        if (type == typeof(Duration))
        {
            return new Duration(TimeSpan.Parse(text, CultureInfo.InvariantCulture));
        }

        if (type == typeof(Point))
        {
            var parts = text.Split(',').Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            return new Point(parts[0], parts[1]);
        }

        if (type == typeof(Input.Cursor))
        {
            return typeof(Input.Cursors).GetProperty(text)?.GetValue(null);
        }

        if (type == typeof(Type))
        {
            return HeadlessResources.ResolveType(text);
        }

        if (typeof(ImageSource).IsAssignableFrom(type) || type == typeof(Transform) || type == typeof(Geometry))
        {
            return null;
        }

        var converter = TypeDescriptor.GetConverter(type);
        return converter.CanConvertFrom(typeof(string)) ? converter.ConvertFromInvariantString(text) : null;
    }

    internal static string StripPrefix(string value)
    {
        value = value.Trim();
        if (value.StartsWith("{x:Type", StringComparison.Ordinal))
        {
            value = value["{x:Type".Length..].Trim().TrimEnd('}').Trim();
        }

        var colon = value.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 ? value[(colon + 1)..] : value;
    }

    private static Delegate Adapt(Type handlerType, HeadlessHandler handler)
    {
        var invoke = handlerType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
        Expression sender = parameters.Length > 0 ? Expression.Convert(parameters[0], typeof(object)) : Expression.Constant(null);
        Expression args = parameters.Length > 1 && typeof(EventArgs).IsAssignableFrom(parameters[1].Type)
            ? Expression.Convert(parameters[1], typeof(EventArgs))
            : parameters.Length > 1
                ? Expression.Call(typeof(HeadlessEventArgs), nameof(HeadlessEventArgs.Box), null, Expression.Convert(parameters[1], typeof(object)))
                : Expression.Constant(EventArgs.Empty);
        var body = Expression.Invoke(Expression.Constant(handler), sender, args);
        return Expression.Lambda(handlerType, body, parameters).Compile();
    }
}

/// <summary>Creates handler argument objects when the web UI raises an event with generic args.</summary>
public static class HeadlessEventArgs
{
    public static EventArgs Box(object? value) => value is EventArgs args ? args : new ValueEventArgs(value);

    public static object Create(Type type, EventArgs source)
    {
        var routedSource = (source as RoutedEventArgs)?.Source;
        object instance;
        if (type == typeof(SelectionChangedEventArgs))
        {
            instance = new SelectionChangedEventArgs(null, Array.Empty<object>(), Array.Empty<object>());
        }
        else if (type == typeof(TextChangedEventArgs))
        {
            instance = new TextChangedEventArgs(null, UndoAction.None);
        }
        else if (type == typeof(CancelEventArgs))
        {
            instance = new CancelEventArgs();
        }
        else if (type == typeof(DependencyPropertyChangedEventArgs))
        {
            instance = default(DependencyPropertyChangedEventArgs);
        }
        else if (type.GetConstructor(Type.EmptyTypes) is { } constructor)
        {
            instance = constructor.Invoke(null);
        }
        else
        {
            instance = RuntimeHelpers.GetUninitializedObject(type);
        }

        if (instance is RoutedEventArgs routed && routedSource is not null)
        {
            routed.Source = routedSource;
        }

        return instance;
    }

    public sealed class ValueEventArgs : EventArgs
    {
        public ValueEventArgs(object? value)
        {
            Value = value;
        }

        public object? Value { get; }
    }
}
