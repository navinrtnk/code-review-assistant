using Microsoft.Build.Locator;

namespace CodeReviewAssistant.Projects;

public static class MsBuildRegistration
{
    private static readonly object SyncRoot = new();

    public static void EnsureRegistered()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }
}
