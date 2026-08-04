using System.Text;
using CodeReviewAssistant.Projects;

namespace CodeReviewAssistant.Reporting;

public sealed class ProjectDiagnosticFormatter
{
    public string Format(IReadOnlyCollection<ProjectDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return string.Empty;
        }

        var output = new StringBuilder("Project Diagnostics\n===================\n");
        foreach (var diagnostic in diagnostics)
        {
            var marker = diagnostic.Severity == ProjectDiagnosticSeverity.Error ? "!" : "~";
            var location = FormatLocation(diagnostic);
            output.AppendLine($"{marker} {diagnostic.Id}{location}: {diagnostic.Message}");
        }

        return output.AppendLine().ToString();
    }

    private static string FormatLocation(ProjectDiagnostic diagnostic)
    {
        if (diagnostic.Path is null)
        {
            return string.Empty;
        }

        return diagnostic.Line is null
            ? $" {diagnostic.Path}"
            : $" {diagnostic.Path}:{diagnostic.Line}";
    }
}
