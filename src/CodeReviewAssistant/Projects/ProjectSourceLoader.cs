using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeReviewAssistant.Projects;

public sealed class ProjectSourceLoader
{
    public async Task<ProjectLoadResult> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            MsBuildRegistration.EnsureRegistered();
        }
        catch (InvalidOperationException exception)
        {
            throw new ProjectLoadException($"Could not locate a compatible MSBuild installation: {exception.Message}", exception);
        }

        using var workspace = MSBuildWorkspace.Create();
        var diagnostics = new List<WorkspaceDiagnostic>();
        workspace.RegisterWorkspaceFailedHandler(eventArgs => diagnostics.Add(eventArgs.Diagnostic));

        Project project;
        try
        {
            project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            throw new ProjectLoadException($"Could not load project '{projectPath}': {exception.Message}", exception);
        }

        var failures = diagnostics
            .Where(diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            .Select(diagnostic => diagnostic.Message)
            .ToArray();
        if (failures.Length > 0)
        {
            throw new ProjectLoadException(
                $"Could not load project '{projectPath}': {string.Join("; ", failures)}");
        }

        var documents = new List<ProjectSourceDocument>();
        foreach (var document in project.Documents.Where(document => document.SourceCodeKind == SourceCodeKind.Regular))
        {
            if (document.FilePath is null || IsBuildArtifact(document.FilePath))
            {
                continue;
            }

            var text = await document.GetTextAsync(cancellationToken);
            documents.Add(new ProjectSourceDocument(document.FilePath, text.ToString()));
        }

        return new ProjectLoadResult(project.Name, documents, diagnostics.Select(item => item.Message).ToArray());
    }

    private static bool IsBuildArtifact(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj");
    }
}

public sealed record ProjectSourceDocument(string Path, string Source);

public sealed record ProjectLoadResult(
    string ProjectName,
    IReadOnlyList<ProjectSourceDocument> Documents,
    IReadOnlyList<string> Diagnostics);

public sealed class ProjectLoadException : Exception
{
    public ProjectLoadException(string message) : base(message) { }

    public ProjectLoadException(string message, Exception innerException) : base(message, innerException) { }
}
