namespace Microsoft.Win32;

/// <summary>
/// File dialogs for the headless host. A web UI can register <see cref="HeadlessHandler"/> to let the
/// user pick (upload) or save (download) a file; without one the dialog reports cancel.
/// </summary>
public abstract class FileDialog
{
    /// <summary>Answers a dialog; returns true and sets <see cref="FileName"/> when the user confirmed.</summary>
    public static Func<FileDialog, bool>? HeadlessHandler { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string[] FileNames => string.IsNullOrEmpty(FileName) ? [] : [FileName];
    public string Filter { get; set; } = string.Empty;
    public int FilterIndex { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string DefaultExt { get; set; } = string.Empty;
    public string InitialDirectory { get; set; } = string.Empty;
    public bool AddExtension { get; set; } = true;
    public bool CheckFileExists { get; set; }
    public bool CheckPathExists { get; set; } = true;
    public bool RestoreDirectory { get; set; }
    public bool ValidateNames { get; set; } = true;
    public string SafeFileName => System.IO.Path.GetFileName(FileName);

    public bool? ShowDialog() => HeadlessHandler?.Invoke(this) ?? false;

    public bool? ShowDialog(System.Windows.Window? owner) => ShowDialog();
}

public sealed class OpenFileDialog : FileDialog
{
    public bool Multiselect { get; set; }

    public OpenFileDialog()
    {
        CheckFileExists = true;
    }
}

public sealed class SaveFileDialog : FileDialog
{
    public bool OverwritePrompt { get; set; } = true;
    public bool CreatePrompt { get; set; }
}

public sealed class OpenFolderDialog
{
    public string FolderName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public bool? ShowDialog() => false;
}
