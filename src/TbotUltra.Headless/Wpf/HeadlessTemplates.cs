using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;

namespace System.Windows.Headless;

/// <summary>
/// Expands data templates at runtime: builds the template's elements from the markup the generator
/// attached, wires the handlers it recorded (by element position, matching the generator's numbering),
/// and caches one instance per item so element identity and state survive re-renders.
/// </summary>
public static class HeadlessTemplates
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly ConcurrentDictionary<string, Type?> PresentationTypes = new(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<object, Dictionary<object, FrameworkElement>> Realized = new();
    private static readonly HashSet<string> TemplateElements = new(StringComparer.Ordinal)
    {
        "DataTemplate", "ControlTemplate", "ItemsPanelTemplate", "HierarchicalDataTemplate",
    };

    /// <summary>The element shown for <paramref name="item"/> inside <paramref name="owner"/> (created once).</summary>
    public static FrameworkElement Realize(object owner, object cacheKey, DataTemplate? template, object? item, Func<FrameworkElement>? fallback = null)
    {
        var cache = Realized.GetValue(owner, static _ => new Dictionary<object, FrameworkElement>(ReferenceEqualityComparer.Instance));
        if (cache.TryGetValue(cacheKey, out var existing))
        {
            if (!ReferenceEquals(existing.DataContext, item))
            {
                existing.DataContext = item;
            }

            return existing;
        }

        var element = (template is not null ? Load(template) : null) ?? fallback?.Invoke() ?? new TextBlock { Text = Convert.ToString(item) ?? string.Empty };
        element.HeadlessParent = owner as DependencyObject;
        element.DataContext = item;
        cache[cacheKey] = element;
        foreach (var descendant in HeadlessXaml.Descendants(element).ToList())
        {
            HeadlessXaml.ResolvePendingMarkupFor(descendant);
            if (descendant is FrameworkElement framework)
            {
                HeadlessXaml.ApplyStyleSetters(framework);
            }
        }

        HeadlessBindings.ReapplySubtree(element);
        return element;
    }

    /// <summary>Drops cached instances whose items left the source, so removed rows release their elements.</summary>
    public static void Prune(object owner, IEnumerable<object> liveKeys)
    {
        if (!Realized.TryGetValue(owner, out var cache))
        {
            return;
        }

        var live = new HashSet<object>(liveKeys, ReferenceEqualityComparer.Instance);
        foreach (var key in cache.Keys.Where(key => !live.Contains(key)).ToList())
        {
            cache.Remove(key);
        }
    }

    /// <summary>Builds a fresh instance of <paramref name="template"/>'s visual tree, or null without markup.</summary>
    public static FrameworkElement? Load(FrameworkTemplate template)
    {
        var info = HeadlessXaml.GetTemplateInfo(template);
        if (info is null || string.IsNullOrEmpty(info.Markup))
        {
            return null;
        }

        var root = XElement.Parse(info.Markup);
        var indexes = new Dictionary<XElement, int>();
        var index = 0;
        foreach (var descendant in root.Descendants())
        {
            indexes[descendant] = ++index;
        }

        var context = new LoadContext(info, indexes);
        FrameworkElement? visualRoot = null;
        foreach (var child in root.Elements())
        {
            if (child.Name.LocalName.Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            var created = Build(context, child, parent: null, parentProperty: null);
            visualRoot ??= created as FrameworkElement;
        }

        return visualRoot;
    }

    private static object? Build(LoadContext context, XElement element, object? parent, string? parentProperty)
    {
        var localName = element.Name.LocalName;
        if (localName.Contains('.', StringComparison.Ordinal))
        {
            var property = localName[(localName.LastIndexOf('.') + 1)..];
            if (property is "Triggers" or "Style" or "Template" or "Resources")
            {
                return null;
            }

            foreach (var child in element.Elements())
            {
                Build(context, child, parent, property);
            }

            return null;
        }

        if (localName is "Style" or "Setter" or "Trigger" or "DataTrigger" or "EventTrigger" or "ResourceDictionary")
        {
            return null;
        }

        if (TemplateElements.Contains(localName))
        {
            var nested = localName == "DataTemplate" ? new DataTemplate() : (FrameworkTemplate)new ControlTemplate();
            HeadlessXaml.SetTemplateMarkup(nested, element.ToString(SaveOptions.DisableFormatting));
            var offset = context.Indexes[element];
            foreach (var (handlerIndex, eventName, handler) in context.Info.Handlers)
            {
                if (handlerIndex > offset)
                {
                    HeadlessXaml.AddTemplateHandler(nested, handlerIndex - offset, eventName, handler);
                }
            }

            if (parent is not null)
            {
                HeadlessXaml.AttachTemplateChild(parent, parentProperty, nested);
            }

            return nested;
        }

        var type = ResolveType(element);
        if (type is null || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
        {
            return null;
        }

        var instance = Activator.CreateInstance(type)!;
        var elementIndex = context.Indexes.GetValueOrDefault(element);
        var handlers = context.Info.Handlers.Where(handler => handler.ElementIndex == elementIndex).ToList();
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            if (attribute.Name.NamespaceName == XamlNamespace)
            {
                if (attribute.Name.LocalName == "Name")
                {
                    HeadlessXaml.SetLiteral(instance, "Name", attribute.Value);
                }

                continue;
            }

            var name = attribute.Name.LocalName;
            var eventName = name[(name.LastIndexOf('.') + 1)..];
            if (handlers.Any(handler => handler.EventName == eventName))
            {
                continue;
            }

            var value = attribute.Value;
            if (value.StartsWith("{}", StringComparison.Ordinal))
            {
                HeadlessXaml.SetLiteral(instance, name, value[2..]);
            }
            else if (value.StartsWith('{'))
            {
                HeadlessXaml.SetMarkup(instance, name, value);
            }
            else
            {
                HeadlessXaml.SetLiteral(instance, name, value);
            }
        }

        foreach (var (_, eventName, handler) in handlers)
        {
            HeadlessXaml.AddHandler(instance, eventName, handler);
        }

        if (!element.HasElements && element.Nodes().OfType<XText>().FirstOrDefault()?.Value.Trim() is { Length: > 0 } text)
        {
            HeadlessXaml.SetLiteral(instance, "__text", text);
        }

        foreach (var child in element.Elements())
        {
            Build(context, child, instance, parentProperty: null);
        }

        if (parent is not null)
        {
            HeadlessXaml.AttachTemplateChild(parent, parentProperty, instance);
        }

        return instance;
    }

    private static Type? ResolveType(XElement element)
    {
        var ns = element.Name.NamespaceName;
        var localName = element.Name.LocalName;
        if (ns.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var clrNamespace = ns["clr-namespace:".Length..].Split(';')[0];
            return HeadlessResources.ResolveType(clrNamespace + "." + localName) ?? HeadlessResources.ResolveType(localName);
        }

        return PresentationTypes.GetOrAdd(localName, static name =>
        {
            var assembly = typeof(HeadlessTemplates).Assembly;
            foreach (var candidate in new[]
                     {
                         "System.Windows.Controls", "System.Windows", "System.Windows.Controls.Primitives",
                         "System.Windows.Documents", "System.Windows.Shapes", "System.Windows.Media", "System.Windows.Data",
                     })
            {
                var type = assembly.GetType(candidate + "." + name);
                if (type is not null)
                {
                    return type;
                }
            }

            return null;
        });
    }

    private sealed record LoadContext(HeadlessTemplateInfo Info, Dictionary<XElement, int> Indexes);
}
