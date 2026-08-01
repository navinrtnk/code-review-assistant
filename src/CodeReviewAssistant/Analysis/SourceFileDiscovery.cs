namespace CodeReviewAssistant.Analysis;

public static class SourceFileDiscovery
{
    public static IEnumerable<string> Find(string path)
    {
        if (File.Exists(path))
        {
            if (string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }

            yield break;
        }

        if (!Directory.Exists(path))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
                     .Where(file => !HasBuildDirectory(file))
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            yield return file;
        }
    }

    private static bool HasBuildDirectory(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj");
    }
}

