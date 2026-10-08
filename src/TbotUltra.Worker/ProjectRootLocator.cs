namespace TbotUltra.Worker;

public static class ProjectRootLocator
{
    /// <summary>Overrides the search, e.g. a Docker data volume kept apart from the app binaries.</summary>
    public const string ProjectRootVariable = "TBOT_PROJECT_ROOT";

    public static string FindProjectRoot(string? startPath = null)
    {
        var configuredRoot = Environment.GetEnvironmentVariable(ProjectRootVariable);
        if (startPath is null && !string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var current = new DirectoryInfo(startPath ?? AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "TbotUltra.sln");
            var configPath = Path.Combine(current.FullName, "config", "bot.json");
            if (File.Exists(solutionPath) || File.Exists(configPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate project root (missing TbotUltra.sln or config/bot.json).");
    }
}
