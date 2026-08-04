namespace ProjectWithDiagnostics;

public sealed class BrokenService
{
    public MissingType Create()
    {
        var q = 1;
        return new MissingType();
    }
}
