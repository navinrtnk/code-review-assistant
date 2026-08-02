namespace CodeReviewAssistant.Configuration;

public sealed class AnalyzerConfiguration
{
    public LongMethodRuleConfiguration Cra001 { get; init; } = new();
    public ShortNameRuleConfiguration Cra002 { get; init; } = new();
    public RuleConfiguration Cra003 { get; init; } = new() { Penalty = 6 };
}

public class RuleConfiguration
{
    public bool Enabled { get; init; } = true;
    public int Penalty { get; init; }
}

public sealed class LongMethodRuleConfiguration : RuleConfiguration
{
    public LongMethodRuleConfiguration()
    {
        Penalty = 10;
    }

    public int MaxLines { get; init; } = 50;
}

public sealed class ShortNameRuleConfiguration : RuleConfiguration
{
    public ShortNameRuleConfiguration()
    {
        Penalty = 4;
    }

    public IReadOnlyList<string> AllowedNames { get; init; } = ["i", "j", "k", "x", "y", "id"];
}
