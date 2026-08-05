namespace CodeReviewAssistant.Input;

public enum InputTargetKind
{
    SourceFile,
    Directory,
    Project,
    Solution,
    UnsupportedFile,
    Missing
}

public sealed record InputTarget(string Path, InputTargetKind Kind);
