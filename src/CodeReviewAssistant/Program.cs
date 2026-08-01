using CodeReviewAssistant.Analysis;
using CodeReviewAssistant.Reporting;

if (args.Length != 1 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: review <file-or-directory>");
    Console.WriteLine("Reviews C# source files without sending code to an external service.");
    return args.Length == 1 ? 0 : 2;
}

var path = Path.GetFullPath(args[0]);
var files = SourceFileDiscovery.Find(path).ToArray();

if (files.Length == 0)
{
    Console.Error.WriteLine($"No C# source files found at '{path}'.");
    return 2;
}

var analyzer = new SourceAnalyzer();
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

