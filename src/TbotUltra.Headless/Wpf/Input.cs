using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace System.Windows.Input
{
    public enum Key
    {
        None,
        Back,
        Tab,
        Enter,
        Return = Enter,
        Escape,
        Space,
        PageUp,
        PageDown,
        End,
        Home,
        Left,
        Up,
        Right,
        Down,
        Delete,
        Insert,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        OemMinus,
        OemPlus,
        OemComma,
        OemPeriod,
        Subtract,
        Add,
        Decimal,
        LeftCtrl,
        RightCtrl,
        LeftShift,
        RightShift,
        LeftAlt,
        RightAlt,
        System,
    }

    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
    }

    public enum MouseButton
    {
        Left,
        Middle,
        Right,
        XButton1,
        XButton2,
    }

    public enum MouseButtonState
    {
        Released,
        Pressed,
    }

    public sealed class Cursor
    {
        internal Cursor(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public static class Cursors
    {
        public static Cursor Arrow { get; } = new("default");
        public static Cursor Hand { get; } = new("pointer");
        public static Cursor Wait { get; } = new("wait");
        public static Cursor AppStarting { get; } = new("progress");
        public static Cursor IBeam { get; } = new("text");
        public static Cursor SizeAll { get; } = new("move");
        public static Cursor SizeNS { get; } = new("ns-resize");
        public static Cursor SizeWE { get; } = new("ew-resize");
        public static Cursor No { get; } = new("not-allowed");
        public static Cursor Cross { get; } = new("crosshair");
        public static Cursor None { get; } = new("none");
    }

    public static class Mouse
    {
        public static Cursor? OverrideCursor { get; set; }
        public static MouseButtonState LeftButton => MouseButtonState.Released;
        public static MouseButtonState RightButton => MouseButtonState.Released;
        public static IInputElement? Captured => null;
        public static IInputElement? DirectlyOver => null;

        public static Point GetPosition(IInputElement? relativeTo) => default;

        public static bool Capture(IInputElement? element) => true;
    }

    public static class Keyboard
    {
        public static ModifierKeys Modifiers => ModifierKeys.None;
        public static IInputElement? FocusedElement => null;

        public static IInputElement? Focus(IInputElement? element) => element;

        public static void ClearFocus()
        {
        }

        public static bool IsKeyDown(Key key) => false;

        public static bool IsKeyUp(Key key) => true;
    }

    public static class FocusManager
    {
        public static IInputElement? GetFocusedElement(DependencyObject element) => null;

        public static void SetFocusedElement(DependencyObject element, IInputElement? value)
        {
        }

        public static DependencyObject? GetFocusScope(DependencyObject element) => element;
    }

    public enum FocusNavigationDirection
    {
        Next,
        Previous,
        First,
        Last,
        Left,
        Right,
        Up,
        Down,
    }

    public sealed class TraversalRequest
    {
        public TraversalRequest(FocusNavigationDirection focusNavigationDirection)
        {
            FocusNavigationDirection = focusNavigationDirection;
        }

        public FocusNavigationDirection FocusNavigationDirection { get; }
    }

    public class InputEventArgs : RoutedEventArgs
    {
        public int Timestamp { get; init; }
    }

    public class MouseEventArgs : InputEventArgs
    {
        public MouseButtonState LeftButton { get; init; }
        public MouseButtonState RightButton { get; init; }

        public Point GetPosition(IInputElement? relativeTo) => default;
    }

    public class MouseButtonEventArgs : MouseEventArgs
    {
        public MouseButton ChangedButton { get; init; } = MouseButton.Left;
        public MouseButtonState ButtonState { get; init; } = MouseButtonState.Pressed;
        public int ClickCount { get; init; } = 1;
    }

    public class MouseWheelEventArgs : MouseEventArgs
    {
        public int Delta { get; init; }
    }

    public class KeyEventArgs : InputEventArgs
    {
        public Key Key { get; init; }
        public Key SystemKey { get; init; }
        public bool IsRepeat { get; init; }
        public bool IsDown { get; init; } = true;
        public bool IsUp => !IsDown;
        public ModifierKeys KeyboardDevice_Modifiers { get; init; }
    }

    public class TextCompositionEventArgs : InputEventArgs
    {
        public string Text { get; init; } = string.Empty;
    }

    public class KeyboardFocusChangedEventArgs : InputEventArgs
    {
        public IInputElement? OldFocus { get; init; }
        public IInputElement? NewFocus { get; init; }
    }

    public delegate void MouseEventHandler(object sender, MouseEventArgs e);

    public delegate void MouseButtonEventHandler(object sender, MouseButtonEventArgs e);

    public delegate void MouseWheelEventHandler(object sender, MouseWheelEventArgs e);

    public delegate void KeyEventHandler(object sender, KeyEventArgs e);

    public delegate void TextCompositionEventHandler(object sender, TextCompositionEventArgs e);

    public delegate void KeyboardFocusChangedEventHandler(object sender, KeyboardFocusChangedEventArgs e);

    public class InputBinding : DependencyObject
    {
        public ICommand? Command { get; set; }
        public object? CommandParameter { get; set; }
        public InputGesture? Gesture { get; set; }
    }

    public sealed class KeyBinding : InputBinding
    {
        public KeyBinding()
        {
        }

        public KeyBinding(ICommand command, Key key, ModifierKeys modifiers)
        {
            Command = command;
            Key = key;
            Modifiers = modifiers;
        }

        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
    }

    public abstract class InputGesture
    {
    }

    public sealed class KeyGesture : InputGesture
    {
        public KeyGesture(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public Key Key { get; }
        public ModifierKeys Modifiers { get; }
    }

    public sealed class InputBindingCollection : Collection<InputBinding>
    {
    }

    public class RoutedCommand : ICommand
    {
        public RoutedCommand()
        {
        }

        public RoutedCommand(string name, Type ownerType)
        {
            Name = name;
        }

        public string Name { get; } = string.Empty;
        public InputGestureCollection InputGestures { get; } = new();

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }

        public bool CanExecute(object? parameter, IInputElement target) => true;

        public void Execute(object? parameter, IInputElement target)
        {
        }
    }

    public sealed class InputGestureCollection : Collection<InputGesture>
    {
    }

    public class RoutedUICommand : RoutedCommand
    {
        public RoutedUICommand()
        {
        }

        public RoutedUICommand(string text, string name, Type ownerType)
            : base(name, ownerType)
        {
            Text = text;
        }

        public string Text { get; set; } = string.Empty;
    }

    public static class CommandManager
    {
        public static event EventHandler? RequerySuggested;

        public static void InvalidateRequerySuggested() => RequerySuggested?.Invoke(null, EventArgs.Empty);
    }

    public static class ApplicationCommands
    {
        public static RoutedUICommand Copy { get; } = new("Copy", nameof(Copy), typeof(ApplicationCommands));
        public static RoutedUICommand Paste { get; } = new("Paste", nameof(Paste), typeof(ApplicationCommands));
        public static RoutedUICommand Close { get; } = new("Close", nameof(Close), typeof(ApplicationCommands));
    }
}

namespace System.Windows.Documents
{
    using System.Collections;
    using System.Windows.Media;

    public abstract class TextElement : FrameworkContentElement
    {
        public Brush? Foreground { get => Get<Brush?>(null); set => Set(value); }
        public Brush? Background { get => Get<Brush?>(null); set => Set(value); }
        public FontWeight FontWeight { get => Get(FontWeight.Normal); set => Set(value); }
        public FontStyle FontStyle { get => Get(FontStyle.Normal); set => Set(value); }
        public double FontSize { get => Get(12d); set => Set(value); }
        public FontFamily? FontFamily { get => Get<FontFamily?>(null); set => Set(value); }
        public object? ToolTip { get => Get<object?>(null); set => Set(value); }
    }

    public abstract class Inline : TextElement
    {
        public Controls.TextDecorationCollection? TextDecorations { get => Get<Controls.TextDecorationCollection?>(null); set => Set(value); }

        internal abstract string ToText();
    }

    public class Run : Inline
    {
        public Run()
        {
        }

        public Run(string text)
        {
            Text = text;
        }

        public string Text { get => Get(string.Empty); set => Set(value); }

        internal override string ToText() => Text;
    }

    public sealed class LineBreak : Inline
    {
        internal override string ToText() => "\n";
    }

    public class Span : Inline
    {
        public Span()
        {
            Inlines = new InlineCollection(null);
        }

        public Span(Inline childInline)
            : this()
        {
            Inlines.Add(childInline);
        }

        public InlineCollection Inlines { get; }

        internal override string ToText() => Inlines.ToText();
    }

    public sealed class Bold : Span
    {
        public Bold()
        {
        }

        public Bold(Inline childInline)
            : base(childInline)
        {
        }
    }

    public sealed class Italic : Span
    {
        public Italic()
        {
        }

        public Italic(Inline childInline)
            : base(childInline)
        {
        }
    }

    public sealed class Underline : Span
    {
        public Underline()
        {
        }

        public Underline(Inline childInline)
            : base(childInline)
        {
        }
    }

    public sealed class Hyperlink : Span
    {
        public Hyperlink()
        {
        }

        public Hyperlink(Inline childInline)
            : base(childInline)
        {
        }

        public Uri? NavigateUri { get => Get<Uri?>(null); set => Set(value); }
        public System.Windows.Input.ICommand? Command { get => Get<System.Windows.Input.ICommand?>(null); set => Set(value); }

        public event RoutedEventHandler? Click;

        public event System.Windows.Navigation.RequestNavigateEventHandler? RequestNavigate;

        public void DoClick() => Click?.Invoke(this, new RoutedEventArgs(null, this));

        public void DoNavigate()
        {
            if (NavigateUri is not null)
            {
                RequestNavigate?.Invoke(this, new System.Windows.Navigation.RequestNavigateEventArgs(NavigateUri, null));
            }
        }
    }

    public sealed class InlineCollection : IEnumerable<Inline>
    {
        private readonly Controls.TextBlock? _owner;
        private readonly List<Inline> _inlines = new();

        internal InlineCollection(Controls.TextBlock? owner)
        {
            _owner = owner;
        }

        public int Count => _inlines.Count;

        public Inline? FirstInline => _inlines.FirstOrDefault();

        public Inline? LastInline => _inlines.LastOrDefault();

        public void Add(Inline inline)
        {
            _inlines.Add(inline);
            _owner?.NotifyInlinesChanged();
        }

        public void Add(string text) => Add(new Run(text));

        public void AddRange(IEnumerable inlines)
        {
            foreach (var inline in inlines)
            {
                if (inline is Inline typed)
                {
                    _inlines.Add(typed);
                }
            }

            _owner?.NotifyInlinesChanged();
        }

        public void Clear()
        {
            _inlines.Clear();
            _owner?.NotifyInlinesChanged();
        }

        public bool Remove(Inline inline)
        {
            var removed = _inlines.Remove(inline);
            _owner?.NotifyInlinesChanged();
            return removed;
        }

        internal void ClearSilently() => _inlines.Clear();

        internal string ToText() => string.Concat(_inlines.Select(inline => inline.ToText()));

        /// <summary>The inline runs, for a web UI that renders bold/colored fragments.</summary>
        public IReadOnlyList<Inline> HeadlessInlines => _inlines;

        public IEnumerator<Inline> GetEnumerator() => _inlines.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _inlines.GetEnumerator();
    }

    public class FlowDocument : FrameworkContentElement
    {
    }
}

namespace System.Windows.Navigation
{
    public delegate void RequestNavigateEventHandler(object sender, RequestNavigateEventArgs e);

    public class RequestNavigateEventArgs : RoutedEventArgs
    {
        public RequestNavigateEventArgs(Uri uri, string? target)
        {
            Uri = uri;
            Target = target;
        }

        public Uri Uri { get; }
        public string? Target { get; }
    }
}

namespace System.Windows.Shapes
{
    using System.Windows.Media;

    public abstract class Shape : FrameworkElement
    {
        public Brush? Fill { get => Get<Brush?>(null); set => Set(value); }
        public Brush? Stroke { get => Get<Brush?>(null); set => Set(value); }
        public double StrokeThickness { get => Get(1d); set => Set(value); }
        public Stretch Stretch { get => Get(Stretch.None); set => Set(value); }
        public DoubleCollection? StrokeDashArray { get => Get<DoubleCollection?>(null); set => Set(value); }
        public PenLineCap StrokeStartLineCap { get => Get(PenLineCap.Flat); set => Set(value); }
        public PenLineCap StrokeEndLineCap { get => Get(PenLineCap.Flat); set => Set(value); }
        public PenLineJoin StrokeLineJoin { get => Get(PenLineJoin.Miter); set => Set(value); }
    }

    public sealed class Ellipse : Shape
    {
    }

    public sealed class Rectangle : Shape
    {
        public double RadiusX { get => Get(0d); set => Set(value); }
        public double RadiusY { get => Get(0d); set => Set(value); }
    }

    public sealed class Line : Shape
    {
        public double X1 { get => Get(0d); set => Set(value); }
        public double Y1 { get => Get(0d); set => Set(value); }
        public double X2 { get => Get(0d); set => Set(value); }
        public double Y2 { get => Get(0d); set => Set(value); }
    }

    public sealed class Path : Shape
    {
        public Geometry? Data { get => Get<Geometry?>(null); set => Set(value); }
    }

    public sealed class Polyline : Shape
    {
        public PointCollection Points { get => Get(new PointCollection()); set => Set(value); }
    }

    public sealed class Polygon : Shape
    {
        public PointCollection Points { get => Get(new PointCollection()); set => Set(value); }
    }
}

namespace System.Windows.Interop
{
    public sealed class WindowInteropHelper
    {
        public WindowInteropHelper(Window window)
        {
            Owner = IntPtr.Zero;
        }

        public IntPtr Handle => IntPtr.Zero;
        public IntPtr Owner { get; set; }

        public IntPtr EnsureHandle() => IntPtr.Zero;
    }

    public sealed class HwndSource : PresentationSource
    {
        public IntPtr Handle => IntPtr.Zero;

        public static HwndSource? FromHwnd(IntPtr hwnd) => null;

        public void AddHook(HwndSourceHook hook)
        {
        }
    }

    public delegate IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
}

namespace System.Windows.Markup
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ContentPropertyAttribute : Attribute
    {
        public ContentPropertyAttribute()
        {
        }

        public ContentPropertyAttribute(string name)
        {
            Name = name;
        }

        public string? Name { get; }
    }

    public abstract class MarkupExtension
    {
        public abstract object ProvideValue(IServiceProvider serviceProvider);
    }

    public interface IComponentConnector
    {
        void InitializeComponent();

        void Connect(int connectionId, object target);
    }

    public static class XamlReader
    {
        public static object Parse(string xamlText) => throw new NotSupportedException("Runtime XAML parsing is not available in the headless host.");
    }
}

namespace System.Windows
{
    public delegate void RoutedPropertyChangedEventHandler<T>(object sender, RoutedPropertyChangedEventArgs<T> e);

    public class RoutedPropertyChangedEventArgs<T> : RoutedEventArgs
    {
        public RoutedPropertyChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public T OldValue { get; }
        public T NewValue { get; }
    }

    public static class SystemColors
    {
        public static Media.SolidColorBrush ControlBrush { get; } = new(Media.Colors.LightGray);
        public static Media.SolidColorBrush WindowBrush { get; } = new(Media.Colors.White);
        public static Media.SolidColorBrush ControlTextBrush { get; } = new(Media.Colors.Black);
        public static Media.SolidColorBrush HighlightBrush { get; } = new(Media.Colors.SteelBlue);
        public static object HighlightBrushKey { get; } = nameof(HighlightBrushKey);
        public static object InactiveSelectionHighlightBrushKey { get; } = nameof(InactiveSelectionHighlightBrushKey);
        public static object HighlightTextBrushKey { get; } = nameof(HighlightTextBrushKey);
        public static object ControlBrushKey { get; } = nameof(ControlBrushKey);
    }
}

namespace System.Media
{
    public sealed class SystemSound
    {
        public void Play()
        {
        }
    }

    public static class SystemSounds
    {
        public static SystemSound Asterisk { get; } = new();
        public static SystemSound Beep { get; } = new();
        public static SystemSound Exclamation { get; } = new();
        public static SystemSound Hand { get; } = new();
        public static SystemSound Question { get; } = new();
    }
}
