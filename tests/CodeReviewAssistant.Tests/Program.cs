using CodeReviewAssistant.Analysis;
using CodeReviewAssistant.Configuration;

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

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} test(s) failed:\n- {string.Join("\n- ", failures)}");
    return 1;
}

Console.WriteLine("All 10 tests passed.");
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
