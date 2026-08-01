using CodeReviewAssistant.Analysis;

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

Run("unclear variable names are reported", () =>
{
    const string source = "class Example { void Run() { var q = 42; } }";
    var review = analyzer.Review("Example.cs", source);
    Expect(review.Findings.Any(finding => finding.RuleId == "CRA002"), "CRA002 was not reported");
});

Run("duplicate statements are reported once", () =>
{
    const string source = """
        class Example
        {
            void Run()
            {
                Console.WriteLine("repeated");
                Console.WriteLine("repeated");
                Console.WriteLine("repeated");
            }
        }
        """;
    var review = analyzer.Review("Example.cs", source);
    Expect(review.Findings.Count(finding => finding.RuleId == "CRA003") == 1, "Expected one CRA003");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} test(s) failed:\n- {string.Join("\n- ", failures)}");
    return 1;
}

Console.WriteLine("All 3 tests passed.");
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

