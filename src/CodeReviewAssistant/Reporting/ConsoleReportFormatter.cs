using System.Text;
using CodeReviewAssistant.Analysis;

namespace CodeReviewAssistant.Reporting;

public sealed class ConsoleReportFormatter
{
    public string Format(IReadOnlyCollection<FileReview> reviews)
    {
        var output = new StringBuilder("Code Review Summary\n===================\n");

        foreach (var review in reviews)
        {
            output.AppendLine().AppendLine(review.Path);
            foreach (var finding in review.Findings)
            {
                var marker = finding.Severity == FindingSeverity.Warning ? "!" : "✓";
                output.AppendLine($"  {marker} {finding.RuleId} line {finding.Line}: {finding.Message}");
            }

            output.AppendLine($"  Score: {review.Score}/100");
        }

        var overallScore = (int)Math.Round(reviews.Average(review => review.Score));
        output.AppendLine().AppendLine($"Overall score: {overallScore}/100");
        return output.ToString();
    }
}

