using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeReviewAssistant.Projects;

public sealed class SolutionSourceLoader
{
    public async Task<SolutionLoadResult> LoadAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        ProjectSourceLoader.EnsureMsBuildRegistered();
        using var workspace = MSBuildWorkspace.Create();
        var workspaceDiagnostics = new List<WorkspaceDiagnostic>();
        workspace.RegisterWorkspaceFailedHandler(eventArgs => workspaceDiagnostics.Add(eventArgs.Diagnostic));

        var solution = await OpenSolutionAsync(workspace, solutionPath, cancellationToken);
        ProjectSourceLoader.ThrowIfWorkspaceFailed(solutionPath, workspaceDiagnostics);

        var projects = new List<ProjectLoadResult>();
        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            projects.Add(await ProjectSourceLoader.CreateResultAsync(project, [], cancellationToken));
        }

        var diagnostics = workspaceDiagnostics
            .Select(ProjectSourceLoader.ToProjectDiagnostic)
            .Concat(projects.SelectMany(project => project.Diagnostics))
            .Distinct()
            .ToArray();
        return new SolutionLoadResult(projects, diagnostics);
    }

    private static async Task<Solution> OpenSolutionAsync(
        MSBuildWorkspace workspace,
        string solutionPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            throw new ProjectLoadException($"Could not load solution '{solutionPath}': {exception.Message}", exception);
        }
    }
}

public sealed record SolutionLoadResult(
    IReadOnlyList<ProjectLoadResult> Projects,
    IReadOnlyList<ProjectDiagnostic> Diagnostics);
