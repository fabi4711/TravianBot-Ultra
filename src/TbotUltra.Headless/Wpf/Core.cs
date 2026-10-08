using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Headless;
using System.Windows.Threading;

namespace System.Windows;

public enum Visibility
{
    Visible,
    Hidden,
    Collapsed,
}

public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
    Stretch,
}

public enum VerticalAlignment
{
    Top,
    Center,
    Bottom,
    Stretch,
}

public enum TextAlignment
{
    Left,
    Right,
    Center,
    Justify,
}

public enum TextWrapping
{
    WrapWithOverflow,
    NoWrap,
    Wrap,
}

public enum TextTrimming
{
    None,
    CharacterEllipsis,
    WordEllipsis,
}

public enum FontWeight
{
    Normal,
    SemiBold,
    Bold,
}

public static class FontWeights
{
    public static FontWeight Normal => FontWeight.Normal;
    public static FontWeight SemiBold => FontWeight.SemiBold;
    public static FontWeight Bold => FontWeight.Bold;
}

public enum FontStyle
{
    Normal,
    Italic,
}

public static class FontStyles
{
    public static FontStyle Normal => FontStyle.Normal;
    public static FontStyle Italic => FontStyle.Italic;
}

public enum WindowState
{
    Normal,
    Minimized,
    Maximized,
}

public enum WindowStartupLocation
{
    Manual,
    CenterScreen,
    CenterOwner,
}

public enum SizeToContent
{
    Manual,
    Width,
    Height,
    WidthAndHeight,
}

public enum ResizeMode
{
    NoResize,
    CanMinimize,
    CanResize,
    CanResizeWithGrip,
}

public enum WindowStyle
{
    None,
    SingleBorderWindow,
    ThreeDBorderWindow,
    ToolWindow,
}

public enum ShutdownMode
{
    OnLastWindowClose,
    OnMainWindowClose,
    OnExplicitShutdown,
}

public enum MessageBoxButton
{
    OK = 0,
    OKCancel = 1,
    YesNoCancel = 3,
    YesNo = 4,
}

public enum MessageBoxResult
{
    None = 0,
    OK = 1,
    Cancel = 2,
    Yes = 6,
    No = 7,
}

public enum MessageBoxImage
{
    None = 0,
    Hand = 16,
    Stop = 16,
    Error = 16,
    Question = 32,
    Exclamation = 48,
    Warning = 48,
    Asterisk = 64,
    Information = 64,
}

public readonly record struct Thickness(double Left, double Top, double Right, double Bottom)
{
    public Thickness(double uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }
}

public readonly record struct CornerRadius(double TopLeft, double TopRight, double BottomRight, double BottomLeft)
{
    public CornerRadius(double uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }
}

public struct Size
{
    public Size(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; set; }
    public double Height { get; set; }
    public static Size Empty => new(0, 0);
    public bool IsEmpty => Width <= 0 && Height <= 0;
}

public struct Point
{
    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; set; }
    public double Y { get; set; }

    public static Vector operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
}

public struct Vector
{
    public Vector(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Length => Math.Sqrt((X * X) + (Y * Y));
}

public struct Rect
{
    public Rect(Point location, Size size)
        : this(location.X, location.Y, size.Width, size.Height)
    {
    }

    public bool Contains(Point point) => point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;

    public Rect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public enum GridUnitType
{
    Auto,
    Pixel,
    Star,
}

public readonly record struct GridLength(double Value, GridUnitType GridUnitType)
{
    public GridLength(double pixels)
        : this(pixels, GridUnitType.Pixel)
    {
    }

    public static GridLength Auto => new(1, GridUnitType.Auto);
    public bool IsAuto => GridUnitType == GridUnitType.Auto;
    public bool IsStar => GridUnitType == GridUnitType.Star;
    public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;
}

public static class SystemParameters
{
    public static double WorkArea_Width => 1920;
    public static Rect WorkArea => new(0, 0, 1920, 1040);
    public static double PrimaryScreenWidth => 1920;
    public static double PrimaryScreenHeight => 1080;
    public static double MinimumHorizontalDragDistance => 4;
    public static double MinimumVerticalDragDistance => 4;
}

public static class DependencyPropertyHelper
{
}

public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);

public delegate object CoerceValueCallback(DependencyObject d, object baseValue);

public delegate bool ValidateValueCallback(object value);

public readonly struct DependencyPropertyChangedEventArgs
{
    public DependencyPropertyChangedEventArgs(DependencyProperty property, object? oldValue, object? newValue)
    {
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public DependencyProperty Property { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
}

public delegate void DependencyPropertyChangedEventHandler(object sender, DependencyPropertyChangedEventArgs e);

public class PropertyMetadata
{
    public PropertyMetadata()
    {
    }

    public PropertyMetadata(object? defaultValue)
    {
        DefaultValue = defaultValue;
    }

    public PropertyMetadata(PropertyChangedCallback? propertyChangedCallback)
    {
        PropertyChangedCallback = propertyChangedCallback;
    }

    public PropertyMetadata(object? defaultValue, PropertyChangedCallback? propertyChangedCallback)
    {
        DefaultValue = defaultValue;
        PropertyChangedCallback = propertyChangedCallback;
    }

    public PropertyMetadata(object? defaultValue, PropertyChangedCallback? propertyChangedCallback, CoerceValueCallback? coerceValueCallback)
        : this(defaultValue, propertyChangedCallback)
    {
        CoerceValueCallback = coerceValueCallback;
    }

    public object? DefaultValue { get; set; }
    public PropertyChangedCallback? PropertyChangedCallback { get; set; }
    public CoerceValueCallback? CoerceValueCallback { get; set; }
}

[Flags]
public enum FrameworkPropertyMetadataOptions
{
    None = 0,
    AffectsMeasure = 1,
    AffectsArrange = 2,
    AffectsParentMeasure = 4,
    AffectsParentArrange = 8,
    AffectsRender = 16,
    Inherits = 32,
    BindsTwoWayByDefault = 256,
}

public class FrameworkPropertyMetadata : PropertyMetadata
{
    public FrameworkPropertyMetadata()
    {
    }

    public FrameworkPropertyMetadata(object? defaultValue)
        : base(defaultValue)
    {
    }

    public FrameworkPropertyMetadata(PropertyChangedCallback? propertyChangedCallback)
        : base(propertyChangedCallback)
    {
    }

    public FrameworkPropertyMetadata(object? defaultValue, PropertyChangedCallback? propertyChangedCallback)
        : base(defaultValue, propertyChangedCallback)
    {
    }

    public FrameworkPropertyMetadata(object? defaultValue, FrameworkPropertyMetadataOptions flags)
        : base(defaultValue)
    {
    }

    public FrameworkPropertyMetadata(object? defaultValue, FrameworkPropertyMetadataOptions flags, PropertyChangedCallback? propertyChangedCallback)
        : base(defaultValue, propertyChangedCallback)
    {
    }
}

public sealed class DependencyProperty
{
    public static readonly object UnsetValue = new();

    private DependencyProperty(string name, Type propertyType, Type ownerType, PropertyMetadata metadata)
    {
        Name = name;
        PropertyType = propertyType;
        OwnerType = ownerType;
        DefaultMetadata = metadata;
    }

    public string Name { get; }
    public Type PropertyType { get; }
    public Type OwnerType { get; }
    public PropertyMetadata DefaultMetadata { get; }

    public static DependencyProperty Register(string name, Type propertyType, Type ownerType)
        => Register(name, propertyType, ownerType, null);

    public static DependencyProperty Register(string name, Type propertyType, Type ownerType, PropertyMetadata? typeMetadata)
        => new(name, propertyType, ownerType, Normalize(typeMetadata, propertyType));

    public static DependencyProperty Register(
        string name,
        Type propertyType,
        Type ownerType,
        PropertyMetadata? typeMetadata,
        ValidateValueCallback? validateValueCallback)
        => Register(name, propertyType, ownerType, typeMetadata);

    public static DependencyProperty RegisterAttached(string name, Type propertyType, Type ownerType)
        => Register(name, propertyType, ownerType, null);

    public static DependencyProperty RegisterAttached(string name, Type propertyType, Type ownerType, PropertyMetadata? defaultMetadata)
        => Register(name, propertyType, ownerType, defaultMetadata);

    private static PropertyMetadata Normalize(PropertyMetadata? metadata, Type propertyType)
    {
        metadata ??= new PropertyMetadata();
        if (metadata.DefaultValue is null && propertyType.IsValueType)
        {
            metadata.DefaultValue = Activator.CreateInstance(propertyType);
        }

        return metadata;
    }

    public override string ToString() => Name;
}

/// <summary>
/// Property bag behind every headless element. CLR properties of shim controls and registered dependency
/// properties both live here, and every change raises <see cref="PropertyChanged"/> so a web UI can mirror
/// the element without polling.
/// </summary>
public class DependencyObject : DispatcherObject, INotifyPropertyChanged
{
    // CLR properties of shim types and dependency properties share one name-keyed store, so
    // GetValue(TextBox.TextProperty) and TextBox.Text always agree.
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public event PropertyChangedEventHandler? PropertyChanged;

    // Annotated non-nullable like WPF's GetValue, so desktop code compiles without new warnings.
    public object GetValue(DependencyProperty dp)
    {
        if (HeadlessXaml.TryGetShimProperty(this, dp.Name, out var shimValue))
        {
            return shimValue!;
        }

        return (_values.TryGetValue(dp.Name, out var value) ? value : dp.DefaultMetadata.DefaultValue)!;
    }

    public void SetValue(DependencyProperty dp, object? value)
    {
        if (HeadlessXaml.TrySetShimProperty(this, dp.Name, value))
        {
            return;
        }

        var old = GetValue(dp);
        if (dp.DefaultMetadata.CoerceValueCallback is { } coerce)
        {
            value = coerce(this, value!);
        }

        _values[dp.Name] = value;
        if (!Equals(old, value))
        {
            var args = new DependencyPropertyChangedEventArgs(dp, old, value);
            dp.DefaultMetadata.PropertyChangedCallback?.Invoke(this, args);
            OnPropertyChanged(args);
            RaisePropertyChanged(dp.Name);
        }
    }

    public void SetCurrentValue(DependencyProperty dp, object? value) => SetValue(dp, value);

    public void ClearValue(DependencyProperty dp)
    {
        var old = GetValue(dp);
        _values.Remove(dp.Name);
        var current = GetValue(dp);
        if (!Equals(old, current))
        {
            dp.DefaultMetadata.PropertyChangedCallback?.Invoke(this, new DependencyPropertyChangedEventArgs(dp, old, current));
            RaisePropertyChanged(dp.Name);
        }
    }

    public object ReadLocalValue(DependencyProperty dp)
        => _values.TryGetValue(dp.Name, out var value) ? value! : DependencyProperty.UnsetValue;

    public void CoerceValue(DependencyProperty dp)
    {
    }

    public void InvalidateProperty(DependencyProperty dp)
    {
    }

    protected virtual void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
    }

    protected T Get<T>(T defaultValue = default!, [CallerMemberName] string name = "")
        => _values.TryGetValue(name, out var value) && value is T typed ? typed : (value is null && _values.ContainsKey(name) ? default! : defaultValue);

    protected bool Set<T>(T value, [CallerMemberName] string name = "")
    {
        if (_values.TryGetValue(name, out var old) && Equals(old, value))
        {
            return false;
        }

        _values[name] = value;
        RaisePropertyChanged(name);
        return true;
    }

    internal object? GetNamedValue(string name) => _values.TryGetValue(name, out var value) ? value : null;

    internal void SetNamedValue(string name, object? value)
    {
        _values[name] = value;
        RaisePropertyChanged(name);
    }

    protected void RaisePropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        HeadlessXaml.NotifyChanged(this, name);
    }
}

public class Freezable : DependencyObject
{
    /// <summary>Animations are not run headless; the property jumps to the animation's end value.</summary>
    public void BeginAnimation(DependencyProperty dp, Media.Animation.Timeline? animation)
    {
        var target = animation switch
        {
            Media.Animation.ColorAnimation { To: { } to } => (object)to,
            Media.Animation.DoubleAnimation { To: { } to } => to,
            _ => null,
        };
        if (target is not null)
        {
            SetValue(dp, target);
        }
    }

    public bool IsFrozen { get; private set; }

    public bool CanFreeze => true;

    public void Freeze() => IsFrozen = true;

    public Freezable Clone() => (Freezable)MemberwiseClone();

    public Freezable CloneCurrentValue() => Clone();
}

public class Visual : DependencyObject
{
}

public sealed class RoutedEvent
{
    internal RoutedEvent(string name, Type ownerType)
    {
        Name = name;
        OwnerType = ownerType;
    }

    public string Name { get; }
    public Type OwnerType { get; }

    public override string ToString() => Name;
}

public enum RoutingStrategy
{
    Tunnel,
    Bubble,
    Direct,
}

public static class EventManager
{
    public static RoutedEvent RegisterRoutedEvent(string name, RoutingStrategy routingStrategy, Type handlerType, Type ownerType)
        => new(name, ownerType);

    public static void RegisterClassHandler(Type classType, RoutedEvent routedEvent, Delegate handler)
    {
    }

    public static void RegisterClassHandler(Type classType, RoutedEvent routedEvent, Delegate handler, bool handledEventsToo)
    {
    }
}

public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);

public class RoutedEventArgs : EventArgs
{
    public RoutedEventArgs()
    {
    }

    public RoutedEventArgs(RoutedEvent? routedEvent)
    {
        RoutedEvent = routedEvent;
    }

    public RoutedEventArgs(RoutedEvent? routedEvent, object? source)
    {
        RoutedEvent = routedEvent;
        Source = source;
        OriginalSource = source;
    }

    public RoutedEvent? RoutedEvent { get; set; }
    public bool Handled { get; set; }
    public object? Source { get; set; }
    public object? OriginalSource { get; internal set; }
}

public delegate void SizeChangedEventHandler(object sender, SizeChangedEventArgs e);

public class SizeChangedEventArgs : RoutedEventArgs
{
    public Size NewSize { get; init; }
    public Size PreviousSize { get; init; }
    public bool WidthChanged { get; init; }
    public bool HeightChanged { get; init; }
}

public delegate void DragEventHandler(object sender, DragEventArgs e);

[Flags]
public enum DragDropEffects
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
    Scroll = int.MinValue,
    All = Copy | Move | Scroll,
}

public interface IDataObject
{
    object? GetData(string format);
    object? GetData(Type format);
    bool GetDataPresent(string format);
    bool GetDataPresent(Type format);
}

public sealed class DataObject : IDataObject
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    public DataObject()
    {
    }

    public DataObject(object data)
    {
        SetData(data.GetType().FullName ?? "object", data);
    }

    public DataObject(string format, object data)
    {
        SetData(format, data);
    }

    public void SetData(string format, object? data) => _data[format] = data;

    public object? GetData(string format) => _data.TryGetValue(format, out var data) ? data : null;

    public object? GetData(Type format) => GetData(format.FullName ?? format.Name);

    public bool GetDataPresent(string format) => _data.ContainsKey(format);

    public bool GetDataPresent(Type format) => GetDataPresent(format.FullName ?? format.Name);
}

public class DragEventArgs : RoutedEventArgs
{
    public IDataObject Data { get; init; } = new DataObject();
    public DragDropEffects Effects { get; set; }
    public DragDropEffects AllowedEffects { get; init; } = DragDropEffects.All;

    public Point GetPosition(IInputElement? relativeTo) => default;
}

public static class DragDrop
{
    public static DragDropEffects DoDragDrop(DependencyObject dragSource, object data, DragDropEffects allowedEffects)
        => DragDropEffects.None;
}

public interface IInputElement
{
}

public interface IFrameworkInputElement : IInputElement
{
}

public class UIElement : Visual, IInputElement
{
    public static readonly DependencyProperty VisibilityProperty = DependencyProperty.Register(nameof(Visibility), typeof(Visibility), typeof(UIElement));
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(UIElement), new PropertyMetadata(true));
    public static readonly DependencyProperty OpacityProperty = DependencyProperty.Register(nameof(Opacity), typeof(double), typeof(UIElement), new PropertyMetadata(1d));

    public bool IsMouseCaptured => false;

    public IInputElement? InputHitTest(Point point) => null;

    private readonly Dictionary<string, List<Delegate>> _handlers = new(StringComparer.Ordinal);

    public Visibility Visibility { get => Get(Visibility.Visible); set => Set(value); }
    public bool IsEnabled { get => Get(true); set => Set(value); }
    public bool IsVisible => Visibility == Visibility.Visible && (HeadlessParent as UIElement)?.IsVisible != false;
    public bool IsHitTestVisible { get => Get(true); set => Set(value); }
    public bool Focusable { get => Get(true); set => Set(value); }
    public bool IsFocused => false;
    public bool IsKeyboardFocusWithin => false;
    public bool IsKeyboardFocused => false;
    public bool IsMouseOver => false;
    public bool AllowDrop { get => Get(false); set => Set(value); }
    public double Opacity { get => Get(1d); set => Set(value); }
    public Media.Transform? RenderTransform { get => Get<Media.Transform?>(null); set => Set(value); }
    public Point RenderTransformOrigin { get => Get(default(Point)); set => Set(value); }
    public Size DesiredSize => default;
    public Size RenderSize => default;
    public bool IsArrangeValid => true;
    public bool IsMeasureValid => true;
    public Media.Effects.Effect? Effect { get => Get<Media.Effects.Effect?>(null); set => Set(value); }
    public Media.Geometry? Clip { get => Get<Media.Geometry?>(null); set => Set(value); }

    /// <summary>Logical parent as reconstructed from the markup (or set by the headless runtime).</summary>
    public DependencyObject? HeadlessParent { get; internal set; }

    public event RoutedEventHandler? GotFocus { add => AddNamed(nameof(GotFocus), value); remove => RemoveNamed(nameof(GotFocus), value); }
    public event RoutedEventHandler? LostFocus { add => AddNamed(nameof(LostFocus), value); remove => RemoveNamed(nameof(LostFocus), value); }
    public event Input.MouseButtonEventHandler? MouseLeftButtonDown { add => AddNamed(nameof(MouseLeftButtonDown), value); remove => RemoveNamed(nameof(MouseLeftButtonDown), value); }
    public event Input.MouseButtonEventHandler? MouseLeftButtonUp { add => AddNamed(nameof(MouseLeftButtonUp), value); remove => RemoveNamed(nameof(MouseLeftButtonUp), value); }
    public event Input.MouseButtonEventHandler? PreviewMouseLeftButtonDown { add => AddNamed(nameof(PreviewMouseLeftButtonDown), value); remove => RemoveNamed(nameof(PreviewMouseLeftButtonDown), value); }
    public event Input.MouseButtonEventHandler? PreviewMouseLeftButtonUp { add => AddNamed(nameof(PreviewMouseLeftButtonUp), value); remove => RemoveNamed(nameof(PreviewMouseLeftButtonUp), value); }
    public event Input.MouseButtonEventHandler? MouseRightButtonUp { add => AddNamed(nameof(MouseRightButtonUp), value); remove => RemoveNamed(nameof(MouseRightButtonUp), value); }
    public event Input.MouseButtonEventHandler? MouseDown { add => AddNamed(nameof(MouseDown), value); remove => RemoveNamed(nameof(MouseDown), value); }
    public event Input.MouseButtonEventHandler? MouseUp { add => AddNamed(nameof(MouseUp), value); remove => RemoveNamed(nameof(MouseUp), value); }
    public event Input.MouseButtonEventHandler? PreviewMouseDown { add => AddNamed(nameof(PreviewMouseDown), value); remove => RemoveNamed(nameof(PreviewMouseDown), value); }
    public event Input.MouseButtonEventHandler? PreviewMouseUp { add => AddNamed(nameof(PreviewMouseUp), value); remove => RemoveNamed(nameof(PreviewMouseUp), value); }
    public event Input.MouseEventHandler? MouseMove { add => AddNamed(nameof(MouseMove), value); remove => RemoveNamed(nameof(MouseMove), value); }
    public event Input.MouseEventHandler? PreviewMouseMove { add => AddNamed(nameof(PreviewMouseMove), value); remove => RemoveNamed(nameof(PreviewMouseMove), value); }
    public event Input.MouseEventHandler? MouseEnter { add => AddNamed(nameof(MouseEnter), value); remove => RemoveNamed(nameof(MouseEnter), value); }
    public event Input.MouseEventHandler? MouseLeave { add => AddNamed(nameof(MouseLeave), value); remove => RemoveNamed(nameof(MouseLeave), value); }
    public event Input.MouseWheelEventHandler? PreviewMouseWheel { add => AddNamed(nameof(PreviewMouseWheel), value); remove => RemoveNamed(nameof(PreviewMouseWheel), value); }
    public event Input.MouseWheelEventHandler? MouseWheel { add => AddNamed(nameof(MouseWheel), value); remove => RemoveNamed(nameof(MouseWheel), value); }
    public event Input.KeyEventHandler? KeyDown { add => AddNamed(nameof(KeyDown), value); remove => RemoveNamed(nameof(KeyDown), value); }
    public event Input.KeyEventHandler? KeyUp { add => AddNamed(nameof(KeyUp), value); remove => RemoveNamed(nameof(KeyUp), value); }
    public event Input.KeyEventHandler? PreviewKeyDown { add => AddNamed(nameof(PreviewKeyDown), value); remove => RemoveNamed(nameof(PreviewKeyDown), value); }
    public event Input.TextCompositionEventHandler? PreviewTextInput { add => AddNamed(nameof(PreviewTextInput), value); remove => RemoveNamed(nameof(PreviewTextInput), value); }
    public event Input.KeyboardFocusChangedEventHandler? GotKeyboardFocus { add => AddNamed(nameof(GotKeyboardFocus), value); remove => RemoveNamed(nameof(GotKeyboardFocus), value); }
    public event Input.KeyboardFocusChangedEventHandler? LostKeyboardFocus { add => AddNamed(nameof(LostKeyboardFocus), value); remove => RemoveNamed(nameof(LostKeyboardFocus), value); }
    public event Input.KeyboardFocusChangedEventHandler? PreviewLostKeyboardFocus { add => AddNamed(nameof(PreviewLostKeyboardFocus), value); remove => RemoveNamed(nameof(PreviewLostKeyboardFocus), value); }
    public event DragEventHandler? Drop { add => AddNamed(nameof(Drop), value); remove => RemoveNamed(nameof(Drop), value); }
    public event DragEventHandler? DragOver { add => AddNamed(nameof(DragOver), value); remove => RemoveNamed(nameof(DragOver), value); }
    public event DragEventHandler? DragEnter { add => AddNamed(nameof(DragEnter), value); remove => RemoveNamed(nameof(DragEnter), value); }
    public event DragEventHandler? DragLeave { add => AddNamed(nameof(DragLeave), value); remove => RemoveNamed(nameof(DragLeave), value); }
    public event DependencyPropertyChangedEventHandler? IsVisibleChanged { add => AddNamed(nameof(IsVisibleChanged), value); remove => RemoveNamed(nameof(IsVisibleChanged), value); }
    public event DependencyPropertyChangedEventHandler? IsEnabledChanged { add => AddNamed(nameof(IsEnabledChanged), value); remove => RemoveNamed(nameof(IsEnabledChanged), value); }

    public bool Focus() => true;

    public void UpdateLayout()
    {
    }

    public void BeginAnimation(DependencyProperty dp, Media.Animation.Timeline? animation)
    {
        if (animation is Media.Animation.DoubleAnimation { To: { } to })
        {
            SetValue(dp, to);
        }
    }

    public void InvalidateVisual()
    {
    }

    public void InvalidateMeasure()
    {
    }

    public void InvalidateArrange()
    {
    }

    public void Measure(Size availableSize)
    {
    }

    public void Arrange(Rect finalRect)
    {
    }

    public bool CaptureMouse() => true;

    public void ReleaseMouseCapture()
    {
    }

    public Point TranslatePoint(Point point, UIElement relativeTo) => point;

    public void AddHandler(RoutedEvent routedEvent, Delegate handler) => AddNamed(routedEvent.Name, handler);

    public void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo) => AddNamed(routedEvent.Name, handler);

    public void RemoveHandler(RoutedEvent routedEvent, Delegate handler) => RemoveNamed(routedEvent.Name, handler);

    public void RaiseEvent(RoutedEventArgs e)
    {
        if (e.RoutedEvent is not null)
        {
            RaiseHeadlessEvent(e.RoutedEvent.Name, e);
        }
    }

    internal void AddNamed(string eventName, Delegate? handler)
    {
        if (handler is null)
        {
            return;
        }

        if (!_handlers.TryGetValue(eventName, out var list))
        {
            list = new List<Delegate>();
            _handlers[eventName] = list;
        }

        list.Add(handler);
    }

    internal void RemoveNamed(string eventName, Delegate? handler)
    {
        if (handler is not null && _handlers.TryGetValue(eventName, out var list))
        {
            var index = list.LastIndexOf(handler);
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
        }
    }

    internal bool HasHeadlessHandlers(string eventName)
        => _handlers.TryGetValue(eventName, out var list) && list.Count > 0;

    /// <summary>
    /// Raises <paramref name="eventName"/> on this element and, for routed args, bubbles it up the
    /// reconstructed logical tree until a handler marks it handled — the behavior code-behind relies on
    /// for container-level handlers such as <c>CheckBox.Checked</c> on a panel.
    /// </summary>
    public void RaiseHeadlessEvent(string eventName, EventArgs args)
    {
        if (args is RoutedEventArgs routed)
        {
            routed.OriginalSource ??= this;
            routed.Source ??= this;
        }

        DependencyObject? current = this;
        while (current is UIElement element)
        {
            element.InvokeHandlers(eventName, args);
            if (args is not RoutedEventArgs { Handled: false } || !HeadlessXaml.IsBubbling(eventName))
            {
                break;
            }

            current = element.HeadlessParent;
        }
    }

    private void InvokeHandlers(string eventName, EventArgs args)
    {
        if (!_handlers.TryGetValue(eventName, out var list) || list.Count == 0)
        {
            return;
        }

        foreach (var handler in list.ToArray())
        {
            try
            {
                if (handler is HeadlessHandler headless)
                {
                    headless(this, args);
                }
                else
                {
                    handler.DynamicInvoke(this, args);
                }
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }

    internal void RaiseDependencyChanged(string eventName, object? oldValue, object? newValue)
    {
        if (_handlers.TryGetValue(eventName, out var list))
        {
            var args = new DependencyPropertyChangedEventArgs(new PropertyHandle(eventName).Property, oldValue, newValue);
            foreach (var handler in list.ToArray())
            {
                handler.DynamicInvoke(this, args);
            }
        }
    }

    private readonly struct PropertyHandle
    {
        private static readonly Dictionary<string, DependencyProperty> Cache = new();

        public PropertyHandle(string name)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(name, out var property))
                {
                    property = DependencyProperty.Register(name, typeof(object), typeof(UIElement));
                    Cache[name] = property;
                }

                Property = property;
            }
        }

        public DependencyProperty Property { get; }
    }
}

public delegate void HeadlessHandler(object sender, EventArgs e);

public class ResourceDictionary : IEnumerable
{
    private readonly Dictionary<object, object?> _resources = new();

    public Collection<ResourceDictionary> MergedDictionaries { get; } = new();

    public Uri? Source { get; set; }

    public object this[object key]
    {
        get
        {
            if (_resources.TryGetValue(key, out var value))
            {
                return value!;
            }

            for (var i = MergedDictionaries.Count - 1; i >= 0; i--)
            {
                if (MergedDictionaries[i].Contains(key))
                {
                    return MergedDictionaries[i][key];
                }
            }

            return null!;
        }
        set => _resources[key] = value;
    }

    public ICollection Keys => _resources.Keys;

    public int Count => _resources.Count;

    public void Add(object key, object? value) => _resources[key] = value;

    public bool Contains(object key)
        => _resources.ContainsKey(key) || MergedDictionaries.Any(dictionary => dictionary.Contains(key));

    public void Remove(object key) => _resources.Remove(key);

    public ICollection Values => _resources.Values;

    // WPF enumerates resources as DictionaryEntry, like IDictionary.
    public IEnumerator GetEnumerator()
        => _resources.Select(pair => new DictionaryEntry(pair.Key, pair.Value)).ToList().GetEnumerator();
}

public class Style
{
    public Style()
    {
    }

    public Style(Type targetType)
    {
        TargetType = targetType;
    }

    public Style(Type targetType, Style? basedOn)
    {
        TargetType = targetType;
        BasedOn = basedOn;
    }

    public Type? TargetType { get; set; }
    public Style? BasedOn { get; set; }
    public Collection<SetterBase> Setters { get; } = new();
    public Collection<TriggerBase> Triggers { get; } = new();
    public ResourceDictionary Resources { get; set; } = new();
    public string? HeadlessKey { get; init; }
    public string? HeadlessTargetTypeName { get; init; }
    public Dictionary<string, string> HeadlessSetters { get; } = new(StringComparer.Ordinal);
}

public abstract class SetterBase
{
}

public class Setter : SetterBase
{
    public Setter()
    {
    }

    public Setter(DependencyProperty property, object? value)
    {
        Property = property;
        Value = value;
    }

    public DependencyProperty? Property { get; set; }
    public object? Value { get; set; }
}

public class EventSetter : SetterBase
{
    public EventSetter()
    {
    }

    public EventSetter(RoutedEvent routedEvent, Delegate handler)
    {
        Event = routedEvent;
        Handler = handler;
    }

    public RoutedEvent? Event { get; set; }
    public Delegate? Handler { get; set; }
}

public abstract class TriggerBase
{
    public Collection<SetterBase> Setters { get; } = new();
}

public class Trigger : TriggerBase
{
    public DependencyProperty? Property { get; set; }
    public object? Value { get; set; }
}

public class DataTrigger : TriggerBase
{
    public Data.BindingBase? Binding { get; set; }
    public object? Value { get; set; }
}

public abstract class FrameworkTemplate
{
    public FrameworkElementFactory? VisualTree { get; set; }
    public ResourceDictionary Resources { get; set; } = new();

    public void Seal()
    {
    }
}

public class DataTemplate : FrameworkTemplate
{
    public DataTemplate()
    {
    }

    public DataTemplate(object dataType)
    {
        DataType = dataType;
    }

    public object? DataType { get; set; }
}

public class ControlTemplate : FrameworkTemplate
{
    public Type? TargetType { get; set; }
}

public class ItemsPanelTemplate : FrameworkTemplate
{
    public ItemsPanelTemplate()
    {
    }

    public ItemsPanelTemplate(FrameworkElementFactory root)
    {
        VisualTree = root;
    }
}

public class FrameworkElementFactory
{
    private readonly List<FrameworkElementFactory> _children = new();

    public FrameworkElementFactory()
    {
    }

    public FrameworkElementFactory(Type type)
    {
        Type = type;
    }

    public FrameworkElementFactory(Type type, string name)
    {
        Type = type;
        Name = name;
    }

    public Type? Type { get; set; }
    public string? Name { get; set; }
    public string? Text { get; set; }
    public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Data.BindingBase> Bindings { get; } = new(StringComparer.Ordinal);
    public IReadOnlyList<FrameworkElementFactory> Children => _children;

    public void SetValue(DependencyProperty dp, object? value) => Values[dp.Name] = value;

    public void SetBinding(DependencyProperty dp, Data.BindingBase binding) => Bindings[dp.Name] = binding;

    public void SetResourceReference(DependencyProperty dp, object name) => Values[dp.Name] = name;

    public void AddHandler(RoutedEvent routedEvent, Delegate handler)
    {
    }

    public void AppendChild(FrameworkElementFactory child) => _children.Add(child);
}

public static class Clipboard
{
    private static string _text = string.Empty;

    /// <summary>Raised when code-behind copies text, so a web UI can offer it to the browser clipboard.</summary>
    public static event Action<string>? HeadlessTextCopied;

    public static void SetText(string text)
    {
        _text = text ?? string.Empty;
        HeadlessTextCopied?.Invoke(_text);
    }

    public static void SetDataObject(object data) => SetText(data?.ToString() ?? string.Empty);

    public static void SetDataObject(object data, bool copy) => SetDataObject(data);

    public static string GetText() => _text;

    public static bool ContainsText() => _text.Length > 0;

    public static void Clear() => _text = string.Empty;
}

public static class MessageBox
{
    public static MessageBoxResult Show(string messageBoxText)
        => Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption)
        => Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        => Show(messageBoxText, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => HeadlessDialogs.ShowMessage(null, messageBoxText, caption, button, icon);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        => HeadlessDialogs.ShowMessage(null, messageBoxText, caption, button, icon);

    public static MessageBoxResult Show(Window? owner, string messageBoxText)
        => HeadlessDialogs.ShowMessage(owner, messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption)
        => HeadlessDialogs.ShowMessage(owner, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button)
        => HeadlessDialogs.ShowMessage(owner, messageBoxText, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => HeadlessDialogs.ShowMessage(owner, messageBoxText, caption, button, icon);
}

public abstract class PresentationSource
{
    public static PresentationSource? FromVisual(Visual visual) => null;

    public static PresentationSource? FromDependencyObject(DependencyObject dependencyObject) => null;

    public Media.CompositionTarget? CompositionTarget => null;
}
