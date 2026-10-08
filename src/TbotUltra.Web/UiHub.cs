using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Headless;
using System.Windows.Input;
using System.Windows.Threading;

namespace TbotUltra.Web;

/// <summary>
/// Connects browsers to the headless UI over WebSockets: pushes a snapshot whenever the element tree
/// changes (debounced, and at least every couple of seconds for values that change without notifying),
/// and replays user input — clicks, typing, selection, dialog answers — on the UI dispatcher through the
/// same events a WPF user would raise.
/// </summary>
public sealed class UiHub : BackgroundService
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(2);

    private readonly UiIds _ids = new();
    private readonly List<Client> _clients = new();
    private readonly SemaphoreSlim _changed = new(0, int.MaxValue);
    private readonly ILogger<UiHub> _logger;
    private string _lastSnapshot = string.Empty;
    private int _dirty;

    private readonly FileBridge _files;

    public UiHub(ILogger<UiHub> logger, FileBridge files)
    {
        _logger = logger;
        _files = files;
        files.Changed += MarkDirty;
        files.DownloadReady += url => Broadcast(new JsonObject { ["download"] = url }.ToJsonString());
        HeadlessXaml.Changed += (_, _) => MarkDirty();
        HeadlessWindows.WindowOpened += _ => MarkDirty();
        HeadlessWindows.WindowClosed += _ => MarkDirty();
        HeadlessDialogs.MessageShown += _ => MarkDirty();
        HeadlessDialogs.MessageClosed += _ => MarkDirty();
        HeadlessDialogs.IsAttended = () =>
        {
            lock (_clients)
            {
                return _clients.Count > 0;
            }
        };
        Clipboard.HeadlessTextCopied += text => Broadcast(new JsonObject { ["clip"] = text }.ToJsonString());
    }

    public async Task AcceptAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var client = new Client(socket);
        lock (_clients)
        {
            _clients.Add(client);
        }

        try
        {
            var snapshot = await BuildSnapshotAsync();
            await client.SendAsync(snapshot, cancellationToken);
            await ReceiveLoopAsync(client, cancellationToken);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
        }
        finally
        {
            lock (_clients)
            {
                _clients.Remove(client);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _changed.WaitAsync(Heartbeat, stoppingToken).ConfigureAwait(false);
            await Task.Delay(Debounce, stoppingToken).ConfigureAwait(false);
            while (_changed.CurrentCount > 0)
            {
                await _changed.WaitAsync(stoppingToken).ConfigureAwait(false);
            }

            Interlocked.Exchange(ref _dirty, 0);
            bool hasClients;
            lock (_clients)
            {
                hasClients = _clients.Count > 0;
            }

            if (!hasClients)
            {
                continue;
            }

            try
            {
                var snapshot = await BuildSnapshotAsync();
                if (snapshot != _lastSnapshot)
                {
                    _lastSnapshot = snapshot;
                    Broadcast(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Building the UI snapshot failed.");
            }
        }
    }

    public void OpenInBrowser(string url) => Broadcast(new JsonObject { ["open"] = url }.ToJsonString());

    private void MarkDirty()
    {
        if (Interlocked.Exchange(ref _dirty, 1) == 0)
        {
            _changed.Release();
        }
    }

    private async Task<string> BuildSnapshotAsync()
    {
        var snapshot = await Dispatcher.Main.InvokeAsync(() => new UiSnapshot(_ids).Build()).Task.ConfigureAwait(false);
        snapshot["files"] = new JsonArray(_files.Pending.Select(request => (JsonNode)new JsonObject
        {
            ["id"] = request.Id.ToString(),
            ["title"] = request.Title,
            ["accept"] = request.Accept,
        }).ToArray());
        return snapshot.ToJsonString();
    }

    private void Broadcast(string payload)
    {
        List<Client> clients;
        lock (_clients)
        {
            clients = _clients.ToList();
        }

        foreach (var client in clients)
        {
            _ = client.SendAsync(payload, CancellationToken.None);
        }
    }

    private async Task ReceiveLoopAsync(Client client, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var message = new MemoryStream();
        while (client.Socket.State == WebSocketState.Open)
        {
            var result = await client.Socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            try
            {
                var input = JsonNode.Parse(json)?.AsObject();
                if (input is not null)
                {
                    // Not awaited: input that opens a modal dialog runs until the dialog closes, and the
                    // connection must keep delivering input meanwhile. The dispatcher keeps the order.
                    _ = Dispatcher.Main.InvokeAsync(() =>
                    {
                        try
                        {
                            Apply(input);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Applying UI input failed: {Input}", json);
                        }
                        finally
                        {
                            MarkDirty();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Applying UI input failed: {Input}", json);
            }

            MarkDirty();
        }
    }

    /// <summary>Applies one browser input on the UI thread.</summary>
    private void Apply(JsonObject input)
    {
        var kind = input["e"]?.GetValue<string>();
        if (kind == "filecancel" && Guid.TryParse(input["id"]?.GetValue<string>(), out var fileRequest))
        {
            _files.Cancel(fileRequest);
            return;
        }

        if (kind == "msg")
        {
            var id = input["id"]?.GetValue<string>();
            var result = Enum.Parse<MessageBoxResult>(input["r"]?.GetValue<string>() ?? "Cancel");
            HeadlessDialogs.Open.FirstOrDefault(message => message.Id.ToString() == id)?.Answer(result);
            return;
        }

        var target = input["i"] is { } idNode ? _ids.Find(idNode.GetValue<int>()) : null;
        switch (kind, target)
        {
            case ("click", ButtonBase button):
                button.PerformClick();
                break;
            case ("click", UIElement element):
                element.RaiseHeadlessEvent(nameof(UIElement.MouseLeftButtonDown), new MouseButtonEventArgs { Source = element });
                element.RaiseHeadlessEvent(nameof(UIElement.MouseLeftButtonUp), new MouseButtonEventArgs { Source = element });
                break;
            case ("dblclick", Control control):
                control.RaiseHeadlessEvent(nameof(Control.MouseDoubleClick), new MouseButtonEventArgs { Source = control, ClickCount = 2 });
                break;
            case ("text", TextBox textBox):
                textBox.Text = input["v"]?.GetValue<string>() ?? string.Empty;
                break;
            case ("text", PasswordBox passwordBox):
                passwordBox.Password = input["v"]?.GetValue<string>() ?? string.Empty;
                break;
            case ("text", ComboBox comboBox):
                comboBox.Text = input["v"]?.GetValue<string>() ?? string.Empty;
                break;
            case ("blur", UIElement element):
                element.RaiseHeadlessEvent(nameof(UIElement.LostFocus), new RoutedEventArgs(null, element));
                break;
            case ("key", UIElement element):
                var key = Enum.TryParse<Key>(input["k"]?.GetValue<string>(), out var parsedKey) ? parsedKey : Key.None;
                element.RaiseHeadlessEvent(nameof(UIElement.PreviewKeyDown), new KeyEventArgs { Key = key, Source = element });
                element.RaiseHeadlessEvent(nameof(UIElement.KeyDown), new KeyEventArgs { Key = key, Source = element });
                break;
            case ("check", ToggleButton toggle):
                toggle.PerformClick();
                break;
            case ("select", ComboBox comboBox):
                comboBox.SelectedIndex = input["v"]?.GetValue<int>() ?? -1;
                comboBox.RaiseHeadlessEvent("DropDownClosed", EventArgs.Empty);
                break;
            case ("select", TabControl tabControl):
                var tabId = input["v"]?.GetValue<int>() ?? -1;
                if (_ids.Find(tabId) is TabItem tab)
                {
                    tabControl.HeadlessSelect(tab);
                }

                break;
            case ("rows", DataGrid dataGrid):
                var indexes = input["v"]?.AsArray().Select(index => index!.GetValue<int>()).ToList() ?? new List<int>();
                dataGrid.HeadlessSelectItems(indexes.Where(index => index >= 0 && index < dataGrid.Items.Count).Select(index => dataGrid.Items[index]));
                break;
            case ("rows", ListBox listBox):
                var listIndexes = input["v"]?.AsArray().Select(index => index!.GetValue<int>()).ToList() ?? new List<int>();
                var selected = listIndexes.Where(index => index >= 0 && index < listBox.Items.Count).Select(index => listBox.Items[index]).ToList();
                listBox.HeadlessSelect(selected.FirstOrDefault());
                foreach (var item in selected.Skip(1))
                {
                    listBox.SelectedItems.Add(item);
                }

                break;
            case ("cellcheck", DataGrid grid):
                var row = input["r"]?.GetValue<int>() ?? -1;
                var column = input["c"]?.GetValue<int>() ?? -1;
                var visibleColumns = grid.Columns.Where(candidate => candidate.Visibility == Visibility.Visible).ToList();
                if (row >= 0 && row < grid.Items.Count && column >= 0 && column < visibleColumns.Count &&
                    visibleColumns[column] is DataGridCheckBoxColumn { Binding: System.Windows.Data.Binding binding })
                {
                    var item = grid.Items[row];
                    var current = HeadlessBindings.Evaluate(binding, item) as bool?;
                    var property = binding.Path?.Path is { } path ? item.GetType().GetProperty(path) : null;
                    property?.SetValue(item, current != true);
                }

                break;
            case ("expand", Expander expander):
                expander.IsExpanded = !expander.IsExpanded;
                expander.RaiseHeadlessEvent(expander.IsExpanded ? "Expanded" : "Collapsed", new RoutedEventArgs(null, expander));
                break;
            case ("close", Window window):
                window.Close();
                break;
        }
    }

    private sealed class Client
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public Client(WebSocket socket)
        {
            Socket = socket;
        }

        public WebSocket Socket { get; }

        public async Task SendAsync(string payload, CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    await Socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
