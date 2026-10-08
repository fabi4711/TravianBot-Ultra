using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Xml.Linq;

namespace System.Windows.Headless;

/// <summary>
/// Styles for the headless host: parses style markup (setters, data/property triggers) and keeps each
/// styled element's properties in sync — local values win, then the last matching trigger, then the
/// style's own setter, mirroring WPF's precedence for the plain properties a renderer cares about.
/// Data triggers watch their binding through hidden bindings on the element itself.
/// </summary>
public static class HeadlessStyles
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    internal const string TriggerPropertyPrefix = "__trigger";

    private static readonly ConditionalWeakTable<FrameworkElement, StyledElement> Styled = new();

    /// <summary>Builds a <see cref="Style"/> from its markup (the <c>&lt;Style&gt;</c> element).</summary>
    public static Style Parse(string markup, object? scope) => Parse(XElement.Parse(markup), scope);

    public static Style Parse(XElement element, object? scope, Func<string, Style?>? resolveBasedOn = null)
    {
        var key = element.Attribute(XName.Get("Key", XamlNamespace))?.Value;
        var targetTypeName = HeadlessXaml.StripPrefix(element.Attribute("TargetType")?.Value ?? string.Empty);
        var targetType = targetTypeName.Length > 0 ? HeadlessResources.ResolveType(targetTypeName) : null;
        var style = new Style { HeadlessKey = key, HeadlessTargetTypeName = targetTypeName, TargetType = targetType };
        if (element.Attribute("BasedOn")?.Value is { } basedOn && MarkupParser.TryParse(basedOn, out var node))
        {
            style.BasedOn = resolveBasedOn is not null && node.Positional.FirstOrDefault() is string baseKey
                ? resolveBasedOn(baseKey)
                : MarkupParser.Evaluate(node, scope ?? new object()) as Style;
        }

        foreach (var child in element.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "Setter":
                    if (CreateSetter(child, targetType) is { } setter)
                    {
                        style.Setters.Add(setter);
                        style.HeadlessSetters[setter.Property!.Name] = setter.HeadlessRawValue ?? string.Empty;
                    }

                    break;
                case "Style.Setters":
                    foreach (var nested in child.Elements().Where(e => e.Name.LocalName == "Setter"))
                    {
                        if (CreateSetter(nested, targetType) is { } nestedSetter)
                        {
                            style.Setters.Add(nestedSetter);
                            style.HeadlessSetters[nestedSetter.Property!.Name] = nestedSetter.HeadlessRawValue ?? string.Empty;
                        }
                    }

                    break;
                case "Style.Triggers":
                    foreach (var trigger in child.Elements())
                    {
                        if (CreateTrigger(trigger, targetType, scope) is { } created)
                        {
                            style.Triggers.Add(created);
                        }
                    }

                    break;
            }
        }

        return style;
    }

    private static TriggerBase? CreateTrigger(XElement element, Type? targetType, object? scope)
    {
        TriggerBase? trigger = element.Name.LocalName switch
        {
            "DataTrigger" => new DataTrigger
            {
                Binding = BindingFrom(element.Attribute("Binding")?.Value, scope),
                Value = element.Attribute("Value")?.Value,
            },
            "Trigger" => new Trigger
            {
                Property = ResolveProperty(element.Attribute("Property")?.Value, targetType),
                Value = element.Attribute("Value")?.Value,
            },
            "MultiDataTrigger" => new MultiDataTrigger(),
            "MultiTrigger" => new MultiTrigger(),
            _ => null,
        };
        if (trigger is null)
        {
            return null;
        }

        foreach (var child in element.Elements())
        {
            var name = child.Name.LocalName;
            if (name == "Setter" && CreateSetter(child, targetType) is { } setter)
            {
                trigger.Setters.Add(setter);
            }
            else if (name.EndsWith(".Setters", StringComparison.Ordinal))
            {
                foreach (var nested in child.Elements().Where(e => e.Name.LocalName == "Setter"))
                {
                    if (CreateSetter(nested, targetType) is { } nestedSetter)
                    {
                        trigger.Setters.Add(nestedSetter);
                    }
                }
            }
            else if (name.EndsWith(".Conditions", StringComparison.Ordinal))
            {
                foreach (var condition in child.Elements().Where(e => e.Name.LocalName == "Condition"))
                {
                    var created = new Condition
                    {
                        Binding = BindingFrom(condition.Attribute("Binding")?.Value, scope),
                        Property = ResolveProperty(condition.Attribute("Property")?.Value, targetType),
                        Value = condition.Attribute("Value")?.Value,
                    };
                    (trigger as MultiDataTrigger)?.Conditions.Add(created);
                    (trigger as MultiTrigger)?.Conditions.Add(created);
                }
            }
        }

        return trigger;
    }

    private static BindingBase? BindingFrom(string? markup, object? scope)
        => markup is not null && MarkupParser.TryParse(markup, out var node) ? MarkupParser.Evaluate(node, scope ?? new object()) as BindingBase : null;

    private static Setter? CreateSetter(XElement element, Type? targetType)
    {
        var propertyName = element.Attribute("Property")?.Value;
        if (propertyName is null)
        {
            return null;
        }

        var value = element.Attribute("Value")?.Value ?? element.Elements().FirstOrDefault()?.ToString(SaveOptions.DisableFormatting);
        return new Setter
        {
            Property = ResolveProperty(propertyName, targetType),
            HeadlessRawValue = value,
            Value = value,
            TargetName = element.Attribute("TargetName")?.Value,
        };
    }

    /// <summary>The dependency property for a markup name such as <c>Foreground</c> or <c>TextBlock.Foreground</c>.</summary>
    internal static DependencyProperty ResolveProperty(string? name, Type? targetType)
    {
        name = HeadlessXaml.StripPrefix(name ?? string.Empty);
        var dot = name.LastIndexOf('.');
        var ownerType = dot > 0 ? HeadlessResources.ResolveType(name[..dot]) ?? targetType : targetType;
        var simple = dot > 0 ? name[(dot + 1)..] : name;
        for (var type = ownerType; type is not null; type = type.BaseType)
        {
            var field = type.GetField(simple + "Property", Reflection.BindingFlags.Public | Reflection.BindingFlags.Static | Reflection.BindingFlags.FlattenHierarchy);
            if (field?.GetValue(null) is DependencyProperty property)
            {
                return property;
            }
        }

        return DependencyProperty.Register(simple, typeof(object), ownerType ?? typeof(FrameworkElement));
    }

    // ---- runtime ---------------------------------------------------------------------------------

    /// <summary>Applies <paramref name="element"/>'s style: setters now, triggers whenever their inputs change.</summary>
    public static void Attach(FrameworkElement element)
    {
        if (element.Style is not { } style)
        {
            return;
        }

        var chain = new List<Style>();
        for (var current = style; current is not null && chain.Count < 8; current = current.BasedOn)
        {
            chain.Insert(0, current);
        }

        var state = new StyledElement(element, chain);
        Styled.AddOrUpdate(element, state);
        var index = 0;
        foreach (var trigger in state.Triggers)
        {
            foreach (var binding in TriggerBindings(trigger))
            {
                var property = TriggerPropertyPrefix + index++;
                state.TriggerProperties[binding] = property;
                HeadlessBindings.SetBinding(element, property, binding);
            }
        }

        state.Evaluate();
    }

    internal static void OnPropertyChanged(DependencyObject target, string property)
    {
        if (target is FrameworkElement element && Styled.TryGetValue(element, out var state) &&
            (property.StartsWith(TriggerPropertyPrefix, StringComparison.Ordinal) || state.WatchedProperties.Contains(property)))
        {
            state.Evaluate();
        }
    }

    private static IEnumerable<BindingBase> TriggerBindings(TriggerBase trigger)
        => trigger switch
        {
            DataTrigger { Binding: { } binding } => new[] { binding },
            MultiDataTrigger multi => multi.Conditions.Select(condition => condition.Binding).OfType<BindingBase>(),
            _ => Array.Empty<BindingBase>(),
        };

    internal static bool ValueMatches(object? actual, object? expected)
    {
        if (expected is null || (expected is string { Length: 0 } && actual is null))
        {
            return actual is null;
        }

        if (expected is string text)
        {
            if (text == "{x:Null}")
            {
                return actual is null;
            }

            if (actual is null)
            {
                return false;
            }

            if (actual is string actualText)
            {
                return string.Equals(actualText, text, StringComparison.Ordinal);
            }

            try
            {
                return Equals(actual, HeadlessXaml.ConvertTo(text, actual.GetType())) ||
                       string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidCastException or OverflowException)
            {
                return string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase);
            }
        }

        return Equals(actual, expected);
    }

    private sealed class StyledElement
    {
        private readonly WeakReference<FrameworkElement> _element;
        private readonly Dictionary<string, object?> _applied = new(StringComparer.Ordinal);
        private bool _evaluating;

        public StyledElement(FrameworkElement element, List<Style> chain)
        {
            _element = new WeakReference<FrameworkElement>(element);
            Setters = chain.SelectMany(style => style.Setters.OfType<Setter>()).Where(setter => setter.TargetName is null).ToList();
            Triggers = chain.SelectMany(style => style.Triggers).ToList();
            foreach (var trigger in Triggers)
            {
                switch (trigger)
                {
                    case Trigger { Property: { } property }:
                        WatchedProperties.Add(property.Name);
                        break;
                    case MultiTrigger multi:
                        foreach (var condition in multi.Conditions)
                        {
                            if (condition.Property is { } conditionProperty)
                            {
                                WatchedProperties.Add(conditionProperty.Name);
                            }
                        }

                        break;
                }
            }
        }

        public List<Setter> Setters { get; }
        public List<TriggerBase> Triggers { get; }
        public Dictionary<BindingBase, string> TriggerProperties { get; } = new(ReferenceEqualityComparer.Instance);
        public HashSet<string> WatchedProperties { get; } = new(StringComparer.Ordinal);

        public void Evaluate()
        {
            if (_evaluating || !_element.TryGetTarget(out var element))
            {
                return;
            }

            _evaluating = true;
            try
            {
                var desired = new Dictionary<string, (Setter Setter, bool FromTrigger)>(StringComparer.Ordinal);
                foreach (var setter in Setters)
                {
                    desired[setter.Property!.Name] = (setter, false);
                }

                foreach (var trigger in Triggers)
                {
                    if (!IsActive(element, trigger))
                    {
                        continue;
                    }

                    foreach (var setter in trigger.Setters.OfType<Setter>().Where(setter => setter.TargetName is null))
                    {
                        desired[setter.Property!.Name] = (setter, true);
                    }
                }

                foreach (var (property, (setter, _)) in desired)
                {
                    if (element.HasNamedValue(property) && !_applied.ContainsKey(property))
                    {
                        // A local value (markup, code or binding) outranks styles and triggers.
                        continue;
                    }

                    if (HeadlessBindings.GetBindings(element).ContainsKey(property))
                    {
                        continue;
                    }

                    var value = Resolve(setter, element);
                    if (_applied.TryGetValue(property, out var previous) && Equals(previous, value))
                    {
                        continue;
                    }

                    if (HeadlessXaml.TrySetProperty(element, property, value))
                    {
                        _applied[property] = value;
                        element.MarkStyleValue(property);
                    }
                }

                // Properties a trigger set earlier but no setter covers any more fall back to their default.
                foreach (var property in _applied.Keys.Where(property => !desired.ContainsKey(property)).ToList())
                {
                    _applied.Remove(property);
                    element.ClearStyleValue(property);
                }
            }
            finally
            {
                _evaluating = false;
            }
        }

        private bool IsActive(FrameworkElement element, TriggerBase trigger)
            => trigger switch
            {
                DataTrigger { Binding: { } binding } dataTrigger => ValueMatches(BoundValue(element, binding), dataTrigger.Value),
                Trigger { Property: { } property } propertyTrigger => ValueMatches(HeadlessXaml.TryGetProperty(element, property.Name), propertyTrigger.Value),
                MultiDataTrigger multi => multi.Conditions.All(condition => condition.Binding is { } binding && ValueMatches(BoundValue(element, binding), condition.Value)),
                MultiTrigger multiTrigger => multiTrigger.Conditions.All(condition => condition.Property is { } property && ValueMatches(HeadlessXaml.TryGetProperty(element, property.Name), condition.Value)),
                _ => false,
            };

        private object? BoundValue(FrameworkElement element, BindingBase binding)
            => TriggerProperties.TryGetValue(binding, out var property) ? element.GetNamedValue(property) : null;

        private static object? Resolve(Setter setter, FrameworkElement element)
        {
            var raw = setter.HeadlessRawValue;
            if (raw is not null && raw.StartsWith('{') && MarkupParser.TryParse(raw, out var node))
            {
                return MarkupParser.Evaluate(node, element);
            }

            return raw;
        }
    }
}
