using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace System.Windows.Threading;

public enum DispatcherPriority
{
    Invalid = -1,
    Inactive = 0,
    SystemIdle = 1,
    ApplicationIdle = 2,
    ContextIdle = 3,
    Background = 4,
    Input = 5,
    Loaded = 6,
    Render = 7,
    DataBind = 8,
    Normal = 9,
    Send = 10,
}

public enum DispatcherOperationStatus
{
    Pending,
    Aborted,
    Completed,
    Executing,
}

public delegate void DispatcherUnhandledExceptionEventHandler(object sender, DispatcherUnhandledExceptionEventArgs e);

public sealed class DispatcherUnhandledExceptionEventArgs : EventArgs
{
    internal DispatcherUnhandledExceptionEventArgs(Dispatcher dispatcher, Exception exception)
    {
        Dispatcher = dispatcher;
        Exception = exception;
    }

    public Dispatcher Dispatcher { get; }
    public Exception Exception { get; }
    public bool Handled { get; set; }
}

/// <summary>
/// The single UI thread of the headless host. Like WPF it owns every element, serializes all work posted to
/// it, installs a <see cref="SynchronizationContext"/> so <c>await</c> resumes on it, and supports nested
/// frames so modal calls (<c>ShowDialog</c>, confirmation dialogs) can block their caller while the loop
/// keeps processing — the web UI answers them from another thread.
/// </summary>
public sealed class Dispatcher
{
    private static readonly Lazy<Dispatcher> MainInstance = new(() => new Dispatcher("Headless UI"), LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly BlockingCollection<WorkItem> _queue = new();
    private readonly Thread _thread;

    private Dispatcher(string name)
    {
        var started = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(this));
            started.Set();
            RunFrame(new DispatcherFrame(exitWhenRequested: false));
        })
        {
            IsBackground = true,
            Name = name,
        };
        _thread.Start();
        started.Wait();
    }

    /// <summary>The headless UI dispatcher. Created on first use.</summary>
    public static Dispatcher Main => MainInstance.Value;

    /// <summary>WPF returns the calling thread's dispatcher; the headless host has exactly one.</summary>
    public static Dispatcher CurrentDispatcher => Main;

    public Thread Thread => _thread;

    public bool HasShutdownStarted { get; private set; }

    public bool HasShutdownFinished => HasShutdownStarted;

    public event DispatcherUnhandledExceptionEventHandler? UnhandledException;

    public bool CheckAccess() => Thread.CurrentThread == _thread;

    public void VerifyAccess()
    {
        if (!CheckAccess())
        {
            throw new InvalidOperationException("The calling thread cannot access this object because a different thread owns it.");
        }
    }

    public void Invoke(Action callback) => Invoke(callback, DispatcherPriority.Send);

    public void Invoke(Action callback, DispatcherPriority priority) => Invoke(callback, priority, CancellationToken.None);

    public void Invoke(Action callback, DispatcherPriority priority, CancellationToken cancellationToken)
    {
        if (CheckAccess())
        {
            callback();
            return;
        }

        InvokeAsync(callback, priority, cancellationToken).Task.GetAwaiter().GetResult();
    }

    public TResult Invoke<TResult>(Func<TResult> callback) => Invoke(callback, DispatcherPriority.Send);

    public TResult Invoke<TResult>(Func<TResult> callback, DispatcherPriority priority)
        => CheckAccess() ? callback() : InvokeAsync(callback, priority).Task.GetAwaiter().GetResult();

    public object? Invoke(Delegate method, params object?[] args)
        => Invoke(() => InvokeDelegate(method, args));

    public object? Invoke(Delegate method, DispatcherPriority priority, params object?[] args)
        => Invoke(() => InvokeDelegate(method, args), priority);

    public DispatcherOperation InvokeAsync(Action callback)
        => InvokeAsync(callback, DispatcherPriority.Normal, CancellationToken.None);

    public DispatcherOperation InvokeAsync(Action callback, DispatcherPriority priority)
        => InvokeAsync(callback, priority, CancellationToken.None);

    public DispatcherOperation InvokeAsync(Action callback, DispatcherPriority priority, CancellationToken cancellationToken)
    {
        var operation = new DispatcherOperation(this, () =>
        {
            callback();
            return null;
        });
        Post(operation, priority, cancellationToken);
        return operation;
    }

    public DispatcherOperation<TResult> InvokeAsync<TResult>(Func<TResult> callback)
        => InvokeAsync(callback, DispatcherPriority.Normal, CancellationToken.None);

    public DispatcherOperation<TResult> InvokeAsync<TResult>(Func<TResult> callback, DispatcherPriority priority)
        => InvokeAsync(callback, priority, CancellationToken.None);

    public DispatcherOperation<TResult> InvokeAsync<TResult>(
        Func<TResult> callback,
        DispatcherPriority priority,
        CancellationToken cancellationToken)
    {
        var operation = new DispatcherOperation<TResult>(this, () => callback());
        Post(operation, priority, cancellationToken);
        return operation;
    }

    public DispatcherOperation BeginInvoke(Delegate method, params object?[] args)
        => BeginInvoke(method, DispatcherPriority.Normal, args);

    public DispatcherOperation BeginInvoke(DispatcherPriority priority, Delegate method)
        => BeginInvoke(method, priority);

    public DispatcherOperation BeginInvoke(DispatcherPriority priority, Delegate method, object? arg)
        => BeginInvoke(method, priority, arg);

    public DispatcherOperation BeginInvoke(Delegate method, DispatcherPriority priority, params object?[] args)
    {
        var operation = new DispatcherOperation(this, () => InvokeDelegate(method, args), captureExceptions: false);
        Post(operation, priority, CancellationToken.None);
        return operation;
    }

    public static DispatcherPriorityAwaitable Yield() => Yield(DispatcherPriority.Background);

    public static DispatcherPriorityAwaitable Yield(DispatcherPriority priority) => new(Main, priority);

    public void InvokeShutdown()
    {
        HasShutdownStarted = true;
        _queue.CompleteAdding();
    }

    public void BeginInvokeShutdown(DispatcherPriority priority) => InvokeShutdown();

    /// <summary>Runs the message loop on the UI thread until <paramref name="frame"/> stops continuing.</summary>
    public static void PushFrame(DispatcherFrame frame) => Main.RunFrame(frame);

    public static void ExitAllFrames()
    {
    }

    internal void Post(Action action)
        => Post(new DispatcherOperation(this, () =>
        {
            action();
            return null;
        }, captureExceptions: false), DispatcherPriority.Normal, CancellationToken.None);

    private void Post(DispatcherOperation operation, DispatcherPriority priority, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            operation.Abort();
            return;
        }

        if (HasShutdownStarted)
        {
            operation.Abort();
            return;
        }

        try
        {
            _queue.Add(new WorkItem(operation, cancellationToken), CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            operation.Abort();
        }
    }

    private void RunFrame(DispatcherFrame frame)
    {
        VerifyAccess();
        try
        {
            while (frame.Continue && !HasShutdownStarted)
            {
                if (!_queue.TryTake(out var item, frame.ExitWhenRequested ? 50 : Timeout.Infinite))
                {
                    if (_queue.IsAddingCompleted)
                    {
                        break;
                    }

                    continue;
                }

                if (item.CancellationToken.IsCancellationRequested)
                {
                    item.Operation.Abort();
                    continue;
                }

                try
                {
                    item.Operation.Execute();
                }
                catch (Exception ex)
                {
                    var args = new DispatcherUnhandledExceptionEventArgs(this, ex);
                    UnhandledException?.Invoke(this, args);
                    if (!args.Handled)
                    {
                        Console.Error.WriteLine($"[dispatcher] unhandled exception: {ex}");
                    }
                }
            }
        }
        finally
        {
        }
    }

    private static object? InvokeDelegate(Delegate method, object?[] args)
    {
        try
        {
            return method.DynamicInvoke(args);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private readonly record struct WorkItem(DispatcherOperation Operation, CancellationToken CancellationToken);
}

public sealed class DispatcherFrame
{
    public DispatcherFrame()
        : this(exitWhenRequested: true)
    {
    }

    public DispatcherFrame(bool exitWhenRequested)
    {
        ExitWhenRequested = exitWhenRequested;
    }

    internal bool ExitWhenRequested { get; }

    public bool Continue { get; set; } = true;
}

public class DispatcherOperation
{
    private readonly Func<object?> _work;
    private protected readonly TaskCompletionSource<object?> Completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal DispatcherOperation(Dispatcher dispatcher, Func<object?> work, bool captureExceptions = true)
    {
        Dispatcher = dispatcher;
        _work = work;
        _captureExceptions = captureExceptions;
    }

    // InvokeAsync/Invoke hand failures to the awaiting caller; BeginInvoke and posted continuations
    // surface them as unhandled dispatcher exceptions, matching WPF.
    private readonly bool _captureExceptions;

    public Dispatcher Dispatcher { get; }

    public DispatcherOperationStatus Status { get; private set; } = DispatcherOperationStatus.Pending;

    public Task Task => Completion.Task;

    public object? Result => Completion.Task.IsCompletedSuccessfully ? Completion.Task.Result : null;

    public event EventHandler? Completed;

    public TaskAwaiter GetAwaiter() => ((Task)Completion.Task).GetAwaiter();

    public DispatcherOperationStatus Wait()
    {
        if (!Dispatcher.CheckAccess())
        {
            try
            {
                Completion.Task.Wait();
            }
            catch (AggregateException)
            {
            }
        }

        return Status;
    }

    public bool Abort()
    {
        if (Status != DispatcherOperationStatus.Pending)
        {
            return false;
        }

        Status = DispatcherOperationStatus.Aborted;
        Completion.TrySetCanceled();
        return true;
    }

    internal void Execute()
    {
        if (Status != DispatcherOperationStatus.Pending)
        {
            return;
        }

        Status = DispatcherOperationStatus.Executing;
        try
        {
            var result = _work();
            Status = DispatcherOperationStatus.Completed;
            Completion.TrySetResult(result);
        }
        catch (Exception ex)
        {
            Status = DispatcherOperationStatus.Completed;
            Completion.TrySetException(ex);
            if (!_captureExceptions)
            {
                throw;
            }
        }
        finally
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed class DispatcherOperation<TResult> : DispatcherOperation
{
    internal DispatcherOperation(Dispatcher dispatcher, Func<TResult> work)
        : base(dispatcher, () => work())
    {
    }

    public new Task<TResult> Task => Completion.Task.ContinueWith(
        static task => (TResult)task.GetAwaiter().GetResult()!,
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);

    public new TResult Result => (TResult)base.Result!;

    public new TaskAwaiter<TResult> GetAwaiter() => Task.GetAwaiter();
}

public readonly struct DispatcherPriorityAwaitable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherPriority _priority;

    internal DispatcherPriorityAwaitable(Dispatcher dispatcher, DispatcherPriority priority)
    {
        _dispatcher = dispatcher;
        _priority = priority;
    }

    public DispatcherPriorityAwaiter GetAwaiter() => new(_dispatcher, _priority);
}

public readonly struct DispatcherPriorityAwaiter : INotifyCompletion
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherPriority _priority;

    internal DispatcherPriorityAwaiter(Dispatcher dispatcher, DispatcherPriority priority)
    {
        _dispatcher = dispatcher;
        _priority = priority;
    }

    public bool IsCompleted => false;

    public void GetResult()
    {
    }

    public void OnCompleted(Action continuation) => _dispatcher.InvokeAsync(continuation, _priority);
}

public sealed class DispatcherSynchronizationContext : SynchronizationContext
{
    private readonly Dispatcher _dispatcher;

    public DispatcherSynchronizationContext()
        : this(Dispatcher.Main)
    {
    }

    public DispatcherSynchronizationContext(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public override void Post(SendOrPostCallback d, object? state) => _dispatcher.Post(() => d(state));

    public override void Send(SendOrPostCallback d, object? state) => _dispatcher.Invoke(() => d(state));

    public override SynchronizationContext CreateCopy() => new DispatcherSynchronizationContext(_dispatcher);
}

/// <summary>Ticks on the UI thread, like WPF's timer. Backed by a thread-pool timer that posts each tick.</summary>
public class DispatcherTimer
{
    private readonly Dispatcher _dispatcher;
    private Timer? _timer;
    private TimeSpan _interval;
    private int _tickQueued;

    public DispatcherTimer()
        : this(DispatcherPriority.Background)
    {
    }

    public DispatcherTimer(DispatcherPriority priority)
        : this(priority, Dispatcher.Main)
    {
    }

    public DispatcherTimer(DispatcherPriority priority, Dispatcher dispatcher)
    {
        Priority = priority;
        _dispatcher = dispatcher;
    }

    public DispatcherTimer(TimeSpan interval, DispatcherPriority priority, EventHandler callback, Dispatcher dispatcher)
        : this(priority, dispatcher)
    {
        Interval = interval;
        Tick += callback;
        Start();
    }

    public event EventHandler? Tick;

    public DispatcherPriority Priority { get; }

    public Dispatcher Dispatcher => _dispatcher;

    public object? Tag { get; set; }

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _interval = value;
            if (IsEnabled)
            {
                Restart();
            }
        }
    }

    public bool IsEnabled
    {
        get => _timer is not null;
        set
        {
            if (value)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }
    }

    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        Restart();
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Restart()
    {
        _timer?.Dispose();
        var period = _interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : _interval;
        Timer? created = null;
        created = new Timer(_ =>
        {
            // Coalesce like WPF: a timer never has more than one tick waiting in the queue.
            if (Interlocked.Exchange(ref _tickQueued, 1) == 1)
            {
                return;
            }

            _dispatcher.InvokeAsync(() =>
            {
                Interlocked.Exchange(ref _tickQueued, 0);
                if (ReferenceEquals(_timer, created))
                {
                    Tick?.Invoke(this, EventArgs.Empty);
                }
            }, Priority);
        }, null, period, period);
        _timer = created;
    }
}

public class DispatcherObject
{
    public Dispatcher Dispatcher => Dispatcher.Main;

    public bool CheckAccess() => Dispatcher.CheckAccess();

    public void VerifyAccess() => Dispatcher.VerifyAccess();
}
