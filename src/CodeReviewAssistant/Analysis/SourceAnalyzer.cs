using System.Text.RegularExpressions;

namespace CodeReviewAssistant.Analysis;

public sealed partial class SourceAnalyzer
{
    private const int LongMethodThreshold = 50;
    private static readonly HashSet<string> AllowedShortNames = new(StringComparer.Ordinal)
    {
        "i", "j", "k", "x", "y", "id"
    };

    public FileReview Review(string path, string source)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var findings = new List<ReviewFinding>();

        FindLongMethods(lines, findings);
        FindUnclearVariableNames(lines, findings);
        FindDuplicateStatements(lines, findings);

        if (findings.Count == 0)
        {
            findings.Add(new ReviewFinding(
                "CRA000", FindingSeverity.Information, "No maintainability issues detected", 1, 0));
        }

        return new FileReview(path, findings.OrderBy(finding => finding.Line).ToArray());
    }

    private static void FindLongMethods(string[] lines, ICollection<ReviewFinding> findings)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            var match = MethodDeclaration().Match(StripLineComment(lines[index]));
            if (!match.Success)
            {
                continue;
            }

            var openingLine = FindOpeningBrace(lines, index);
            if (openingLine < 0)
            {
                continue;
            }

            var endLine = FindClosingBrace(lines, openingLine);
            if (endLine < 0)
            {
                continue;
            }

            var length = endLine - openingLine + 1;
            if (length > LongMethodThreshold)
            {
                findings.Add(new ReviewFinding(
                    "CRA001",
                    FindingSeverity.Warning,
                    $"Method {match.Groups[1].Value}() is {length} lines long (limit: {LongMethodThreshold})",
                    index + 1,
                    10));
            }

            index = endLine;
        }
    }

    private static void FindUnclearVariableNames(string[] lines, ICollection<ReviewFinding> findings)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            foreach (Match match in LocalDeclaration().Matches(StripLineComment(lines[index])))
            {
                var name = match.Groups[1].Value;
                if (name.Length <= 2 && !AllowedShortNames.Contains(name))
                {
                    findings.Add(new ReviewFinding(
                        "CRA002", FindingSeverity.Warning,
                        $"Variable '{name}' could have a clearer name", index + 1, 4));
                }
            }
        }
    }

    private static void FindDuplicateStatements(string[] lines, ICollection<ReviewFinding> findings)
    {
        var firstOccurrence = new Dictionary<string, int>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < lines.Length; index++)
        {
            var statement = StripLineComment(lines[index]).Trim();
            if (statement.Length < 12 ||
                !statement.EndsWith(';') ||
                statement.StartsWith("using ") ||
                statement.StartsWith("return ") ||
                statement.StartsWith("yield return ") ||
                statement is "yield break;" or "break;" or "continue;")
            {
                continue;
            }

            if (firstOccurrence.TryGetValue(statement, out var firstLine) && reported.Add(statement))
            {
                findings.Add(new ReviewFinding(
                    "CRA003", FindingSeverity.Warning,
                    $"Duplicate statement also appears on line {firstLine}", index + 1, 6));
            }
            else
            {
                firstOccurrence.TryAdd(statement, index + 1);
            }
        }
    }

    private static int FindOpeningBrace(string[] lines, int start)
    {
        for (var index = start; index < Math.Min(lines.Length, start + 5); index++)
        {
            if (lines[index].Contains('{')) return index;
            if (lines[index].Contains(';')) return -1;
        }

        return -1;
    }

    private static int FindClosingBrace(string[] lines, int start)
    {
        var depth = 0;
        for (var index = start; index < lines.Length; index++)
        {
            var line = StripStringsAndComments(lines[index]);
            depth += line.Count(character => character == '{');
            depth -= line.Count(character => character == '}');
            if (depth == 0) return index;
        }

        return -1;
    }

    private static string StripLineComment(string line) => line.Split("//", 2)[0];

    private static string StripStringsAndComments(string line) =>
        StringLiteral().Replace(StripLineComment(line), "\"\"");

    [GeneratedRegex(@"^\s*(?:(?:public|private|protected|internal|static|virtual|override|async|sealed|new|partial|unsafe|extern)\s+)*(?:[\w<>,.?\[\]]+\s+)+(\w+)\s*\([^;]*\)\s*(?:where\b.*)?(?:\{|$)")]
    private static partial Regex MethodDeclaration();

    [GeneratedRegex(@"\b(?:var|bool|byte|char|decimal|double|float|int|long|object|sbyte|short|string|uint|ulong|ushort)\s+([A-Za-z_]\w*)\s*(?:=|;|,)")]
    private static partial Regex LocalDeclaration();

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"")]
    private static partial Regex StringLiteral();
}
