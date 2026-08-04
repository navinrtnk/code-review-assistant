namespace CodeReviewAssistant.Projects;

public enum ProjectDiagnosticSeverity
{
    Warning,
    Error
}

public enum ProjectDiagnosticOrigin
{
    Workspace,
    Compilation
}

public sealed record ProjectDiagnostic(
    string Id,
    ProjectDiagnosticSeverity Severity,
    ProjectDiagnosticOrigin Origin,
    string Message,
    string? Path = null,
    int? Line = null);
