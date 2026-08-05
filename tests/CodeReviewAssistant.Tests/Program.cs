using CodeReviewAssistant.Analysis;
using CodeReviewAssistant.Configuration;
using CodeReviewAssistant.Input;
using CodeReviewAssistant.Projects;
using CodeReviewAssistant.Reporting;
using Microsoft.CodeAnalysis;

var analyzer = new SourceAnalyzer();
var failures = new List<string>();

Run("clean source receives full score", () =>
{
    const string source = """
        internal sealed class Calculator
        {
            public int Add(int left, int right)
            {
                var total = left + right;
                return total;
            }
        }
        """;

    var review = analyzer.Review("Calculator.cs", source);
    Expect(review.Score == 100, $"Expected 100, got {review.Score}");
    Expect(review.Findings.Single().RuleId == "CRA000", "Expected the clean-code finding");
});

Run("semantic model limits short-name findings to local symbols", () =>
{
    const string source = """
        class Example
        {
            private int db;
            void Run()
            {
                var q = db;
            }
        }
        """;
    var review = analyzer.Review("Example.cs", source);
    var findings = review.Findings.Where(finding => finding.RuleId == "CRA002").ToArray();
    Expect(findings.Length == 1, $"Expected one CRA002, got {findings.Length}");
    Expect(findings[0].Message.Contains("'q'", StringComparison.Ordinal), "Expected local variable q");
});

Run("duplicate statements are normalized and reported once", () =>
{
    const string source = """
        class Example
        {
            void Run()
            {
                Console.WriteLine("repeated");
                Console . WriteLine( "repeated" ); // trivia should not matter
                Console.WriteLine("repeated");
            }
        }
        """;
    var review = analyzer.Review("Example.cs", source);
    Expect(review.Findings.Count(finding => finding.RuleId == "CRA003") == 1, "Expected one CRA003");
});

Run("duplicates in separate methods are independent", () =>
{
    const string source = """
        class Example
        {
            void First() { Console.WriteLine("same"); }
            void Second() { Console.WriteLine("same"); }
        }
        """;
    var review = analyzer.Review("Example.cs", source);
    Expect(review.Findings.All(finding => finding.RuleId != "CRA003"), "Unexpected cross-method CRA003");
});

Run("braces in strings do not affect long-method analysis", () =>
{
    const string source = """
        class Example
        {
            void Run()
            {
                var message = "}";
                Console.WriteLine(message);
            }
        }
        """;
    var review = analyzer.Review("Example.cs", source);
    Expect(review.Findings.All(finding => finding.RuleId != "CRA001"), "Unexpected CRA001");
});

Run("long methods are measured from syntax spans", () =>
{
    var statements = string.Join('\n', Enumerable.Repeat("Console.WriteLine(\"line\");", 50));
    var source = $"class Example {{ void Run() {{\n{statements}\n}} }}";
    var review = analyzer.Review("Example.cs", source);
    Expect(review.Findings.Any(finding => finding.RuleId == "CRA001"), "CRA001 was not reported");
});

Run("configuration changes thresholds and penalties", () =>
{
    var configuration = ConfigurationLoader.Parse("""
        {
          "cra001": { "maxLines": 2, "penalty": 25 }
        }
        """);
    const string source = """
        class Example
        {
            void Run()
            {
                Console.WriteLine("configured");
            }
        }
        """;
    var review = new SourceAnalyzer(configuration).Review("Example.cs", source);
    var finding = review.Findings.Single(item => item.RuleId == "CRA001");
    Expect(finding.Penalty == 25, $"Expected penalty 25, got {finding.Penalty}");
    Expect(review.Score == 75, $"Expected score 75, got {review.Score}");
});

Run("rules can be disabled", () =>
{
    var configuration = ConfigurationLoader.Parse("""
        {
          "cra002": { "enabled": false }
        }
        """);
    const string source = "class Example { void Run() { var q = 1; } }";
    var review = new SourceAnalyzer(configuration).Review("Example.cs", source);
    Expect(review.Findings.All(finding => finding.RuleId != "CRA002"), "CRA002 should be disabled");
});

Run("allowed short names can be configured", () =>
{
    var configuration = ConfigurationLoader.Parse("""
        {
          "cra002": { "allowedNames": ["q"] }
        }
        """);
    const string source = "class Example { void Run() { var q = 1; var i = 2; } }";
    var review = new SourceAnalyzer(configuration).Review("Example.cs", source);
    Expect(review.Findings.All(finding => !finding.Message.Contains("'q'", StringComparison.Ordinal)),
        "q should be allowed");
    Expect(review.Findings.Any(finding => finding.Message.Contains("'i'", StringComparison.Ordinal)),
        "custom allowed names should replace the defaults");
});

Run("invalid configuration is rejected", () =>
{
    try
    {
        ConfigurationLoader.Parse("""{ "cra003": { "penalty": -1 } }""");
        throw new InvalidOperationException("Expected invalid configuration to fail");
    }
    catch (ConfigurationException)
    {
        // Expected.
    }
});

Run("input detector recognizes C# source files", () =>
{
    WithTemporaryInput("Example.cs", target =>
        Expect(InputTargetDetector.Detect(target).Kind == InputTargetKind.SourceFile,
            "Expected a source-file input"));
});

Run("input detector recognizes directories", () =>
{
    WithTemporaryDirectory(target =>
        Expect(InputTargetDetector.Detect(target).Kind == InputTargetKind.Directory,
            "Expected a directory input"));
});

Run("input detector recognizes C# projects", () =>
{
    WithTemporaryInput("Example.csproj", target =>
        Expect(InputTargetDetector.Detect(target).Kind == InputTargetKind.Project,
            "Expected a project input"));
});

Run("input detector recognizes solution formats", () =>
{
    WithTemporaryInput("Example.sln", target =>
        Expect(InputTargetDetector.Detect(target).Kind == InputTargetKind.Solution,
            "Expected an SLN solution input"));
    WithTemporaryInput("Example.slnx", target =>
        Expect(InputTargetDetector.Detect(target).Kind == InputTargetKind.Solution,
            "Expected an SLNX solution input"));
});

Run("input detector rejects unsupported file types", () =>
{
    WithTemporaryInput("README.md", target =>
        Expect(InputTargetDetector.Detect(target).Kind == InputTargetKind.UnsupportedFile,
            "Expected an unsupported-file input"));
});

Run("input detector reports missing paths", () =>
{
    var target = Path.Combine(Path.GetTempPath(), $"code-review-assistant-{Guid.NewGuid():N}", "Missing.cs");
    Expect(InputTargetDetector.Detect(target).Kind == InputTargetKind.Missing, "Expected a missing input");
});

Run("MSBuild registration is idempotent", () =>
{
    MsBuildRegistration.EnsureRegistered();
    MsBuildRegistration.EnsureRegistered();
});

Run("SDK-style projects load C# documents", () =>
{
    var projectPath = Path.Combine(FindRepositoryRoot(), "tests", "Fixtures", "SampleProject", "SampleProject.csproj");
    var result = new ProjectSourceLoader().LoadAsync(projectPath).GetAwaiter().GetResult();
    Expect(result.ProjectName == "SampleProject", $"Unexpected project name: {result.ProjectName}");
    Expect(result.Documents.Count == 2, $"Expected two authored documents, got {result.Documents.Count}");
    Expect(result.Documents.All(document => result.Compilation.SyntaxTrees.Contains(document.SyntaxTree)),
        "Every authored document should belong to the shared compilation");
});

Run("project documents use cross-file semantic information", () =>
{
    var projectPath = Path.Combine(FindRepositoryRoot(), "tests", "Fixtures", "SampleProject", "SampleProject.csproj");
    var result = new ProjectSourceLoader().LoadAsync(projectPath).GetAwaiter().GetResult();
    var document = result.Documents.Single(item =>
        string.Equals(Path.GetFileName(item.Path), "Calculator.cs", StringComparison.Ordinal));
    var model = result.Compilation.GetSemanticModel(document.SyntaxTree);
    var calculator = document.SyntaxTree.GetRoot().DescendantNodes()
        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
        .Single();
    var interfaceType = model.GetTypeInfo(calculator.BaseList!.Types.Single().Type).Type;

    Expect(interfaceType?.ToDisplayString() == "SampleProject.ICalculator",
        "Expected ICalculator to resolve from another project document");
    var review = new SourceAnalyzer().Review(document.Path, document.SyntaxTree, result.Compilation);
    Expect(review.Score == 100, $"Expected a clean shared-compilation review, got {review.Score}");
});

Run("project compilation diagnostics include source locations", () =>
{
    var projectPath = Path.Combine(
        FindRepositoryRoot(), "tests", "Fixtures", "ProjectWithDiagnostics", "ProjectWithDiagnostics.csproj");
    var result = new ProjectSourceLoader().LoadAsync(projectPath).GetAwaiter().GetResult();
    var diagnostic = result.Diagnostics.FirstOrDefault(item => item.Id == "CS0246")
        ?? throw new InvalidOperationException("Expected a CS0246 diagnostic");
    Expect(diagnostic.Path?.EndsWith("BrokenService.cs", StringComparison.Ordinal) == true,
        "Expected the compiler diagnostic source path");
    Expect(diagnostic.Line is 5 or 8, $"Unexpected compiler diagnostic line: {diagnostic.Line}");
    var formatted = new ProjectDiagnosticFormatter().Format(result.Diagnostics);
    Expect(formatted.Contains("Project Diagnostics", StringComparison.Ordinal), "Expected a diagnostics heading");
    Expect(formatted.Contains("CS0246", StringComparison.Ordinal), "Expected the diagnostic ID in output");
});

Run("analysis continues when a project has compiler errors", () =>
{
    var projectPath = Path.Combine(
        FindRepositoryRoot(), "tests", "Fixtures", "ProjectWithDiagnostics", "ProjectWithDiagnostics.csproj");
    var result = new ProjectSourceLoader().LoadAsync(projectPath).GetAwaiter().GetResult();
    var document = result.Documents.Single();
    var review = new SourceAnalyzer().Review(document.Path, document.SyntaxTree, result.Compilation);
    Expect(review.Findings.Any(finding => finding.RuleId == "CRA002"),
        "Expected maintainability analysis to continue after compiler errors");
});

Run("workspace failures identify project loading problems", () =>
{
    var projectPath = Path.Combine(
        FindRepositoryRoot(), "tests", "Fixtures", "ProjectWithMissingReference", "ProjectWithMissingReference.csproj");
    try
    {
        new ProjectSourceLoader().LoadAsync(projectPath).GetAwaiter().GetResult();
        throw new InvalidOperationException("Expected the missing project reference to fail");
    }
    catch (ProjectLoadException exception)
    {
        Expect(exception.Message.Contains("DoesNotExist", StringComparison.Ordinal),
            "Expected the missing reference in the workspace error");
    }
});

Run("solutions load every C# project", () =>
{
    foreach (var extension in new[] { ".sln", ".slnx" })
    {
        var solutionPath = Path.Combine(
            FindRepositoryRoot(), "tests", "Fixtures", "SampleSolution", $"SampleSolution{extension}");
        var result = new SolutionSourceLoader().LoadAsync(solutionPath).GetAwaiter().GetResult();
        Expect(result.Projects.Count == 2, $"Expected two projects from {extension}, got {result.Projects.Count}");
        Expect(result.Projects.Select(project => project.ProjectName).Order().SequenceEqual(["App", "Domain"]),
            $"Expected the App and Domain projects from {extension}");
        Expect(result.Projects.SelectMany(project => project.Documents).Count() == 2,
            $"Expected both authored source documents from {extension}");
    }
});

Run("solution project references resolve without compiler errors", () =>
{
    var solutionPath = Path.Combine(
        FindRepositoryRoot(), "tests", "Fixtures", "SampleSolution", "SampleSolution.slnx");
    var result = new SolutionSourceLoader().LoadAsync(solutionPath).GetAwaiter().GetResult();
    Expect(result.Diagnostics.All(diagnostic => diagnostic.Severity != ProjectDiagnosticSeverity.Error),
        "Expected project references to resolve in the solution");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} test(s) failed:\n- {string.Join("\n- ", failures)}");
    return 1;
}

Console.WriteLine("All 24 tests passed.");
return 0;

void Run(string name, Action test)
{
    try { test(); }
    catch (Exception exception) { failures.Add($"{name}: {exception.Message}"); }
}

static void Expect(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void WithTemporaryInput(string fileName, Action<string> test)
{
    WithTemporaryDirectory(directory =>
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, string.Empty);
        test(path);
    });
}

static void WithTemporaryDirectory(Action<string> test)
{
    var directory = Path.Combine(Path.GetTempPath(), $"code-review-assistant-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        test(directory);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "CodeReviewAssistant.slnx")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate the repository root.");
}
