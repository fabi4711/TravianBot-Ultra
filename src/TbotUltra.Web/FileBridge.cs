using System.Windows.Headless;
using Microsoft.Win32;

namespace TbotUltra.Web;

/// <summary>
/// Turns the desktop app's file dialogs into browser transfers. An open dialog asks the browser for an
/// upload and returns the stored file's path; a save dialog returns a path under the export folder and
/// offers the file as a download once the app has written it.
/// </summary>
public sealed class FileBridge
{
    private readonly string _uploadDirectory;
    private readonly string _exportDirectory;
    private readonly List<FileRequest> _pending = new();

    public FileBridge(string projectRoot)
    {
        _uploadDirectory = Path.Combine(projectRoot, "web-transfer", "uploads");
        _exportDirectory = Path.Combine(projectRoot, "web-transfer", "exports");
        FileDialog.HeadlessHandler = Show;
    }

    /// <summary>Raised when a pending upload request appears or goes away.</summary>
    public event Action? Changed;

    /// <summary>Raised with a download URL once an exported file is ready.</summary>
    public event Action<string>? DownloadReady;

    public IReadOnlyList<FileRequest> Pending
    {
        get
        {
            lock (_pending)
            {
                return _pending.ToList();
            }
        }
    }

    public string ExportDirectory => _exportDirectory;

    public async Task<bool> CompleteUploadAsync(Guid id, IFormFile file)
    {
        var request = Pending.FirstOrDefault(candidate => candidate.Id == id);
        if (request is null)
        {
            return false;
        }

        Directory.CreateDirectory(_uploadDirectory);
        var path = Path.Combine(_uploadDirectory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{SafeName(file.FileName)}");
        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream);
        }

        request.Completion.TrySetResult(path);
        return true;
    }

    public void Cancel(Guid id) => Pending.FirstOrDefault(candidate => candidate.Id == id)?.Completion.TrySetResult(null);

    private bool Show(FileDialog dialog)
    {
        if (dialog is SaveFileDialog)
        {
            Directory.CreateDirectory(_exportDirectory);
            var name = SafeName(string.IsNullOrWhiteSpace(dialog.FileName) ? "export" + dialog.DefaultExt : Path.GetFileName(dialog.FileName));
            var path = Path.Combine(_exportDirectory, name);
            dialog.FileName = path;
            _ = OfferWhenWrittenAsync(path, File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue);
            return true;
        }

        if (!HeadlessDialogs.IsAttended())
        {
            return false;
        }

        var request = new FileRequest(dialog.Title, AcceptFrom(dialog.Filter));
        lock (_pending)
        {
            _pending.Add(request);
        }

        Changed?.Invoke();
        HeadlessDialogs.WaitOnDispatcher(request.Completion.Task);
        lock (_pending)
        {
            _pending.Remove(request);
        }

        Changed?.Invoke();
        var uploaded = request.Completion.Task.IsCompletedSuccessfully ? request.Completion.Task.Result : null;
        if (uploaded is null)
        {
            return false;
        }

        dialog.FileName = uploaded;
        return true;
    }

    // The app writes the file after the dialog returns; offer it once it exists and has stopped growing.
    private async Task OfferWhenWrittenAsync(string path, DateTime previousWrite)
    {
        long lastLength = -1;
        for (var attempt = 0; attempt < 600; attempt++)
        {
            await Task.Delay(500);
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) <= previousWrite)
            {
                continue;
            }

            var length = new FileInfo(path).Length;
            if (length > 0 && length == lastLength)
            {
                DownloadReady?.Invoke("api/files/" + Uri.EscapeDataString(Path.GetFileName(path)));
                return;
            }

            lastLength = length;
        }
    }

    private static string AcceptFrom(string filter)
    {
        // "Name (*.a.json)|*.a.json|JSON (*.json)|*.json" → ".json"
        var patterns = filter.Split('|').Where((_, index) => index % 2 == 1)
            .SelectMany(pattern => pattern.Split(';'))
            .Select(pattern => Path.GetExtension(pattern.Trim()))
            .Where(extension => !string.IsNullOrEmpty(extension) && extension != ".*")
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(",", patterns);
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '\\', ':' }).ToHashSet();
        var cleaned = new string(Path.GetFileName(name).Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }

    public sealed class FileRequest
    {
        public FileRequest(string title, string accept)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Choose a file" : title;
            Accept = accept;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; }
        public string Accept { get; }
        public TaskCompletionSource<string?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
