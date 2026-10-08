using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace System.Windows.Headless;

/// <summary>A parsed markup extension such as <c>{Binding Path, Converter={StaticResource X}}</c>.</summary>
public sealed class MarkupNode
{
    public string Name { get; init; } = string.Empty;
    public List<object> Positional { get; } = new();
    public Dictionary<string, object> Named { get; } = new(StringComparer.Ordinal);
}

public static class MarkupParser
{
    public static bool TryParse(string text, out MarkupNode node)
    {
        node = new MarkupNode();
        text = text.Trim();
        if (!text.StartsWith('{') || !text.EndsWith('}'))
        {
            return false;
        }

        var position = 0;
        try
        {
            node = ParseNode(text, ref position);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MarkupNode ParseNode(string text, ref int position)
    {
        Expect(text, ref position, '{');
        SkipWhitespace(text, ref position);
        var nameStart = position;
        while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not '}' and not ',')
        {
            position++;
        }

        var node = new MarkupNode { Name = text[nameStart..position] };
        SkipWhitespace(text, ref position);
        while (position < text.Length && text[position] != '}')
        {
            SkipWhitespace(text, ref position);
            object value;
            string? key = null;
            if (text[position] == '{')
            {
                value = ParseNode(text, ref position);
            }
            else
            {
                var token = ReadToken(text, ref position);
                SkipWhitespace(text, ref position);
                if (position < text.Length && text[position] == '=')
                {
                    position++;
                    SkipWhitespace(text, ref position);
                    key = token.Trim();
                    value = position < text.Length && text[position] == '{' && !text[position..].StartsWith("{}", StringComparison.Ordinal)
                        ? ParseNode(text, ref position)
                        : ReadToken(text, ref position).Trim();
                }
                else
                {
                    value = token.Trim();
                }
            }

            if (key is null)
            {
                node.Positional.Add(value);
            }
            else
            {
                node.Named[key] = value;
            }

            SkipWhitespace(text, ref position);
            if (position < text.Length && text[position] == ',')
            {
                position++;
            }
        }

        Expect(text, ref position, '}');
        return node;
    }

    private static string ReadToken(string text, ref int position)
    {
        var builder = new StringBuilder();
        var braceDepth = 0;
        var quote = '\0';
        while (position < text.Length)
        {
            var ch = text[position];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }
                else
                {
                    builder.Append(ch);
                }

                position++;
                continue;
            }

            if (ch is '\'' or '"')
            {
                quote = ch;
                position++;
                continue;
            }

            if (ch == '\\' && position + 1 < text.Length)
            {
                builder.Append(text[position + 1]);
                position += 2;
                continue;
            }

            if (ch == '{')
            {
                braceDepth++;
            }
            else if (ch == '}')
            {
                if (braceDepth == 0)
                {
                    break;
                }

                braceDepth--;
            }
            else if ((ch == ',' || ch == '=') && braceDepth == 0)
            {
                break;
            }

            builder.Append(ch);
            position++;
        }

        var token = builder.ToString();
        return token.StartsWith("{}", StringComparison.Ordinal) ? token[2..] : token;
    }

    private static void Expect(string text, ref int position, char expected)
    {
        if (position >= text.Length || text[position] != expected)
        {
            throw new FormatException($"Expected '{expected}' at {position} in '{text}'.");
        }

        position++;
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }

    internal static BindingBase ToBinding(MarkupNode node, object target)
    {
        if (node.Name == "MultiBinding")
        {
            var multi = new MultiBinding();
            if (node.Named.TryGetValue("Converter", out var multiConverter))
            {
                multi.Converter = Evaluate(multiConverter, target) as IMultiValueConverter;
            }

            if (node.Named.TryGetValue("StringFormat", out var multiFormat))
            {
                multi.StringFormat = multiFormat as string;
            }

            return multi;
        }

        var binding = new Binding();
        if (node.Name == "TemplateBinding")
        {
            binding.RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent);
        }

        if (node.Positional.FirstOrDefault() is string positionalPath)
        {
            binding.Path = new PropertyPath(positionalPath);
        }

        foreach (var (key, value) in node.Named)
        {
            switch (key)
            {
                case "Path":
                    binding.Path = new PropertyPath(value as string ?? string.Empty);
                    break;
                case "Mode":
                    binding.Mode = Enum.Parse<BindingMode>((string)value, ignoreCase: true);
                    break;
                case "UpdateSourceTrigger":
                    binding.UpdateSourceTrigger = Enum.Parse<UpdateSourceTrigger>((string)value, ignoreCase: true);
                    break;
                case "Converter":
                    binding.Converter = Evaluate(value, target) as IValueConverter;
                    break;
                case "ConverterParameter":
                    binding.ConverterParameter = value is MarkupNode parameterNode ? Evaluate(parameterNode, target) : value;
                    break;
                case "StringFormat":
                    binding.StringFormat = value as string;
                    break;
                case "ElementName":
                    binding.ElementName = value as string;
                    break;
                case "FallbackValue":
                    binding.FallbackValue = value is MarkupNode fallbackNode ? Evaluate(fallbackNode, target) : value;
                    break;
                case "TargetNullValue":
                    binding.TargetNullValue = value is MarkupNode nullNode ? Evaluate(nullNode, target) : value;
                    break;
                case "Source":
                    binding.Source = value is MarkupNode sourceNode ? Evaluate(sourceNode, target) : value;
                    break;
                case "RelativeSource":
                    binding.RelativeSource = value as MarkupNode is { } relativeNode ? ToRelativeSource(relativeNode) : null;
                    break;
            }
        }

        return binding;
    }

    private static RelativeSource ToRelativeSource(MarkupNode node)
    {
        var modeText = node.Positional.FirstOrDefault() as string ?? (node.Named.GetValueOrDefault("Mode") as string)
            ?? (node.Named.ContainsKey("AncestorType") ? "FindAncestor" : "Self");
        var relative = new RelativeSource(Enum.Parse<RelativeSourceMode>(modeText, ignoreCase: true));
        if (node.Named.GetValueOrDefault("AncestorType") is { } ancestor)
        {
            relative.AncestorType = ancestor switch
            {
                MarkupNode typeNode => HeadlessResources.ResolveType(typeNode.Positional.FirstOrDefault() as string ?? string.Empty),
                string typeName => HeadlessResources.ResolveType(typeName),
                _ => null,
            };
        }

        if (node.Named.GetValueOrDefault("AncestorLevel") is string level && int.TryParse(level, out var parsedLevel))
        {
            relative.AncestorLevel = parsedLevel;
        }

        return relative;
    }

    /// <summary>Evaluates a non-binding markup extension (resources, x:Static, x:Null, x:Type).</summary>
    public static object? Evaluate(object node, object target)
    {
        if (node is not MarkupNode markup)
        {
            return node;
        }

        var argument = markup.Positional.FirstOrDefault() as string ?? markup.Named.GetValueOrDefault("ResourceKey") as string;
        switch (markup.Name)
        {
            case "StaticResource":
            case "DynamicResource":
                return argument is null ? null : (target as FrameworkElement)?.TryFindResource(argument) ?? HeadlessResources.Find(argument);
            case "x:Null":
                return null;
            case "x:Type":
                return argument is null ? null : HeadlessResources.ResolveType(argument);
            case "x:Static":
                return argument is null ? null : HeadlessResources.ResolveStatic(argument);
            case "Binding":
            case "MultiBinding":
            case "TemplateBinding":
                return ToBinding(markup, target);
            case "RelativeSource":
                return ToRelativeSource(markup);
            default:
                return null;
        }
    }
}

/// <summary>
/// Application-level resources. The desktop app merges its theme dictionaries in App.xaml; the headless
/// host reads the same files (embedded at build time) so palette brushes and converters resolve by key.
/// </summary>
public static class HeadlessResources
{
    private static readonly Lazy<Dictionary<string, object?>> Theme = new(LoadTheme);
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public static object? Find(object key)
    {
        if (key is string text && Theme.Value.TryGetValue(text, out var value))
        {
            return value;
        }

        if (key is string styleKey && (styleKey.EndsWith("Style", StringComparison.Ordinal) || styleKey.EndsWith("Template", StringComparison.Ordinal)))
        {
            return styleKey.EndsWith("Style", StringComparison.Ordinal) ? new Style { HeadlessKey = styleKey } : new ControlTemplate();
        }

        return null;
    }

    public static IReadOnlyDictionary<string, object?> All => Theme.Value;

    public static Type? ResolveType(string name)
        => TypeCache.GetOrAdd(HeadlessXaml.StripPrefix(name), static simpleName =>
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type is not null).ToArray()!;
                }

                var match = types.FirstOrDefault(type =>
                    type.Name == simpleName &&
                    (type.Namespace?.StartsWith("System.Windows", StringComparison.Ordinal) == true ||
                     type.Namespace?.StartsWith("TbotUltra", StringComparison.Ordinal) == true));
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        });

    public static object? ResolveStatic(string reference)
    {
        var text = HeadlessXaml.StripPrefix(reference.Trim());
        var dot = text.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var type = ResolveType(text[..dot]);
        var member = text[(dot + 1)..];
        return type?.GetProperty(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
               ?? type?.GetField(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    private static Dictionary<string, object?> LoadTheme()
    {
        var resources = new Dictionary<string, object?>(StringComparer.Ordinal);
        var assembly = typeof(HeadlessResources).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Theme/", StringComparison.Ordinal))
            .OrderBy(name => name.EndsWith("Palette.xaml", StringComparison.Ordinal) ? 0 : 1)
            .ToList();
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            var document = XDocument.Load(stream);
            foreach (var element in document.Root?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var key = element.Attribute(x + "Key")?.Value;
                var localName = element.Name.LocalName;
                if (localName == "Style")
                {
                    var style = new Style
                    {
                        HeadlessKey = key,
                        HeadlessTargetTypeName = HeadlessXaml.StripPrefix(element.Attribute("TargetType")?.Value ?? string.Empty),
                    };
                    resources[key ?? style.HeadlessTargetTypeName ?? Guid.NewGuid().ToString()] = style;
                    continue;
                }

                if (key is null)
                {
                    continue;
                }

                resources[key] = localName switch
                {
                    "Color" => ColorConverter.TryParse(element.Value, out var color) ? color : default(Color),
                    "SolidColorBrush" => CreateBrush(element, resources),
                    "ControlTemplate" => new ControlTemplate(),
                    "DataTemplate" => new DataTemplate(),
                    "Thickness" => HeadlessXaml.ConvertTo(element.Value, typeof(Thickness)),
                    "CornerRadius" => HeadlessXaml.ConvertTo(element.Value, typeof(CornerRadius)),
                    "Double" => double.Parse(element.Value, CultureInfo.InvariantCulture),
                    "FontFamily" => new FontFamily(element.Value),
                    _ => CreateInstance(localName),
                };
            }
        }

        return resources;
    }

    private static object? CreateBrush(XElement element, Dictionary<string, object?> resources)
    {
        var colorText = element.Attribute("Color")?.Value ?? string.Empty;
        Color color;
        if (MarkupParser.TryParse(colorText, out var node) && node.Positional.FirstOrDefault() is string colorKey)
        {
            color = resources.GetValueOrDefault(colorKey) is Color referenced ? referenced : Colors.Magenta;
        }
        else if (!ColorConverter.TryParse(colorText, out color))
        {
            color = Colors.Magenta;
        }

        var brush = new SolidColorBrush(color);
        if (element.Attribute("Opacity")?.Value is { } opacity)
        {
            brush.Opacity = double.Parse(opacity, CultureInfo.InvariantCulture);
        }

        brush.Freeze();
        return brush;
    }

    private static object? CreateInstance(string typeName)
    {
        var type = ResolveType(typeName);
        return type?.GetConstructor(Type.EmptyTypes)?.Invoke(null);
    }
}

public static class HeadlessCollections
{
    public static IEnumerable<object?> Sort(IEnumerable<object?> items, IEnumerable<SortDescription> sorts)
    {
        IOrderedEnumerable<object?>? ordered = null;
        foreach (var sort in sorts)
        {
            object? Key(object? item) => item is null ? null : GetPathValue(item, sort.PropertyName);
            var comparer = Comparer<object?>.Create(CompareValues);
            ordered = ordered is null
                ? sort.Direction == ListSortDirection.Ascending ? items.OrderBy(Key, comparer) : items.OrderByDescending(Key, comparer)
                : sort.Direction == ListSortDirection.Ascending ? ordered.ThenBy(Key, comparer) : ordered.ThenByDescending(Key, comparer);
        }

        return ordered ?? items;
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null ? (right is null ? 0 : -1) : 1;
        }

        if (left is IComparable comparable && left.GetType() == right.GetType())
        {
            return comparable.CompareTo(right);
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCulture);
    }

    public static object? GetPathValue(object? item, string? path)
        => string.IsNullOrEmpty(path) ? item : HeadlessBindings.ResolvePath(item, path, out _);

    public static string DisplayText(object? item, string? displayMemberPath)
        => item switch
        {
            null => string.Empty,
            ContentControl { Content: var content } => content?.ToString() ?? string.Empty,
            _ => Convert.ToString(GetPathValue(item, displayMemberPath), CultureInfo.CurrentCulture) ?? string.Empty,
        };

    internal sealed class DeferredRefresh : IDisposable
    {
        private readonly Action _refresh;

        public DeferredRefresh(Action refresh)
        {
            _refresh = refresh;
        }

        public void Dispose() => _refresh();
    }
}

/// <summary>A pending message box, answered by the web UI.</summary>
public sealed class HeadlessMessage
{
    private readonly TaskCompletionSource<MessageBoxResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal HeadlessMessage(Window? owner, string text, string caption, MessageBoxButton buttons, MessageBoxImage icon)
    {
        Owner = owner;
        Text = text;
        Caption = caption;
        Buttons = buttons;
        Icon = icon;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public Window? Owner { get; }
    public string Text { get; }
    public string Caption { get; }
    public MessageBoxButton Buttons { get; }
    public MessageBoxImage Icon { get; }
    public Task<MessageBoxResult> Result => _result.Task;

    public void Answer(MessageBoxResult result) => _result.TrySetResult(result);
}

public static class HeadlessDialogs
{
    private static readonly List<HeadlessMessage> Pending = new();

    /// <summary>When no UI is attached, message boxes resolve to this (cancel/no for confirmations).</summary>
    public static Func<HeadlessMessage, MessageBoxResult> UnattendedAnswer { get; set; } = message => message.Buttons switch
    {
        MessageBoxButton.OK => MessageBoxResult.OK,
        MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
        MessageBoxButton.YesNo => MessageBoxResult.No,
        _ => MessageBoxResult.Cancel,
    };

    /// <summary>True when a web client is connected and will answer dialogs.</summary>
    public static Func<bool> IsAttended { get; set; } = static () => false;

    public static event Action<HeadlessMessage>? MessageShown;

    public static event Action<HeadlessMessage>? MessageClosed;

    public static IReadOnlyList<HeadlessMessage> Open
    {
        get
        {
            lock (Pending)
            {
                return Pending.ToList();
            }
        }
    }

    internal static MessageBoxResult ShowMessage(Window? owner, string text, string caption, MessageBoxButton buttons, MessageBoxImage icon)
    {
        var message = new HeadlessMessage(owner, text ?? string.Empty, caption ?? string.Empty, buttons, icon);
        Console.WriteLine($"[dialog] {message.Caption}: {message.Text}");
        if (!IsAttended())
        {
            return UnattendedAnswer(message);
        }

        lock (Pending)
        {
            Pending.Add(message);
        }

        MessageShown?.Invoke(message);
        WaitOnDispatcher(message.Result);
        lock (Pending)
        {
            Pending.Remove(message);
        }

        MessageClosed?.Invoke(message);
        return message.Result.IsCompletedSuccessfully ? message.Result.Result : UnattendedAnswer(message);
    }

    /// <summary>Blocks the caller like a modal dialog while the dispatcher keeps running.</summary>
    public static void WaitOnDispatcher(Task task)
    {
        var dispatcher = Dispatcher.Main;
        if (!dispatcher.CheckAccess())
        {
            task.Wait();
            return;
        }

        var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.InvokeAsync(() => frame.Continue = false), TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
    }
}

/// <summary>Open windows, in z-order. The web UI renders the top-most modal over the main window.</summary>
public static class HeadlessWindows
{
    private static readonly List<Window> OpenWindows = new();

    public static event Action<Window>? WindowOpened;

    public static event Action<Window>? WindowClosed;

    public static event Action<Window>? WindowActivated;

    public static IReadOnlyList<Window> Open
    {
        get
        {
            lock (OpenWindows)
            {
                return OpenWindows.ToList();
            }
        }
    }

    internal static void Opened(Window window)
    {
        lock (OpenWindows)
        {
            OpenWindows.Remove(window);
            OpenWindows.Add(window);
        }

        WindowOpened?.Invoke(window);
    }

    internal static void ClosedWindow(Window window)
    {
        lock (OpenWindows)
        {
            OpenWindows.Remove(window);
        }

        WindowClosed?.Invoke(window);
    }

    internal static void Activate(Window window)
    {
        lock (OpenWindows)
        {
            if (OpenWindows.Remove(window))
            {
                OpenWindows.Add(window);
            }
        }

        WindowActivated?.Invoke(window);
    }
}
