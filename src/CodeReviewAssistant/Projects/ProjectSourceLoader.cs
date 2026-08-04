using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeReviewAssistant.Projects;

public sealed class ProjectSourceLoader
{
    public async Task<ProjectLoadResult> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        EnsureMsBuildRegistered();
        using var workspace = MSBuildWorkspace.Create();
        var diagnostics = new List<WorkspaceDiagnostic>();
        workspace.RegisterWorkspaceFailedHandler(eventArgs => diagnostics.Add(eventArgs.Diagnostic));

        var project = await OpenProjectAsync(workspace, projectPath, cancellationToken);
        ThrowIfWorkspaceFailed(projectPath, diagnostics);
        var compilation = await project.GetCompilationAsync(cancellationToken)
            ?? throw new ProjectLoadException($"Roslyn could not create a compilation for project '{projectPath}'.");
        var documents = await LoadDocumentsAsync(project, cancellationToken);
        var projectDiagnostics = GetProjectDiagnostics(compilation, diagnostics, cancellationToken);

        return new ProjectLoadResult(
            project.Name,
            compilation,
            documents,
            projectDiagnostics);
    }

    private static async Task<IReadOnlyList<ProjectSourceDocument>> LoadDocumentsAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        var documents = new List<ProjectSourceDocument>();
        foreach (var document in project.Documents.Where(document => document.SourceCodeKind == SourceCodeKind.Regular))
        {
            if (document.FilePath is null || IsBuildArtifact(document.FilePath))
            {
                continue;
            }

            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree is not null)
            {
                documents.Add(new ProjectSourceDocument(document.FilePath, syntaxTree));
            }
        }

        return documents;
    }

    private static async Task<Project> OpenProjectAsync(
        MSBuildWorkspace workspace,
        string projectPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            throw new ProjectLoadException($"Could not load project '{projectPath}': {exception.Message}", exception);
        }
    }

    private static void ThrowIfWorkspaceFailed(string projectPath, IEnumerable<WorkspaceDiagnostic> diagnostics)
    {
        var failures = diagnostics
            .Where(diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            .Select(diagnostic => diagnostic.Message)
            .ToArray();
        if (failures.Length > 0)
        {
            throw new ProjectLoadException(
                $"Could not load project '{projectPath}': {string.Join("; ", failures)}");
        }
    }

    private static void EnsureMsBuildRegistered()
    {
        try
        {
            MsBuildRegistration.EnsureRegistered();
        }
        catch (InvalidOperationException exception)
        {
            throw new ProjectLoadException(
                $"Could not locate a compatible MSBuild installation: {exception.Message}", exception);
        }
    }

    private static IReadOnlyList<ProjectDiagnostic> GetProjectDiagnostics(
        Compilation compilation,
        IEnumerable<WorkspaceDiagnostic> workspaceDiagnostics,
        CancellationToken cancellationToken)
    {
        var diagnostics = workspaceDiagnostics
            .Select(item => new ProjectDiagnostic(
                "MSBUILD",
                item.Kind == WorkspaceDiagnosticKind.Failure
                    ? ProjectDiagnosticSeverity.Error
                    : ProjectDiagnosticSeverity.Warning,
                ProjectDiagnosticOrigin.Workspace,
                item.Message))
            .ToList();

        diagnostics.AddRange(compilation.GetDiagnostics(cancellationToken)
            .Where(item => item.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Where(item => !IsGeneratedDiagnostic(item))
            .Select(ToProjectDiagnostic));
        return diagnostics;
    }

    private static ProjectDiagnostic ToProjectDiagnostic(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.IsInSource
            ? diagnostic.Location.GetLineSpan()
            : default;
        return new ProjectDiagnostic(
            diagnostic.Id,
            diagnostic.Severity == DiagnosticSeverity.Error
                ? ProjectDiagnosticSeverity.Error
                : ProjectDiagnosticSeverity.Warning,
            ProjectDiagnosticOrigin.Compilation,
            diagnostic.GetMessage(),
            lineSpan.IsValid ? lineSpan.Path : null,
            lineSpan.IsValid ? lineSpan.StartLinePosition.Line + 1 : null);
    }

    private static bool IsGeneratedDiagnostic(Diagnostic diagnostic) =>
        diagnostic.Location.IsInSource && IsBuildArtifact(diagnostic.Location.SourceTree?.FilePath ?? string.Empty);

    private static bool IsBuildArtifact(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj");
    }
}

public sealed record ProjectSourceDocument(string Path, SyntaxTree SyntaxTree);

public sealed record ProjectLoadResult(
    string ProjectName,
    Compilation Compilation,
    IReadOnlyList<ProjectSourceDocument> Documents,
    IReadOnlyList<ProjectDiagnostic> Diagnostics);

public sealed class ProjectLoadException : Exception
{
    public ProjectLoadException(string message) : base(message) { }

    public ProjectLoadException(string message, Exception innerException) : base(message, innerException) { }
}
