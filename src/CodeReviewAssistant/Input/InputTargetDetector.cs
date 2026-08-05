namespace CodeReviewAssistant.Input;

public static class InputTargetDetector
{
    public static InputTarget Detect(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return new InputTarget(fullPath, InputTargetKind.Directory);
        }

        if (!File.Exists(fullPath))
        {
            return new InputTarget(fullPath, InputTargetKind.Missing);
        }

        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".cs" => new InputTarget(fullPath, InputTargetKind.SourceFile),
            ".csproj" => new InputTarget(fullPath, InputTargetKind.Project),
            ".sln" or ".slnx" => new InputTarget(fullPath, InputTargetKind.Solution),
            _ => new InputTarget(fullPath, InputTargetKind.UnsupportedFile)
        };
    }
}
