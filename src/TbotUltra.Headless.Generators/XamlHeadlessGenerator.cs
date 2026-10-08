using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TbotUltra.Headless.Generators;

/// <summary>
/// Replaces the WPF markup compiler for the headless build. For every XAML file with an <c>x:Class</c> it
/// emits the partial-class half the markup compiler would have produced: a field per <c>x:Name</c> and an
/// <c>InitializeComponent</c> that rebuilds the logical element tree against the headless WPF shim —
/// literal attribute values, markup extensions (bindings, resources) as strings for the runtime to resolve,
/// keyed resources, and every event handler the markup wires up (including those inside templates and
/// style EventSetters), so a web UI can raise them. Templates are not expanded; their markup is attached
/// to the template object for a renderer to interpret.
/// </summary>
[Generator]
public sealed class XamlHeadlessGenerator : IIncrementalGenerator
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string Runtime = "global::System.Windows.Headless.HeadlessXaml";

    private static readonly HashSet<string> TemplateElements = new HashSet<string>(StringComparer.Ordinal)
    {
        "DataTemplate", "ControlTemplate", "ItemsPanelTemplate", "HierarchicalDataTemplate",
    };

    private static readonly HashSet<string> SkippedElements = new HashSet<string>(StringComparer.Ordinal)
    {
        "Setter", "Trigger", "DataTrigger", "MultiTrigger", "MultiDataTrigger", "EventTrigger", "Condition",
        "VisualStateManager.VisualStateGroups", "BeginStoryboard", "Storyboard", "DoubleAnimation", "ColorAnimation",
        "ResourceDictionary.MergedDictionaries", "ResourceDictionary",
    };

    private static readonly Dictionary<string, string> PresentationTypes = BuildPresentationTypes();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var xamlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => (file.Path, Text: file.GetText(cancellationToken)?.ToString()));

        context.RegisterSourceOutput(
            xamlFiles.Combine(context.CompilationProvider),
            static (spc, input) =>
            {
                var ((path, text), compilation) = input;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                try
                {
                    var source = Generate(path, text!, compilation);
                    if (source is not null)
                    {
                        var hint = System.IO.Path.GetFileNameWithoutExtension(path) + "_" +
                                   StableHash(path).ToString("x8") + ".g.cs";
                        spc.AddSource(hint, SourceText.From(source, Encoding.UTF8));
                    }
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("TBH001", "XAML headless generation failed",
                            "Could not generate headless code for '{0}': {1}", "Headless",
                            DiagnosticSeverity.Warning, isEnabledByDefault: true),
                        Location.None, path, ex.ToString().Replace("\r", " ").Replace("\n", " ")));
                }
            });
    }

    private static string? Generate(string path, string text, Compilation compilation)
    {
        var document = XDocument.Parse(text, LoadOptions.None);
        var root = document.Root;
        var className = root?.Attribute(XName.Get("Class", XamlNamespace))?.Value;
        if (root is null || string.IsNullOrWhiteSpace(className))
        {
            return null;
        }

        var classSymbol = compilation.GetTypeByMetadataName(className!);
        if (classSymbol is null)
        {
            return null;
        }

        var state = new EmitState(classSymbol);
        state.Body.AppendLine($"        {Runtime}.BeginComponent(this, {Literal(System.IO.Path.GetFileName(path))});");
        var rootName = root.Attribute(XName.Get("Name", XamlNamespace))?.Value;
        if (rootName is not null && ResolveTypeName(root) is { } rootType)
        {
            state.Fields.AppendLine($"    internal {rootType} {rootName};");
            state.Body.AppendLine($"        {rootName} = this;");
            state.Body.AppendLine($"        {Runtime}.SetLiteral(this, \"Name\", {Literal(rootName)});");
        }

        EmitAttributes(state, root, "this", CollectHandlers(state, root));
        foreach (var child in root.Elements())
        {
            Visit(state, child, "this", parentProperty: null);
        }

        state.Body.AppendLine($"        {Runtime}.EndComponent(this);");

        var lastDot = className!.LastIndexOf('.');
        var ns = lastDot > 0 ? className.Substring(0, lastDot) : null;
        var simpleName = lastDot > 0 ? className.Substring(lastDot + 1) : className;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>Generated by XamlHeadlessGenerator from " + System.IO.Path.GetFileName(path) + "</auto-generated>");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("#pragma warning disable");
        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
        }

        sb.AppendLine($"partial class {simpleName}");
        sb.AppendLine("{");
        sb.Append(state.Fields);
        sb.AppendLine("    private bool _headlessContentLoaded;");
        sb.AppendLine();
        sb.AppendLine("    public void InitializeComponent()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_headlessContentLoaded) return;");
        sb.AppendLine("        _headlessContentLoaded = true;");
        sb.Append(state.Body);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void Visit(EmitState state, XElement element, string parentVariable, string? parentProperty)
    {
        var localName = element.Name.LocalName;
        if (localName.Contains("."))
        {
            var property = localName.Substring(localName.LastIndexOf('.') + 1);
            if (SkippedElements.Contains(localName) || property is "Triggers" or "Template")
            {
                return;
            }

            foreach (var child in element.Elements())
            {
                Visit(state, child, parentVariable, property);
            }

            return;
        }

        if (SkippedElements.Contains(localName))
        {
            if (localName == "ResourceDictionary")
            {
                foreach (var child in element.Elements())
                {
                    Visit(state, child, parentVariable, parentProperty);
                }
            }

            return;
        }

        var key = element.Attribute(XName.Get("Key", XamlNamespace))?.Value;

        if (localName == "Style")
        {
            EmitStyle(state, element, parentVariable, parentProperty, key);
            return;
        }

        if (TemplateElements.Contains(localName))
        {
            EmitTemplate(state, element, parentVariable, parentProperty, key);
            return;
        }

        if (element.Name.NamespaceName.StartsWith("clr-namespace:System;", StringComparison.Ordinal) ||
            element.Name.NamespaceName == "clr-namespace:System")
        {
            state.Body.AppendLine(
                $"        {Runtime}.AddValue(this, {parentVariable}, {Literal(parentProperty)}, {Literal(key)}, {Literal(localName)}, {Literal(element.Value)});");
            return;
        }

        var typeName = ResolveTypeName(element);
        if (typeName is null)
        {
            return;
        }

        var name = element.Attribute(XName.Get("Name", XamlNamespace))?.Value
                   ?? (element.Name.NamespaceName == PresentationNamespace ? element.Attribute("Name")?.Value : null);
        string variable;
        string id;
        if (name is not null)
        {
            if (state.DeclaredFields.Add(name))
            {
                state.Fields.AppendLine($"    internal {typeName} {name};");
            }

            variable = name;
            id = name;
            state.Body.AppendLine($"        {name} = new {typeName}();");
            state.Body.AppendLine($"        {Runtime}.SetLiteral({name}, \"Name\", {Literal(name)});");
        }
        else
        {
            variable = $"__e{++state.Counter}";
            id = $"{localName}#{state.Counter}";
            state.Body.AppendLine($"        var {variable} = new {typeName}();");
        }

        EmitAttributes(state, element, variable, CollectHandlers(state, element));
        var text = element.HasElements ? null : element.Nodes().OfType<XText>().FirstOrDefault()?.Value?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            state.Body.AppendLine($"        {Runtime}.SetLiteral({variable}, \"__text\", {Literal(text!)});");
        }

        foreach (var child in element.Elements())
        {
            Visit(state, child, variable, parentProperty: null);
        }

        // Attach after the element is built so collection/content setters see the final element.
        state.Body.AppendLine(
            $"        {Runtime}.Attach(this, {variable}, {Literal(id)}, {parentVariable}, {Literal(parentProperty)}, {Literal(key)});");
    }

    private static void EmitStyle(EmitState state, XElement element, string parentVariable, string? parentProperty, string? key)
    {
        var variable = $"__s{++state.Counter}";
        state.Body.AppendLine(
            $"        var {variable} = {Runtime}.CreateStyle(this, {Literal(element.ToString(SaveOptions.DisableFormatting))});");
        foreach (var setter in element.Descendants().Where(e => e.Name.LocalName == "EventSetter"))
        {
            var eventName = setter.Attribute("Event")?.Value;
            var handlerName = setter.Attribute("Handler")?.Value;
            var method = handlerName is null ? null : FindHandler(state.ClassSymbol, handlerName);
            if (eventName is not null && method is not null)
            {
                state.Body.AppendLine(
                    $"        {Runtime}.AddStyleHandler(this, {variable}, {Literal(eventName.Split('.').Last())}, {HandlerLambda(method)});");
            }
        }

        state.Body.AppendLine(
            $"        {Runtime}.Attach(this, {variable}, {Literal("Style#" + state.Counter)}, {parentVariable}, {Literal(parentProperty ?? (key is null ? "Style" : null))}, {Literal(key)});");
    }

    private static void EmitTemplate(EmitState state, XElement element, string parentVariable, string? parentProperty, string? key)
    {
        var variable = $"__t{++state.Counter}";
        var localName = element.Name.LocalName;
        var typeName = PresentationTypes.TryGetValue(localName, out var ns) ? $"global::{ns}.{localName}" : "global::System.Windows.DataTemplate";
        state.Body.AppendLine($"        var {variable} = new {typeName}();");
        state.Body.AppendLine(
            $"        {Runtime}.SetTemplateMarkup({variable}, {Literal(element.ToString(SaveOptions.DisableFormatting))});");

        // Handlers inside a template run per generated item; keep them on the template so a renderer can
        // raise them with the item as the sender's DataContext.
        var index = 0;
        foreach (var descendant in element.Descendants())
        {
            index++;
            if (descendant.Name.LocalName.Contains("."))
            {
                continue;
            }

            foreach (var (eventName, method) in CollectHandlers(state, descendant))
            {
                state.Body.AppendLine(
                    $"        {Runtime}.AddTemplateHandler({variable}, {index}, {Literal(eventName)}, {HandlerLambda(method)});");
            }
        }

        state.Body.AppendLine(
            $"        {Runtime}.Attach(this, {variable}, {Literal(localName + "#" + state.Counter)}, {parentVariable}, {Literal(parentProperty)}, {Literal(key)});");
    }

    private static void EmitAttributes(
        EmitState state,
        XElement element,
        string variable,
        List<(string EventName, IMethodSymbol Method)> handlers)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            var attributeName = attribute.Name.LocalName;
            if (attribute.Name.NamespaceName.Length > 0)
            {
                continue;
            }

            if (handlers.Any(handler => handler.EventName == attributeName.Split('.').Last() && attribute.Value == handler.Method.Name))
            {
                continue;
            }

            var value = attribute.Value;
            var method = value.StartsWith("{", StringComparison.Ordinal) && !value.StartsWith("{}", StringComparison.Ordinal)
                ? "SetMarkup"
                : "SetLiteral";
            if (value.StartsWith("{}", StringComparison.Ordinal))
            {
                value = value.Substring(2);
            }

            state.Body.AppendLine($"        {Runtime}.{method}({variable}, {Literal(attributeName)}, {Literal(value)});");
        }

        foreach (var (eventName, method) in handlers)
        {
            state.Body.AppendLine(
                $"        {Runtime}.AddHandler({variable}, {Literal(eventName)}, {HandlerLambda(method)});");
        }
    }

    private static List<(string EventName, IMethodSymbol Method)> CollectHandlers(EmitState state, XElement element)
    {
        var handlers = new List<(string, IMethodSymbol)>();
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || attribute.Name.NamespaceName.Length > 0)
            {
                continue;
            }

            var value = attribute.Value;
            if (value.Length == 0 || !IsIdentifier(value) || IsKnownLiteralProperty(attribute.Name.LocalName))
            {
                continue;
            }

            var method = FindHandler(state.ClassSymbol, value);
            if (method is not null)
            {
                handlers.Add((attribute.Name.LocalName.Split('.').Last(), method));
            }
        }

        return handlers;
    }

    // Attributes that take identifier-like literals which could collide with a method name.
    private static bool IsKnownLiteralProperty(string name)
        => name is "Name" or "Tag" or "Content" or "Header" or "Text" or "Title" or "ToolTip" or "GroupName" or
            "DisplayMemberPath" or "SelectedValuePath" or "SortMemberPath" or "Uid" or "Style" or "TargetType";

    private static IMethodSymbol? FindHandler(INamedTypeSymbol classSymbol, string name)
    {
        for (var type = classSymbol; type is not null; type = type.BaseType)
        {
            var method = type.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(m => m.Parameters.Length == 2 && !m.IsStatic);
            if (method is not null)
            {
                return method;
            }
        }

        return null;
    }

    private static string HandlerLambda(IMethodSymbol method)
    {
        var argsType = method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var call = $"{method.Name}(__s, {Runtime}.Args<{argsType}>(__a))";
        return method.ReturnsVoid
            ? $"new global::System.Windows.HeadlessHandler((object __s, global::System.EventArgs __a) => {call})"
            : $"new global::System.Windows.HeadlessHandler((object __s, global::System.EventArgs __a) => {{ _ = {call}; }})";
    }

    private static string? ResolveTypeName(XElement element)
    {
        var ns = element.Name.NamespaceName;
        var localName = element.Name.LocalName;
        if (ns.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var clrNamespace = ns.Substring("clr-namespace:".Length).Split(';')[0];
            return $"global::{clrNamespace}.{localName}";
        }

        if (ns == PresentationNamespace)
        {
            return PresentationTypes.TryGetValue(localName, out var clrNamespace)
                ? $"global::{clrNamespace}.{localName}"
                : $"global::System.Windows.Controls.{localName}";
        }

        return null;
    }

    private static Dictionary<string, string> BuildPresentationTypes()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string ns, params string[] names)
        {
            foreach (var name in names)
            {
                map[name] = ns;
            }
        }

        Add("System.Windows", "Window", "FrameworkElement", "UIElement", "ResourceDictionary", "DataTemplate",
            "ControlTemplate", "ItemsPanelTemplate", "HierarchicalDataTemplate", "Style", "Setter", "EventSetter",
            "Trigger", "DataTrigger", "EventTrigger", "Thickness");
        Add("System.Windows.Shapes", "Ellipse", "Rectangle", "Path", "Line", "Polygon", "Polyline");
        Add("System.Windows.Media", "RotateTransform", "ScaleTransform", "TranslateTransform", "TransformGroup",
            "SolidColorBrush", "LinearGradientBrush", "GradientStop", "Color", "FontFamily", "PathGeometry",
            "RectangleGeometry", "EllipseGeometry", "PathFigure", "LineSegment", "BezierSegment", "PolyLineSegment");
        Add("System.Windows.Media.Effects", "DropShadowEffect", "BlurEffect");
        Add("System.Windows.Media.Animation", "BeginStoryboard", "Storyboard", "DoubleAnimation", "ColorAnimation");
        Add("System.Windows.Documents", "Run", "Hyperlink", "LineBreak", "Bold", "Italic", "Span", "Underline");
        Add("System.Windows.Data", "CollectionViewSource", "Binding", "MultiBinding", "RelativeSource");
        Add("System.Windows.Controls.Primitives", "Popup", "ToggleButton", "UniformGrid", "ScrollBar", "Thumb",
            "StatusBar", "TabPanel");
        Add("System.ComponentModel", "SortDescription");
        return map;
    }

    private static bool IsIdentifier(string value)
    {
        if (!(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        return value.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private static string Literal(string? value)
        => value is null ? "null" : "@\"" + value.Replace("\"", "\"\"") + "\"";

    private static uint StableHash(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in text)
            {
                hash = (hash ^ ch) * 16777619u;
            }

            return hash;
        }
    }

    private sealed class EmitState
    {
        public EmitState(INamedTypeSymbol classSymbol)
        {
            ClassSymbol = classSymbol;
        }

        public INamedTypeSymbol ClassSymbol { get; }
        public StringBuilder Fields { get; } = new StringBuilder();
        public StringBuilder Body { get; } = new StringBuilder();
        public HashSet<string> DeclaredFields { get; } = new HashSet<string>(StringComparer.Ordinal);
        public int Counter { get; set; }
    }
}
