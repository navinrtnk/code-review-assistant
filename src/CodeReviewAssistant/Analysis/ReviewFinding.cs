namespace CodeReviewAssistant.Analysis;

public enum FindingSeverity
{
    Information,
    Warning
}

public sealed record ReviewFinding(
    string RuleId,
    FindingSeverity Severity,
    string Message,
    int Line,
    int Penalty);

public sealed record FileReview(string Path, IReadOnlyList<ReviewFinding> Findings)
{
    public int Score => Math.Max(0, 100 - Findings.Sum(finding => finding.Penalty));
}

