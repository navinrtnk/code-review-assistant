using CodeReviewAssistant.Analysis;
using CodeReviewAssistant.Configuration;
using CodeReviewAssistant.Input;
using CodeReviewAssistant.Reporting;

if (!TryParseArguments(args, out var targetArgument, out var configArgument))
{
    PrintUsage();
    return args.Length == 1 && args[0] is "--help" or "-h" ? 0 : 2;
}

var input = InputTargetDetector.Detect(targetArgument);

if (input.Kind == InputTargetKind.Missing)
{
    Console.Error.WriteLine($"Input path does not exist: '{input.Path}'.");
    return 2;
}

if (input.Kind == InputTargetKind.UnsupportedFile)
{
    Console.Error.WriteLine($"Unsupported input file: '{input.Path}'. Expected a .cs or .csproj file.");
    return 2;
}

if (input.Kind == InputTargetKind.Project)
{
    Console.Error.WriteLine("Project analysis is not supported yet. Support for .csproj inputs is coming next.");
    return 2;
}

var files = SourceFileDiscovery.Find(input.Path).ToArray();

if (files.Length == 0)
{
    Console.Error.WriteLine($"No C# source files found at '{input.Path}'.");
    return 2;
}

AnalyzerConfiguration configuration;
try
{
    var configPath = configArgument is null
        ? ConfigurationLoader.Find(input.Path)
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
    Console.WriteLine("Usage: review <file-or-directory-or-project> [--config <configuration-file>]");
    Console.WriteLine("Reviews C# source files without sending code to an external service.");
    Console.WriteLine("Accepted inputs: .cs files, directories, and .csproj projects (project analysis coming next).");
}
