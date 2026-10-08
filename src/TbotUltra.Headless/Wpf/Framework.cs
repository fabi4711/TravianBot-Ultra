using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Headless;
using System.Windows.Input;
using System.Windows.Threading;

namespace System.Windows;

public class FrameworkElement : UIElement, IFrameworkInputElement, ISupportInitialize
{
    public static readonly RoutedEvent LoadedEvent = EventManager.RegisterRoutedEvent("Loaded", RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(FrameworkElement));
    public static readonly DependencyProperty DataContextProperty = DependencyProperty.Register(nameof(DataContext), typeof(object), typeof(FrameworkElement));
    public static readonly DependencyProperty TagProperty = DependencyProperty.Register(nameof(Tag), typeof(object), typeof(FrameworkElement));
    public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(nameof(Width), typeof(double), typeof(FrameworkElement), new PropertyMetadata(double.NaN));
    public static readonly DependencyProperty HeightProperty = DependencyProperty.Register(nameof(Height), typeof(double), typeof(FrameworkElement), new PropertyMetadata(double.NaN));
    public static readonly DependencyProperty MarginProperty = DependencyProperty.Register(nameof(Margin), typeof(Thickness), typeof(FrameworkElement));
    public static readonly DependencyProperty StyleProperty = DependencyProperty.Register(nameof(Style), typeof(Style), typeof(FrameworkElement));
    public static readonly DependencyProperty ToolTipProperty = DependencyProperty.Register(nameof(ToolTip), typeof(object), typeof(FrameworkElement));
    public static readonly DependencyProperty HorizontalAlignmentProperty = DependencyProperty.Register(nameof(HorizontalAlignment), typeof(HorizontalAlignment), typeof(FrameworkElement));
    public static readonly DependencyProperty VerticalAlignmentProperty = DependencyProperty.Register(nameof(VerticalAlignment), typeof(VerticalAlignment), typeof(FrameworkElement));

    private ResourceDictionary? _resources;
    private object? _dataContext;
    private bool _hasLocalDataContext;
    private readonly Dictionary<string, BindingBase> _bindings = new(StringComparer.Ordinal);

    public string Name { get => Get(string.Empty); set => Set(value); }
    public object? Tag { get => Get<object?>(null); set => Set(value); }
    public object? ToolTip { get => Get<object?>(null); set => Set(value); }
    public double Width { get => Get(double.NaN); set => Set(value); }
    public double Height { get => Get(double.NaN); set => Set(value); }
    public double MinWidth { get => Get(0d); set => Set(value); }
    public double MinHeight { get => Get(0d); set => Set(value); }
    public double MaxWidth { get => Get(double.PositiveInfinity); set => Set(value); }
    public double MaxHeight { get => Get(double.PositiveInfinity); set => Set(value); }
    public double ActualWidth => double.IsNaN(Width) ? 0 : Width;
    public double ActualHeight => double.IsNaN(Height) ? 0 : Height;
    public Thickness Margin { get => Get(default(Thickness)); set => Set(value); }
    public HorizontalAlignment HorizontalAlignment { get => Get(HorizontalAlignment.Stretch); set => Set(value); }
    public VerticalAlignment VerticalAlignment { get => Get(VerticalAlignment.Stretch); set => Set(value); }
    public Style? Style { get => Get<Style?>(null); set => Set(value); }
    public Cursor? Cursor { get => Get<Cursor?>(null); set => Set(value); }
    public Controls.ContextMenu? ContextMenu { get => Get<Controls.ContextMenu?>(null); set => Set(value); }
    public Media.Transform? LayoutTransform { get => Get<Media.Transform?>(null); set => Set(value); }
    public FlowDirection FlowDirection { get => Get(FlowDirection.LeftToRight); set => Set(value); }
    public bool IsLoaded { get; private set; }
    public bool IsInitialized => true;
    public DependencyObject? Parent => HeadlessParent;
    public DependencyObject? TemplatedParent => null;
    public TriggerCollection Triggers { get; } = new();
    public InputBindingCollection InputBindings { get; } = new();
    public Language Language { get; set; } = new();

    internal ResourceDictionary? ExistingResources => _resources;

    public ResourceDictionary Resources
    {
        get => _resources ??= new ResourceDictionary();
        set => _resources = value;
    }

    /// <summary>Inherited from the logical parent unless set locally, as in WPF.</summary>
    public object? DataContext
    {
        get => _hasLocalDataContext ? _dataContext : HeadlessXaml.InheritedDataContext(HeadlessParent);
        set
        {
            var old = DataContext;
            _dataContext = value;
            _hasLocalDataContext = true;
            if (!ReferenceEquals(old, value))
            {
                RaisePropertyChanged(nameof(DataContext));
                RaiseDependencyChanged(nameof(DataContextChanged), old, value);
            }
        }
    }

    public event RoutedEventHandler? Loaded { add => AddNamed(nameof(Loaded), value); remove => RemoveNamed(nameof(Loaded), value); }
    public event RoutedEventHandler? Unloaded { add => AddNamed(nameof(Unloaded), value); remove => RemoveNamed(nameof(Unloaded), value); }
    public event SizeChangedEventHandler? SizeChanged { add => AddNamed(nameof(SizeChanged), value); remove => RemoveNamed(nameof(SizeChanged), value); }
    public event DependencyPropertyChangedEventHandler? DataContextChanged { add => AddNamed(nameof(DataContextChanged), value); remove => RemoveNamed(nameof(DataContextChanged), value); }
    public event EventHandler? Initialized;
    public event Controls.ToolTipEventHandler? ToolTipOpening { add => AddNamed(nameof(ToolTipOpening), value); remove => RemoveNamed(nameof(ToolTipOpening), value); }
    public event RequestBringIntoViewEventHandler? RequestBringIntoView { add => AddNamed(nameof(RequestBringIntoView), value); remove => RemoveNamed(nameof(RequestBringIntoView), value); }
    public event Controls.ContextMenuEventHandler? ContextMenuOpening { add => AddNamed(nameof(ContextMenuOpening), value); remove => RemoveNamed(nameof(ContextMenuOpening), value); }

    public object FindResource(object resourceKey)
        => TryFindResource(resourceKey) ?? throw new ResourceReferenceKeyNotFoundException($"'{resourceKey}' resource not found.", resourceKey);

    public object? TryFindResource(object resourceKey)
    {
        for (DependencyObject? current = this; current is not null; current = (current as UIElement)?.HeadlessParent)
        {
            if (current is FrameworkElement element && element._resources?.Contains(resourceKey) == true)
            {
                return element._resources[resourceKey];
            }
        }

        return Application.Current?.TryFindResource(resourceKey);
    }

    public object? FindName(string name) => HeadlessXaml.FindName(this, name);

    public void SetResourceReference(DependencyProperty dp, object name)
    {
        var resource = TryFindResource(name);
        if (resource is not null)
        {
            HeadlessXaml.TrySetProperty(this, dp.Name, resource);
        }
    }

    public Binding? GetBindingExpressionBinding(DependencyProperty dp)
        => _bindings.TryGetValue(dp.Name, out var binding) ? binding as Binding : null;

    public BindingExpression? GetBindingExpression(DependencyProperty dp)
        => _bindings.TryGetValue(dp.Name, out var binding) ? new BindingExpression(this, dp, binding) : null;

    public BindingExpressionBase SetBinding(DependencyProperty dp, BindingBase binding)
    {
        _bindings[dp.Name] = binding;
        HeadlessBindings.Apply(this, dp.Name, binding);
        return new BindingExpression(this, dp, binding);
    }

    public BindingExpression SetBinding(DependencyProperty dp, string path)
    {
        var binding = new Binding(path);
        _bindings[dp.Name] = binding;
        HeadlessBindings.Apply(this, dp.Name, binding);
        return new BindingExpression(this, dp, binding);
    }

    internal IReadOnlyDictionary<string, BindingBase> HeadlessBindingsByProperty => _bindings;

    internal void SetHeadlessBinding(string property, BindingBase binding)
    {
        _bindings[property] = binding;
        HeadlessBindings.SetBinding(this, property, binding);
    }

    public void BringIntoView()
    {
    }

    public bool MoveFocus(TraversalRequest request) => true;

    protected virtual Size MeasureOverride(Size availableSize) => default;

    protected virtual Size ArrangeOverride(Size finalSize) => finalSize;

    public virtual void OnApplyTemplate()
    {
    }

    public bool ApplyTemplate() => true;

    public virtual void BeginInit()
    {
    }

    public virtual void EndInit() => Initialized?.Invoke(this, EventArgs.Empty);

    public void RegisterName(string name, object scopedElement) => HeadlessXaml.RegisterName(this, name, scopedElement);

    public void UnregisterName(string name)
    {
    }

    /// <summary>Called by the headless host when the element's window is shown.</summary>
    internal void RaiseLoaded()
    {
        if (IsLoaded)
        {
            return;
        }

        IsLoaded = true;
        RaiseHeadlessEvent(nameof(Loaded), new RoutedEventArgs(LoadedEvent, this));
    }

    internal void RaiseUnloaded()
    {
        if (!IsLoaded)
        {
            return;
        }

        IsLoaded = false;
        RaiseHeadlessEvent(nameof(Unloaded), new RoutedEventArgs(null, this));
    }
}

public enum FlowDirection
{
    LeftToRight,
    RightToLeft,
}

public sealed class Language
{
}

public sealed class TriggerCollection : Collection<TriggerBase>
{
}

public delegate void RequestBringIntoViewEventHandler(object sender, RequestBringIntoViewEventArgs e);

public class RequestBringIntoViewEventArgs : RoutedEventArgs
{
}

public class ResourceReferenceKeyNotFoundException : InvalidOperationException
{
    public ResourceReferenceKeyNotFoundException(string message, object resourceKey)
        : base(message)
    {
        Key = resourceKey;
    }

    public object Key { get; }
}

public sealed class StartupEventArgs : EventArgs
{
    public string[] Args { get; init; } = [];
}

public sealed class ExitEventArgs : EventArgs
{
    public int ApplicationExitCode { get; set; }
}

public delegate void StartupEventHandler(object sender, StartupEventArgs e);

public delegate void ExitEventHandler(object sender, ExitEventArgs e);

public sealed class WindowCollection : IEnumerable<Window>
{
    private readonly List<Window> _windows = new();

    public int Count
    {
        get
        {
            lock (_windows)
            {
                return _windows.Count;
            }
        }
    }

    public Window this[int index]
    {
        get
        {
            lock (_windows)
            {
                return _windows[index];
            }
        }
    }

    internal void Add(Window window)
    {
        lock (_windows)
        {
            if (!_windows.Contains(window))
            {
                _windows.Add(window);
            }
        }
    }

    internal void Remove(Window window)
    {
        lock (_windows)
        {
            _windows.Remove(window);
        }
    }

    public IEnumerator<Window> GetEnumerator()
    {
        lock (_windows)
        {
            return _windows.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class Application : DispatcherObject
{
    private static Application? _current;
    private bool _shuttingDown;
    private ResourceDictionary _resources = new();

    public Application()
    {
        _current = this;
    }

    public static Application Current => _current!;

    public Window? MainWindow { get; set; }

    public WindowCollection Windows { get; } = new();

    public ShutdownMode ShutdownMode { get; set; } = ShutdownMode.OnMainWindowClose;

    public Uri? StartupUri { get; set; }

    public IDictionary Properties { get; } = new Hashtable();

    public ResourceDictionary Resources
    {
        get => _resources;
        set => _resources = value;
    }

    public event DispatcherUnhandledExceptionEventHandler? DispatcherUnhandledException
    {
        add => Dispatcher.UnhandledException += value;
        remove => Dispatcher.UnhandledException -= value;
    }

    public event StartupEventHandler? Startup;

    public event ExitEventHandler? Exit;

    public event EventHandler? Activated;

    public event EventHandler? Deactivated;

    public object FindResource(object resourceKey)
        => TryFindResource(resourceKey) ?? throw new ResourceReferenceKeyNotFoundException($"'{resourceKey}' resource not found.", resourceKey);

    public object? TryFindResource(object resourceKey)
        => _resources.Contains(resourceKey) ? _resources[resourceKey] : HeadlessResources.Find(resourceKey);

    /// <summary>Raised by <see cref="Shutdown()"/>; the headless host stops when it fires.</summary>
    public static event Action<int>? HeadlessShutdownRequested;

    public void Shutdown() => Shutdown(0);

    public void Shutdown(int exitCode)
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;

        // As in WPF, shutting down closes every window that is still open.
        foreach (var window in Windows.Reverse().ToList())
        {
            window.HeadlessForceClose();
        }

        OnExit(new ExitEventArgs { ApplicationExitCode = exitCode });
        HeadlessShutdownRequested?.Invoke(exitCode);
    }

    protected virtual void OnStartup(StartupEventArgs e) => Startup?.Invoke(this, e);

    protected virtual void OnExit(ExitEventArgs e) => Exit?.Invoke(this, e);

    public bool IsShuttingDown => _shuttingDown;

    internal void RaiseActivated() => Activated?.Invoke(this, EventArgs.Empty);

    internal void RaiseDeactivated() => Deactivated?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A headless window. Showing it registers it with <see cref="HeadlessWindows"/> so the web UI can render
/// it; <see cref="ShowDialog"/> runs a nested dispatcher frame until the window closes, exactly the
/// blocking contract WPF code-behind expects.
/// </summary>
public class Window : Controls.ContentControl
{
    private DispatcherFrame? _modalFrame;
    private bool? _dialogResult;
    private bool _closed;

    public Window()
    {
        Visibility = Visibility.Collapsed;
    }

    public string Title { get => Get(string.Empty); set => Set(value); }
    public Window? Owner { get => Get<Window?>(null); set => Set(value); }
    public WindowState WindowState
    {
        get => Get(WindowState.Normal);
        set
        {
            if (Set(value))
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public WindowStartupLocation WindowStartupLocation { get => Get(WindowStartupLocation.Manual); set => Set(value); }
    public SizeToContent SizeToContent { get => Get(SizeToContent.Manual); set => Set(value); }
    public ResizeMode ResizeMode { get => Get(ResizeMode.CanResize); set => Set(value); }
    public WindowStyle WindowStyle { get => Get(WindowStyle.SingleBorderWindow); set => Set(value); }
    public bool ShowInTaskbar { get => Get(true); set => Set(value); }
    public bool ShowActivated { get => Get(true); set => Set(value); }
    public bool Topmost { get => Get(false); set => Set(value); }
    public bool AllowsTransparency { get => Get(false); set => Set(value); }
    public double Left { get => Get(0d); set => Set(value); }
    public double Top { get => Get(0d); set => Set(value); }
    public Media.ImageSource? Icon { get => Get<Media.ImageSource?>(null); set => Set(value); }
    public Rect RestoreBounds => new(Left, Top, ActualWidth, ActualHeight);
    public bool IsActive => IsShown;
    public bool IsShown { get; private set; }
    public bool IsModal => _modalFrame is not null;
    public bool IsClosed => _closed;

    public bool? DialogResult
    {
        get => _dialogResult;
        set
        {
            _dialogResult = value;
            if (_modalFrame is not null)
            {
                Close();
            }
        }
    }

    public event EventHandler? SourceInitialized;
    public event EventHandler? ContentRendered;
    public event EventHandler? Activated;
    public event EventHandler? Deactivated;
    public event EventHandler? StateChanged;
    public event EventHandler? LocationChanged;
    public event EventHandler? Closed;
    public event CancelEventHandler? Closing { add => AddNamed(nameof(Closing), value); remove => RemoveNamed(nameof(Closing), value); }

    public static Window? GetWindow(DependencyObject dependencyObject)
    {
        for (var current = dependencyObject; current is not null; current = (current as UIElement)?.HeadlessParent)
        {
            if (current is Window window)
            {
                return window;
            }
        }

        return null;
    }

    public void Show()
    {
        VerifyAccess();
        if (_closed)
        {
            throw new InvalidOperationException("Cannot show a window after it has been closed.");
        }

        if (IsShown)
        {
            return;
        }

        IsShown = true;
        Visibility = Visibility.Visible;
        Application.Current?.Windows.Add(this);
        if (Application.Current is { MainWindow: null } application)
        {
            application.MainWindow = this;
        }

        OnSourceInitialized(EventArgs.Empty);
        SourceInitialized?.Invoke(this, EventArgs.Empty);
        HeadlessWindows.Opened(this);
        HeadlessXaml.RaiseLoadedTree(this);
        Activated?.Invoke(this, EventArgs.Empty);
        OnContentRendered(EventArgs.Empty);
        ContentRendered?.Invoke(this, EventArgs.Empty);
    }

    public bool? ShowDialog()
    {
        VerifyAccess();
        _modalFrame = new DispatcherFrame();
        Show();
        if (!_closed)
        {
            Dispatcher.PushFrame(_modalFrame);
        }

        _modalFrame = null;
        return _dialogResult;
    }

    public void Close()
    {
        if (_closed)
        {
            return;
        }

        if (!CheckAccess())
        {
            Dispatcher.Invoke(Close);
            return;
        }

        var closing = new CancelEventArgs();
        OnClosing(closing);
        RaiseHeadlessEvent(nameof(Closing), closing);
        if (closing.Cancel)
        {
            return;
        }

        _closed = true;
        IsShown = false;
        Visibility = Visibility.Collapsed;
        HeadlessXaml.RaiseUnloadedTree(this);
        Application.Current?.Windows.Remove(this);
        HeadlessWindows.ClosedWindow(this);
        OnClosed(EventArgs.Empty);
        Closed?.Invoke(this, EventArgs.Empty);
        if (_modalFrame is not null)
        {
            _modalFrame.Continue = false;
        }

        if (Application.Current is { } application && ReferenceEquals(application.MainWindow, this) &&
            application.ShutdownMode == ShutdownMode.OnMainWindowClose)
        {
            application.Shutdown();
        }
    }

    /// <summary>Closes without raising Closing (host shutdown), releasing a modal caller.</summary>
    public void HeadlessForceClose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        IsShown = false;
        Visibility = Visibility.Collapsed;
        HeadlessXaml.RaiseUnloadedTree(this);
        Application.Current?.Windows.Remove(this);
        HeadlessWindows.ClosedWindow(this);
        OnClosed(EventArgs.Empty);
        Closed?.Invoke(this, EventArgs.Empty);
        if (_modalFrame is not null)
        {
            _modalFrame.Continue = false;
        }
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    public bool Activate()
    {
        HeadlessWindows.Activate(this);
        return true;
    }

    public void DragMove()
    {
    }

    protected virtual void OnClosing(CancelEventArgs e)
    {
    }

    protected virtual void OnClosed(EventArgs e)
    {
    }

    protected virtual void OnSourceInitialized(EventArgs e)
    {
    }

    protected virtual void OnContentRendered(EventArgs e)
    {
    }
}
