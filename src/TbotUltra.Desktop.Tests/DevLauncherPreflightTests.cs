using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DevLauncherPreflightTests
{
    [Fact]
    public void VbsLauncher_GatesAppStartOnCiEquivalentPreflight()
    {
        var root = FindRepositoryRoot();
        var launcher = File.ReadAllText(Path.Combine(root, "Start_Tbot_UI.vbs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "Verify-DevLaunch.ps1"));

        Assert.Contains("UpdateLatestModified srcDesktopTests, latestCheckSource", launcher);
        Assert.Contains("UpdateLatestModified srcWorkerTests, latestCheckSource", launcher);
        Assert.Contains("For Each sourceFile In folder.Files", launcher);
        Assert.Contains("-WindowStyle Hidden -File", launcher);
        Assert.Contains("ReadPreflightFailure(fso, checkLogPath)", launcher);
        Assert.True(launcher.IndexOf("If checkExitCode <> 0 Then", StringComparison.Ordinal)
            < launcher.IndexOf("LaunchAndActivate shell, exePath", StringComparison.Ordinal));

        Assert.Contains("Invoke-PreflightStep \"Release build\"", preflight);
        Assert.Contains("Invoke-PreflightStep \"Worker tests\"", preflight);
        Assert.Contains("Invoke-PreflightStep \"Desktop tests\"", preflight);
        Assert.Contains("-WindowStyle Hidden -Wait -PassThru", preflight);
        Assert.Contains("exit $process.ExitCode", preflight);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate TbotUltra.sln from the test output directory.");
    }
}
