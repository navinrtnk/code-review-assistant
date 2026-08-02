using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeReviewAssistant.Configuration;

public static class ConfigurationLoader
{
    public const string DefaultFileName = ".codereview.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static AnalyzerConfiguration Load(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new ConfigurationException($"Invalid configuration in '{path}': {exception.Message}", exception);
        }
        catch (IOException exception)
        {
            throw new ConfigurationException($"Could not read configuration '{path}': {exception.Message}", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ConfigurationException($"Could not read configuration '{path}': {exception.Message}", exception);
        }
    }

    public static AnalyzerConfiguration Parse(string json)
    {
        var configuration = JsonSerializer.Deserialize<AnalyzerConfiguration>(json, JsonOptions)
            ?? throw new ConfigurationException("Configuration cannot be null.");

        Validate(configuration);
        return configuration;
    }

    public static string? Find(string targetPath)
    {
        var directory = File.Exists(targetPath)
            ? Path.GetDirectoryName(Path.GetFullPath(targetPath))
            : Path.GetFullPath(targetPath);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private static void Validate(AnalyzerConfiguration configuration)
    {
        ValidateRule("CRA001", configuration.Cra001);
        ValidateRule("CRA002", configuration.Cra002);
        ValidateRule("CRA003", configuration.Cra003);

        if (configuration.Cra001.MaxLines < 1)
        {
            throw new ConfigurationException("CRA001 maxLines must be at least 1.");
        }

        if (configuration.Cra002.AllowedNames is null ||
            configuration.Cra002.AllowedNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ConfigurationException("CRA002 allowedNames must contain only non-empty names.");
        }
    }

    private static void ValidateRule(string ruleId, RuleConfiguration rule)
    {
        if (rule is null)
        {
            throw new ConfigurationException($"{ruleId} configuration cannot be null.");
        }

        if (rule.Penalty < 0)
        {
            throw new ConfigurationException($"{ruleId} penalty cannot be negative.");
        }
    }
}

public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }

    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
