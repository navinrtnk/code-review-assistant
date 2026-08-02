using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CodeReviewAssistant.Configuration;

namespace CodeReviewAssistant.Analysis;

public sealed class SourceAnalyzer
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformReferences =
        new(CreatePlatformReferences);
    private readonly AnalyzerConfiguration _configuration;
    private readonly HashSet<string> _allowedShortNames;

    public SourceAnalyzer(AnalyzerConfiguration? configuration = null)
    {
        _configuration = configuration ?? new AnalyzerConfiguration();
        _allowedShortNames = new HashSet<string>(_configuration.Cra002.AllowedNames, StringComparer.Ordinal);
    }

    public FileReview Review(string path, string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create(
            "CodeUnderReview",
            [syntaxTree],
            PlatformReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var findings = new List<ReviewFinding>();

        if (_configuration.Cra001.Enabled)
        {
            FindLongMethods(root, syntaxTree, findings);
        }

        if (_configuration.Cra002.Enabled)
        {
            FindUnclearVariableNames(root, semanticModel, findings);
        }

        if (_configuration.Cra003.Enabled)
        {
            FindDuplicateStatements(root, syntaxTree, findings);
        }

        if (findings.Count == 0)
        {
            findings.Add(new ReviewFinding(
                "CRA000", FindingSeverity.Information, "No maintainability issues detected", 1, 0));
        }

        return new FileReview(path, findings.OrderBy(finding => finding.Line).ToArray());
    }

    private void FindLongMethods(
        SyntaxNode root,
        SyntaxTree syntaxTree,
        ICollection<ReviewFinding> findings)
    {
        foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            if (method.Body is null)
            {
                continue;
            }

            AddLongMethodFinding(
                method.Body,
                method switch
                {
                    MethodDeclarationSyntax declaration => declaration.Identifier.ValueText,
                    ConstructorDeclarationSyntax declaration => declaration.Identifier.ValueText,
                    DestructorDeclarationSyntax declaration => $"~{declaration.Identifier.ValueText}",
                    OperatorDeclarationSyntax declaration => $"operator {declaration.OperatorToken.ValueText}",
                    ConversionOperatorDeclarationSyntax declaration => $"operator {declaration.Type}",
                    _ => "member"
                },
                syntaxTree,
                findings);
        }

        foreach (var function in root.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
        {
            if (function.Body is not null)
            {
                AddLongMethodFinding(function.Body, function.Identifier.ValueText, syntaxTree, findings);
            }
        }
    }

    private void AddLongMethodFinding(
        BlockSyntax body,
        string name,
        SyntaxTree syntaxTree,
        ICollection<ReviewFinding> findings)
    {
        var lineSpan = syntaxTree.GetLineSpan(body.Span);
        var length = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
        if (length <= _configuration.Cra001.MaxLines)
        {
            return;
        }

        findings.Add(new ReviewFinding(
            "CRA001",
            FindingSeverity.Warning,
            $"Method {name}() is {length} lines long (limit: {_configuration.Cra001.MaxLines})",
            GetLine(syntaxTree, body),
            _configuration.Cra001.Penalty));
    }

    private void FindUnclearVariableNames(
        SyntaxNode root,
        SemanticModel semanticModel,
        ICollection<ReviewFinding> findings)
    {
        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            var symbol = semanticModel.GetDeclaredSymbol(declarator);
            if (symbol is not ILocalSymbol ||
                symbol.Name.Length > 2 ||
                _allowedShortNames.Contains(symbol.Name))
            {
                continue;
            }

            findings.Add(new ReviewFinding(
                "CRA002",
                FindingSeverity.Warning,
                $"Variable '{symbol.Name}' could have a clearer name",
                GetLine(declarator.SyntaxTree, declarator),
                _configuration.Cra002.Penalty));
        }
    }

    private void FindDuplicateStatements(
        SyntaxNode root,
        SyntaxTree syntaxTree,
        ICollection<ReviewFinding> findings)
    {
        var executableBodies = root.DescendantNodes()
            .Where(node => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax)
            .Select(GetBody)
            .Where(body => body is not null)
            .Cast<BlockSyntax>();

        foreach (var body in executableBodies)
        {
            var firstOccurrence = new Dictionary<string, int>(StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);

            foreach (var statement in body.DescendantNodes()
                         .OfType<StatementSyntax>()
                         .Where(IsDuplicateCandidate))
            {
                var normalized = statement.WithoutTrivia().NormalizeWhitespace().ToFullString();
                if (normalized.Length < 12)
                {
                    continue;
                }

                var line = GetLine(syntaxTree, statement);
                if (firstOccurrence.TryGetValue(normalized, out var firstLine) && reported.Add(normalized))
                {
                    findings.Add(new ReviewFinding(
                        "CRA003",
                        FindingSeverity.Warning,
                        $"Duplicate statement also appears on line {firstLine}",
                        line,
                        _configuration.Cra003.Penalty));
                }
                else
                {
                    firstOccurrence.TryAdd(normalized, line);
                }
            }
        }
    }

    private static BlockSyntax? GetBody(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.Body,
        LocalFunctionStatementSyntax function => function.Body,
        AccessorDeclarationSyntax accessor => accessor.Body,
        _ => null
    };

    private static bool IsDuplicateCandidate(StatementSyntax statement) =>
        statement is ExpressionStatementSyntax or LocalDeclarationStatementSyntax;

    private static int GetLine(SyntaxTree tree, SyntaxNode node) =>
        tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    private static IReadOnlyList<MetadataReference> CreatePlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            return [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
        }

        return trustedAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
