namespace CodeReviewAssistant.Input;

public enum InputTargetKind
{
    SourceFile,
    Directory,
    Project,
    UnsupportedFile,
    Missing
}

public sealed record InputTarget(string Path, InputTargetKind Kind);
