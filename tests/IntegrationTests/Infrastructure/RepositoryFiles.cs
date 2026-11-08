namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Reads repository files so tests share one source of truth with Compose.</summary>
internal static class RepositoryFiles
{
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root (*.slnx) not found.");
    }

    /// <summary>Value of KEY=value in .env.example.</summary>
    public static string EnvExample(string key)
    {
        var prefix = key + "=";
        var line = File.ReadLines(Path.Combine(Root(), ".env.example"))
            .FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));

        return line?[prefix.Length..].Trim()
            ?? throw new InvalidOperationException($"{key} not found in .env.example.");
    }
}
