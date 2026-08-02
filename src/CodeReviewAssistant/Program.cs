using CodeReviewAssistant.Analysis;
using CodeReviewAssistant.Configuration;
using CodeReviewAssistant.Reporting;

if (!TryParseArguments(args, out var targetArgument, out var configArgument))
{
    PrintUsage();
    return args.Length == 1 && args[0] is "--help" or "-h" ? 0 : 2;
}

var path = Path.GetFullPath(targetArgument);
var files = SourceFileDiscovery.Find(path).ToArray();

if (files.Length == 0)
{
    Console.Error.WriteLine($"No C# source files found at '{path}'.");
    return 2;
}

AnalyzerConfiguration configuration;
try
{
    var configPath = configArgument is null
        ? ConfigurationLoader.Find(path)
        : Path.GetFullPath(configArgument);
    configuration = configPath is null
        ? new AnalyzerConfiguration()
        : ConfigurationLoader.Load(configPath);
}
catch (ConfigurationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

var analyzer = new SourceAnalyzer(configuration);
var results = new List<FileReview>();

foreach (var file in files)
{
    try
    {
        results.Add(analyzer.Review(file, await File.ReadAllTextAsync(file)));
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Could not read '{file}': {exception.Message}");
        return 2;
    }
}

Console.Write(new ConsoleReportFormatter().Format(results));
return 0;

static bool TryParseArguments(string[] arguments, out string target, out string? config)
{
    target = arguments.FirstOrDefault() ?? string.Empty;
    config = null;

    if (arguments.Length == 1 && arguments[0] is not "--help" and not "-h")
    {
        return true;
    }

    if (arguments.Length == 3 && arguments[1] == "--config")
    {
        config = arguments[2];
        return true;
    }

    return false;
}

static void PrintUsage()
{
    Console.WriteLine("Usage: review <file-or-directory> [--config <configuration-file>]");
    Console.WriteLine("Reviews C# source files without sending code to an external service.");
}
