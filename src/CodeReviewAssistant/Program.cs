using CodeReviewAssistant.Analysis;
using CodeReviewAssistant.Configuration;
using CodeReviewAssistant.Input;
using CodeReviewAssistant.Projects;
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
    Console.Error.WriteLine($"Unsupported input file: '{input.Path}'. Expected a .cs, .csproj, .sln, or .slnx file.");
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
IReadOnlyList<ProjectDiagnostic> projectDiagnostics = [];

if (input.Kind == InputTargetKind.Project)
{
    try
    {
        var project = await new ProjectSourceLoader().LoadAsync(input.Path);
        projectDiagnostics = project.Diagnostics;
        foreach (var document in project.Documents)
        {
            results.Add(analyzer.Review(document.Path, document.SyntaxTree, project.Compilation));
        }
    }
    catch (ProjectLoadException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}
else if (input.Kind == InputTargetKind.Solution)
{
    try
    {
        var solution = await new SolutionSourceLoader().LoadAsync(input.Path);
        projectDiagnostics = solution.Diagnostics;
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var reviewedPaths = new HashSet<string>(pathComparer);
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents.Where(document => reviewedPaths.Add(document.Path)))
            {
                results.Add(analyzer.Review(document.Path, document.SyntaxTree, project.Compilation));
            }
        }
    }
    catch (ProjectLoadException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}
else
{
    var files = SourceFileDiscovery.Find(input.Path).ToArray();
    if (files.Length == 0)
    {
        Console.Error.WriteLine($"No C# source files found at '{input.Path}'.");
        return 2;
    }

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
}

if (results.Count == 0)
{
    Console.Error.WriteLine($"No C# source files found in input '{input.Path}'.");
    return 2;
}

Console.Write(new ProjectDiagnosticFormatter().Format(projectDiagnostics));
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
    Console.WriteLine("Usage: review <file-directory-project-or-solution> [--config <configuration-file>]");
    Console.WriteLine("Reviews C# source files without sending code to an external service.");
    Console.WriteLine("Accepted inputs: .cs files, directories, SDK-style .csproj projects, and .sln/.slnx solutions.");
}
