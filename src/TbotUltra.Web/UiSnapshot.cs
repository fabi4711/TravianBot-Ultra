using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Headless;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TbotUltra.Web;

/// <summary>
/// Stable numeric ids for headless objects, so the browser can address elements across snapshots.
/// </summary>
public sealed class UiIds
{
    private readonly ConditionalWeakTable<object, StrongBox<int>> _ids = new();
    private readonly Dictionary<int, WeakReference<object>> _objects = new();
    private int _next;

    public int Get(object target)
    {
        var box = _ids.GetValue(target, _ => new StrongBox<int>(Interlocked.Increment(ref _next)));
        lock (_objects)
        {
            _objects[box.Value] = new WeakReference<object>(target);
        }

        return box.Value;
    }

    public object? Find(int id)
    {
        lock (_objects)
        {
            return _objects.TryGetValue(id, out var reference) && reference.TryGetTarget(out var target) ? target : null;
        }
    }
}

/// <summary>
/// Serializes the open windows' logical trees into a compact JSON shape the browser renderer understands.
/// Must run on the UI dispatcher. Collapsed elements and unselected tab pages are left out, which keeps
/// snapshots small; templates are expanded per item through <see cref="HeadlessTemplates"/>.
/// </summary>
public sealed class UiSnapshot
{
    private const int MaxRows = 500;
    private readonly UiIds _ids;

    public UiSnapshot(UiIds ids)
    {
        _ids = ids;
    }

    public JsonObject Build()
    {
        var windows = new JsonArray();
        foreach (var window in HeadlessWindows.Open)
        {
            if (window.Visibility == Visibility.Collapsed && !window.IsShown)
            {
                continue;
            }

            windows.Add(new JsonObject
            {
                ["i"] = _ids.Get(window),
                ["title"] = window.Title,
                ["modal"] = window.IsModal,
                ["main"] = ReferenceEquals(window, Application.Current?.MainWindow),
                ["w"] = Number(window.Width),
                ["h"] = Number(window.Height),
                ["bg"] = Css(window.Background),
                ["root"] = Node(window.Content),
            });
        }

        var messages = new JsonArray();
        foreach (var message in HeadlessDialogs.Open)
        {
            messages.Add(new JsonObject
            {
                ["id"] = message.Id.ToString(),
                ["caption"] = message.Caption,
                ["text"] = message.Text,
                ["buttons"] = message.Buttons.ToString(),
                ["icon"] = message.Icon.ToString(),
            });
        }

        return new JsonObject { ["windows"] = windows, ["messages"] = messages };
    }

    private JsonNode? Node(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return new JsonObject { ["k"] = "text", ["t"] = text };
            case UIElement { Visibility: Visibility.Collapsed }:
                return null;
            case UIElement element:
                return Element(element);
            default:
                return new JsonObject { ["k"] = "text", ["t"] = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty };
        }
    }

    private JsonObject Element(UIElement element)
    {
        var node = new JsonObject { ["i"] = _ids.Get(element) };
        if (element.Visibility == Visibility.Hidden)
        {
            node["hidden"] = true;
        }

        if (!element.IsEnabled)
        {
            node["off"] = true;
        }

        if (element.Opacity < 1)
        {
            node["op"] = Math.Round(element.Opacity, 2);
        }

        if (element is FrameworkElement framework)
        {
            Layout(node, framework);
        }

        switch (element)
        {
            case Window:
                break;
            case TextBlock textBlock:
                node["k"] = "tb";
                TextStyle(node, textBlock.Foreground, textBlock.FontWeight, textBlock.FontSize, textBlock.FontStyle);
                node["bg"] = Css(textBlock.Background);
                if (textBlock.Inlines.Count > 0)
                {
                    node["inl"] = Inlines(textBlock.Inlines);
                }
                else
                {
                    node["t"] = textBlock.Text;
                }

                if (textBlock.TextWrapping != TextWrapping.NoWrap)
                {
                    node["wrap"] = true;
                }

                if (textBlock.TextAlignment != TextAlignment.Left)
                {
                    node["ta"] = textBlock.TextAlignment.ToString().ToLowerInvariant();
                }

                break;
            case TextBox textBox:
                node["k"] = "input";
                node["val"] = textBox.Text;
                node["ro"] = textBox.IsReadOnly;
                node["multi"] = textBox.AcceptsReturn || textBox.TextWrapping == TextWrapping.Wrap;
                ControlStyle(node, textBox);
                break;
            case PasswordBox passwordBox:
                node["k"] = "password";
                node["set"] = passwordBox.Password.Length > 0;
                break;
            case RadioButton radio:
                node["k"] = "radio";
                node["chk"] = radio.IsChecked;
                Content(node, radio.Content);
                ControlStyle(node, radio);
                break;
            case CheckBox checkBox:
                node["k"] = "check";
                node["chk"] = checkBox.IsChecked;
                Content(node, checkBox.Content);
                ControlStyle(node, checkBox);
                break;
            case ToggleButton toggle:
                node["k"] = "toggle";
                node["chk"] = toggle.IsChecked;
                Content(node, toggle.Content);
                ControlStyle(node, toggle);
                break;
            case ButtonBase button:
                node["k"] = "button";
                Content(node, button.Content);
                ControlStyle(node, button);
                if (button.Style?.HeadlessKey is { } styleKey)
                {
                    node["style"] = styleKey;
                }

                break;
            case ComboBox comboBox:
                node["k"] = "combo";
                node["opts"] = new JsonArray(comboBox.Items.Cast<object?>().Select(item => (JsonNode?)ItemText(comboBox, item)).ToArray());
                node["sel"] = comboBox.SelectedIndex;
                node["edit"] = comboBox.IsEditable;
                if (comboBox.IsEditable)
                {
                    node["val"] = comboBox.Text;
                }

                ControlStyle(node, comboBox);
                break;
            case TabControl tabControl:
                node["k"] = "tabs";
                var tabs = new JsonArray();
                foreach (var tab in tabControl.Items.OfType<TabItem>())
                {
                    if (tab.Visibility == Visibility.Collapsed)
                    {
                        continue;
                    }

                    tabs.Add(new JsonObject
                    {
                        ["i"] = _ids.Get(tab),
                        ["t"] = TextOf(tab.Header),
                        ["off"] = !tab.IsEnabled,
                        ["fg"] = Css(tab.Foreground),
                    });
                }

                node["tabs"] = tabs;
                if (tabControl.ItemContainerStyle?.HeadlessSetters.GetValueOrDefault("Visibility") == "Collapsed" || tabControl.Template is not null)
                {
                    node["nobar"] = true;
                }

                if (tabControl.SelectedItem is TabItem selectedTab)
                {
                    node["sel"] = _ids.Get(selectedTab);
                    node["c"] = Node(selectedTab.Content);
                }
                else if (tabControl.SelectedItem is not null)
                {
                    node["c"] = Node(tabControl.SelectedContent);
                }

                break;
            case DataGrid dataGrid:
                DataGridNode(node, dataGrid);
                break;
            case ListBox listBox:
                node["k"] = "list";
                node["items"] = Items(listBox);
                node["sel"] = new JsonArray(listBox.SelectedItems.Cast<object?>().Select(item => (JsonNode)listBox.Items.IndexOf(item)).ToArray());
                node["multi"] = listBox.SelectionMode != SelectionMode.Single;
                break;
            case ItemsControl itemsControl:
                node["k"] = "items";
                node["c"] = Items(itemsControl);
                node["o"] = PanelOrientation(itemsControl);
                break;
            case Expander expander:
                node["k"] = "expander";
                node["hdr"] = TextOf(expander.Header);
                node["open"] = expander.IsExpanded;
                node["c"] = expander.IsExpanded ? Node(expander.Content) : null;
                break;
            case HeaderedContentControl headered:
                node["k"] = "box";
                node["hdr"] = TextOf(headered.Header);
                node["c"] = new JsonArray(Node(headered.Content));
                break;
            case ScrollViewer scrollViewer:
                node["k"] = "scroll";
                node["c"] = new JsonArray(Node(scrollViewer.Content));
                node["hs"] = scrollViewer.HorizontalScrollBarVisibility is ScrollBarVisibility.Auto or ScrollBarVisibility.Visible;
                break;
            case ContentControl contentControl:
                node["k"] = "box";
                node["bg"] = Css(contentControl.Background);
                node["c"] = new JsonArray(Node(contentControl.Content));
                ControlStyle(node, contentControl);
                break;
            case Border border:
                node["k"] = "border";
                node["bg"] = Css(border.Background);
                node["bb"] = Css(border.BorderBrush);
                node["bt"] = Thickness(border.BorderThickness);
                node["cr"] = border.CornerRadius.TopLeft > 0 ? border.CornerRadius.TopLeft : null;
                node["pad"] = Thickness(border.Padding);
                node["c"] = new JsonArray(Node(border.Child));
                break;
            case Decorator decorator:
                node["k"] = "box";
                node["c"] = new JsonArray(Node(decorator.Child));
                break;
            case Grid grid:
                node["k"] = "grid";
                node["bg"] = Css(grid.Background);
                node["cols"] = new JsonArray(grid.ColumnDefinitions.Select(column => (JsonNode)GridTrack(column.Width, column.MinWidth, column.MaxWidth)).ToArray());
                node["rows"] = new JsonArray(grid.RowDefinitions.Select(row => (JsonNode)GridTrack(row.Height, row.MinHeight, row.MaxHeight)).ToArray());
                node["c"] = Children(grid.Children);
                break;
            case StackPanel stackPanel:
                node["k"] = "stack";
                node["o"] = stackPanel.Orientation == Orientation.Horizontal ? "h" : "v";
                node["bg"] = Css(stackPanel.Background);
                node["c"] = Children(stackPanel.Children);
                break;
            case WrapPanel wrapPanel:
                node["k"] = "wrap";
                node["o"] = wrapPanel.Orientation == Orientation.Horizontal ? "h" : "v";
                node["bg"] = Css(wrapPanel.Background);
                node["c"] = Children(wrapPanel.Children);
                break;
            case UniformGrid uniformGrid:
                node["k"] = "ugrid";
                node["n"] = uniformGrid.Columns > 0 ? uniformGrid.Columns : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(uniformGrid.Children.Count)));
                node["c"] = Children(uniformGrid.Children);
                break;
            case DockPanel dockPanel:
                node["k"] = "dock";
                node["fill"] = dockPanel.LastChildFill;
                node["bg"] = Css(dockPanel.Background);
                node["c"] = Children(dockPanel.Children, child => new JsonObject { ["dock"] = DockPanel.GetDock(child).ToString().ToLowerInvariant() });
                break;
            case Canvas canvas:
                node["k"] = "canvas";
                node["bg"] = Css(canvas.Background);
                node["c"] = Children(canvas.Children, child => new JsonObject
                {
                    ["x"] = Number(Canvas.GetLeft(child)),
                    ["y"] = Number(Canvas.GetTop(child)),
                });
                break;
            case Panel panel:
                node["k"] = "stack";
                node["o"] = "v";
                node["c"] = Children(panel.Children);
                break;
            case ProgressBar progressBar:
                node["k"] = "progress";
                node["val"] = progressBar.Value;
                node["min"] = progressBar.Minimum;
                node["max"] = progressBar.Maximum;
                node["ind"] = progressBar.IsIndeterminate;
                node["fg"] = Css(progressBar.Foreground);
                break;
            case Shape shape:
                node["k"] = "shape";
                node["round"] = shape is Ellipse;
                node["bg"] = Css(shape.Fill);
                node["bb"] = Css(shape.Stroke);
                break;
            case Image image:
                node["k"] = "img";
                node["src"] = (image.Source as System.Windows.Media.Imaging.BitmapImage)?.UriSource is { } uri
                    ? "assets/" + Uri.EscapeDataString(System.IO.Path.GetFileName(uri.OriginalString))
                    : null;
                break;
            case Separator:
                node["k"] = "sep";
                break;
            case Popup popup:
                node["k"] = popup.IsOpen ? "box" : "none";
                node["c"] = popup.IsOpen ? new JsonArray(Node(popup.Child)) : null;
                break;
            case ContentPresenter presenter:
                node["k"] = "box";
                node["c"] = new JsonArray(Node(presenter.Content));
                break;
            default:
                node["k"] = "none";
                break;
        }

        return node;
    }

    private void DataGridNode(JsonObject node, DataGrid dataGrid)
    {
        node["k"] = "table";
        var columns = dataGrid.Columns.Where(column => column.Visibility == Visibility.Visible).ToList();
        node["cols"] = new JsonArray(columns.Select(column => (JsonNode)new JsonObject
        {
            ["h"] = TextOf(column.Header),
            ["w"] = column.Width.IsAbsolute ? column.Width.Value : null,
            ["star"] = column.Width.IsStar ? column.Width.Value : null,
        }).ToArray());

        var items = dataGrid.Items.Cast<object?>().Take(MaxRows).ToList();
        var rows = new JsonArray();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var cells = new JsonArray();
            foreach (var column in columns)
            {
                cells.Add(Cell(dataGrid, column, item));
            }

            rows.Add(new JsonObject { ["c"] = cells });
        }

        foreach (var column in columns.OfType<DataGridTemplateColumn>())
        {
            HeadlessTemplates.Prune(column, items.Where(item => item is not null)!);
        }

        node["rows"] = rows;
        node["total"] = dataGrid.Items.Count;
        node["sel"] = new JsonArray(dataGrid.SelectedItems.Cast<object?>().Select(item => (JsonNode)dataGrid.Items.IndexOf(item)).ToArray());
        node["multi"] = dataGrid.SelectionMode == DataGridSelectionMode.Extended;
        node["edit"] = !dataGrid.IsReadOnly;
    }

    private JsonNode? Cell(DataGrid dataGrid, DataGridColumn column, object? item)
    {
        switch (column)
        {
            case DataGridCheckBoxColumn checkColumn when checkColumn.Binding is { } checkBinding:
                return new JsonObject
                {
                    ["k"] = "cellcheck",
                    ["chk"] = HeadlessBindings.Evaluate(checkBinding, item) as bool?,
                    ["ro"] = dataGrid.IsReadOnly || column.IsReadOnly,
                };
            case DataGridBoundColumn boundColumn when boundColumn.Binding is { } binding:
                return Convert.ToString(HeadlessBindings.Evaluate(binding, item), CultureInfo.CurrentCulture) ?? string.Empty;
            case DataGridTemplateColumn templateColumn when item is not null:
                var cell = HeadlessTemplates.Realize(column, item, templateColumn.CellTemplate, item);
                return Node(cell);
            default:
                return string.Empty;
        }
    }

    private JsonArray Items(ItemsControl itemsControl)
    {
        var result = new JsonArray();
        var keys = new List<object>();
        foreach (var item in itemsControl.Items.Cast<object?>().Take(MaxRows))
        {
            if (item is UIElement element)
            {
                result.Add(Node(element));
                continue;
            }

            if (item is null)
            {
                continue;
            }

            keys.Add(item);
            var template = itemsControl.ItemTemplate ?? FindDataTemplate(itemsControl, item);
            if (template is null)
            {
                result.Add(new JsonObject { ["k"] = "text", ["t"] = HeadlessCollections.DisplayText(item, itemsControl.DisplayMemberPath) });
                continue;
            }

            var realized = HeadlessTemplates.Realize(itemsControl, item, template, item);
            result.Add(Node(realized));
        }

        HeadlessTemplates.Prune(itemsControl, keys);
        return result;
    }

    private static DataTemplate? FindDataTemplate(FrameworkElement scope, object item)
        => scope.TryFindResource(item.GetType()) as DataTemplate;

    private string ItemText(ItemsControl owner, object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (item is not UIElement && owner.ItemTemplate is { } template)
        {
            return TextOf(HeadlessTemplates.Realize(owner, item, template, item));
        }

        return item is UIElement element ? TextOf(element) : HeadlessCollections.DisplayText(item, owner.DisplayMemberPath);
    }

    private JsonArray Children(IEnumerable<UIElement> children, Func<UIElement, JsonObject>? extras = null)
    {
        var result = new JsonArray();
        foreach (var child in children)
        {
            if (Node(child) is JsonObject node)
            {
                if (extras is not null)
                {
                    foreach (var (key, value) in extras(child))
                    {
                        node[key] = value?.DeepClone();
                    }
                }

                result.Add(node);
            }
        }

        return result;
    }

    private void Content(JsonObject node, object? content)
    {
        if (content is UIElement element)
        {
            node["c"] = new JsonArray(Node(element));
        }
        else
        {
            node["t"] = Convert.ToString(content, CultureInfo.CurrentCulture) ?? string.Empty;
        }
    }

    private static void Layout(JsonObject node, FrameworkElement element)
    {
        if (element.HeadlessParent is Grid)
        {
            node["gr"] = Grid.GetRow(element);
            node["gc"] = Grid.GetColumn(element);
            var rowSpan = Grid.GetRowSpan(element);
            var columnSpan = Grid.GetColumnSpan(element);
            if (rowSpan > 1)
            {
                node["grs"] = rowSpan;
            }

            if (columnSpan > 1)
            {
                node["gcs"] = columnSpan;
            }
        }

        node["m"] = Thickness(element.Margin);
        node["w"] = Number(element.Width);
        node["h"] = Number(element.Height);
        if (element.MinWidth > 0)
        {
            node["minw"] = element.MinWidth;
        }

        if (!double.IsInfinity(element.MaxWidth))
        {
            node["maxw"] = element.MaxWidth;
        }

        if (element.MinHeight > 0)
        {
            node["minh"] = element.MinHeight;
        }

        if (!double.IsInfinity(element.MaxHeight))
        {
            node["maxh"] = element.MaxHeight;
        }

        if (element.HorizontalAlignment != HorizontalAlignment.Stretch)
        {
            node["ha"] = element.HorizontalAlignment.ToString()[0..1].ToLowerInvariant();
        }

        if (element.VerticalAlignment != VerticalAlignment.Stretch)
        {
            node["va"] = element.VerticalAlignment.ToString()[0..1].ToLowerInvariant();
        }

        if (element.ToolTip is { } toolTip)
        {
            node["tip"] = TextOf(toolTip);
        }

        if (!string.IsNullOrEmpty(element.Name))
        {
            node["n"] = element.Name;
        }
    }

    private static void ControlStyle(JsonObject node, Control control)
    {
        TextStyle(node, control.Foreground, control.FontWeight, control.FontSize, control.FontStyle);
        node["bg"] ??= Css(control.Background);
        node["bb"] ??= Css(control.BorderBrush);
        node["pad"] ??= Thickness(control.Padding);
    }

    private static void TextStyle(JsonObject node, Brush? foreground, FontWeight weight, double size, FontStyle style)
    {
        node["fg"] = Css(foreground);
        if (weight != FontWeight.Normal)
        {
            node["fw"] = weight == FontWeight.Bold ? 700 : 600;
        }

        if (Math.Abs(size - 12) > 0.1)
        {
            node["fs"] = size;
        }

        if (style == FontStyle.Italic)
        {
            node["it"] = true;
        }
    }

    private static JsonArray Inlines(IEnumerable<Inline> inlines)
    {
        var result = new JsonArray();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    result.Add(new JsonObject
                    {
                        ["t"] = run.Text,
                        ["fg"] = Css(run.Foreground),
                        ["fw"] = run.FontWeight == FontWeight.Normal ? null : 600,
                    });
                    break;
                case LineBreak:
                    result.Add(new JsonObject { ["br"] = true });
                    break;
                case Hyperlink hyperlink:
                    result.Add(new JsonObject { ["t"] = string.Concat(hyperlink.Inlines.OfType<Run>().Select(run => run.Text)), ["href"] = hyperlink.NavigateUri?.ToString() });
                    break;
                case Span span:
                    foreach (var child in Inlines(span.Inlines))
                    {
                        if (child is JsonObject childObject && span is Bold)
                        {
                            childObject["fw"] = 600;
                        }

                        result.Add(child?.DeepClone());
                    }

                    break;
            }
        }

        return result;
    }

    /// <summary>Plain text of a header/tooltip/content, which may be a string or a small element tree.</summary>
    public static string TextOf(object? value)
        => value switch
        {
            null => string.Empty,
            string text => text,
            TextBlock textBlock => textBlock.Text,
            ContentControl { Content: var content } => TextOf(content),
            DependencyObject element => string.Join(" ", HeadlessXaml.Descendants(element).OfType<TextBlock>().Select(textBlock => textBlock.Text).Where(text => text.Length > 0)),
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
        };

    private static string PanelOrientation(ItemsControl itemsControl)
    {
        var panel = itemsControl.ItemsPanel?.VisualTree?.Type;
        if (panel is not null && (typeof(WrapPanel).IsAssignableFrom(panel) || typeof(UniformGrid).IsAssignableFrom(panel)))
        {
            return "w";
        }

        var markup = itemsControl.ItemsPanel is { } template ? HeadlessXaml.GetTemplateInfo(template)?.Markup : null;
        if (markup is not null && (markup.Contains("WrapPanel", StringComparison.Ordinal) || markup.Contains("UniformGrid", StringComparison.Ordinal)))
        {
            return "w";
        }

        return markup is not null && markup.Contains("Orientation=\"Horizontal\"", StringComparison.Ordinal) ? "h" : "v";
    }

    private static string GridTrack(GridLength length, double min, double max)
    {
        var track = length.GridUnitType switch
        {
            GridUnitType.Auto => "auto",
            GridUnitType.Star => $"minmax(0,{length.Value.ToString(CultureInfo.InvariantCulture)}fr)",
            _ => $"{length.Value.ToString(CultureInfo.InvariantCulture)}px",
        };
        if (min > 0 && length.GridUnitType == GridUnitType.Star)
        {
            track = $"minmax({min.ToString(CultureInfo.InvariantCulture)}px,{length.Value.ToString(CultureInfo.InvariantCulture)}fr)";
        }

        return track;
    }

    private static JsonArray? Thickness(Thickness thickness)
        => thickness == default
            ? null
            : new JsonArray(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);

    private static double? Number(double value) => double.IsNaN(value) || double.IsInfinity(value) ? null : value;

    private static string? Css(Brush? brush) => brush?.ToCss();
}
