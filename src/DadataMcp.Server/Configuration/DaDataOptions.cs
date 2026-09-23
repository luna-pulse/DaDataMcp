namespace DadataMcp.Server.Configuration;

/// <summary>
/// Ключи DaData из файла .env в корне проекта (или из переменных окружения процесса).
/// Файл .env не коммитится.
/// </summary>
public sealed class DaDataOptions
{
    public const string ApiKeyVariable = "DADATA_API_KEY";
    public const string SecretKeyVariable = "DADATA_SECRET_KEY";

    public required string ApiKey { get; init; }
    public required string SecretKey { get; init; }

    public static DaDataOptions FromProcess()
    {
        LoadDotEnv();

        var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        var secretKey = Environment.GetEnvironmentVariable(SecretKeyVariable);

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Задайте DADATA_API_KEY и DADATA_SECRET_KEY в файле .env в корне проекта " +
                "(образец — .env.example).");
        }

        return new DaDataOptions
        {
            ApiKey = apiKey.Trim(),
            SecretKey = secretKey.Trim()
        };
    }

    private static void LoadDotEnv()
    {
        var path = FindEnvFile();
        if (path is null)
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            if (key.StartsWith("export ", StringComparison.Ordinal))
            {
                key = key["export ".Length..].Trim();
            }

            if (key.Length == 0 || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindEnvFile()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
