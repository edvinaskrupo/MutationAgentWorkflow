using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Core;

public class CodeMetricsAnalyzer
{
    private static readonly HashSet<string> ExternalDependencyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "HttpClient", "IHttpClientFactory",
        "DbContext", "IDbContextFactory",
        "ILogger", "ILoggerFactory",
        "IMemoryCache", "IDistributedCache",
        "IConfiguration",
        "IMediator",
        "IMessageBus", "IEventBus",
        "IServiceProvider"
    };

    private static readonly HashSet<string> ControllerAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApiController", "ApiControllerAttribute",
        "Controller", "ControllerAttribute"
    };

    private static readonly HashSet<string> ControllerBaseClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ControllerBase", "Controller", "ApiController"
    };

    private const int LongMethodThreshold = 30;
    private const int HighParamThreshold = 4;

    public CodeMetrics Analyze(string sourceCode, string? targetClassName = null)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        var allClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        if (allClasses.Count == 0)
            return new CodeMetrics
            {
                RecommendedStrategy = "Skip",
                Reasoning = "No class declaration found in source."
            };

        ClassDeclarationSyntax? classDecl = null;

        if (!string.IsNullOrWhiteSpace(targetClassName))
            classDecl = allClasses.FirstOrDefault(c =>
                c.Identifier.Text.Equals(targetClassName, StringComparison.OrdinalIgnoreCase));

        classDecl ??= allClasses
            .OrderByDescending(c => c.Members.OfType<MethodDeclarationSyntax>().Count())
            .First();

        var metrics = new CodeMetrics();

        metrics.MethodComplexities = CalculatePerMethodComplexity(classDecl);
        metrics.CyclomaticComplexity = metrics.MethodComplexities.Values.Sum();
        AnalyzeDependencies(classDecl, metrics);
        metrics.IsControllerOrEndpoint = DetectControllerOrEndpoint(classDecl);
        AnalyzeCodeSmells(classDecl, metrics);

        DetermineStrategy(metrics);

        return metrics;
    }

    private Dictionary<string, int> CalculatePerMethodComplexity(ClassDeclarationSyntax classDecl)
    {
        var result = new Dictionary<string, int>();

        foreach (var method in classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var name = method.Identifier.Text;
            int complexity = 1;

            foreach (var node in method.DescendantNodes())
            {
                complexity += node switch
                {
                    IfStatementSyntax => 1,
                    ElseClauseSyntax => 0,
                    WhileStatementSyntax => 1,
                    ForStatementSyntax => 1,
                    ForEachStatementSyntax => 1,
                    DoStatementSyntax => 1,
                    CaseSwitchLabelSyntax => 1,
                    CasePatternSwitchLabelSyntax => 1,
                    CatchClauseSyntax => 1,
                    ConditionalExpressionSyntax => 1,
                    BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.LogicalAndExpression) => 1,
                    BinaryExpressionSyntax bin2 when bin2.IsKind(SyntaxKind.LogicalOrExpression) => 1,
                    BinaryExpressionSyntax bin3 when bin3.IsKind(SyntaxKind.CoalesceExpression) => 1,
                    ConditionalAccessExpressionSyntax => 1,
                    SwitchExpressionArmSyntax => 1,
                    _ => 0
                };
            }

            if (result.ContainsKey(name))
                result[name] = Math.Max(result[name], complexity);
            else
                result[name] = complexity;
        }

        return result;
    }

    private void AnalyzeDependencies(ClassDeclarationSyntax classDecl, CodeMetrics metrics)
    {
        var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();

        foreach (var ctor in constructors)
        {
            foreach (var param in ctor.ParameterList.Parameters)
            {
                var typeName = param.Type?.ToString() ?? string.Empty;
                metrics.InjectedDependencies.Add(typeName);

                if (IsExternalDependency(typeName))
                    metrics.HasExternalDependencies = true;
            }
        }

        metrics.DependencyCount = metrics.InjectedDependencies.Count;
    }

    private void AnalyzeCodeSmells(ClassDeclarationSyntax classDecl, CodeMetrics metrics)
    {
        int maxNesting = 0;

        foreach (var method in classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var name = method.Identifier.Text;
            var lineSpan = method.GetLocation().GetLineSpan();
            int lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;

            if (lineCount > LongMethodThreshold)
                metrics.LongMethods.Add($"{name} ({lineCount} lines)");

            int paramCount = method.ParameterList.Parameters.Count;
            if (paramCount > HighParamThreshold)
                metrics.HighParamMethods.Add($"{name} ({paramCount} params)");

            int methodMaxNesting = CalculateMaxNesting(method.Body ?? (SyntaxNode?)method.ExpressionBody ?? method);
            if (methodMaxNesting > maxNesting)
                maxNesting = methodMaxNesting;
        }

        metrics.MaxNestingDepth = maxNesting;
    }

    private int CalculateMaxNesting(SyntaxNode root)
    {
        int max = 0;
        CalculateNestingRecursive(root, 0, ref max);
        return max;
    }

    private void CalculateNestingRecursive(SyntaxNode node, int current, ref int max)
    {
        bool isNesting = node is IfStatementSyntax or WhileStatementSyntax or ForStatementSyntax
            or ForEachStatementSyntax or DoStatementSyntax or SwitchStatementSyntax
            or TryStatementSyntax;

        int depth = isNesting ? current + 1 : current;
        if (depth > max) max = depth;

        foreach (var child in node.ChildNodes())
            CalculateNestingRecursive(child, depth, ref max);
    }

    private bool IsExternalDependency(string typeName)
    {
        var baseName = typeName.Split('<')[0].TrimStart('I');
        return ExternalDependencyTypes.Any(ext =>
            typeName.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
            typeName.StartsWith($"I{ext}", StringComparison.OrdinalIgnoreCase)) ||
            ExternalDependencyTypes.Contains(typeName);
    }

    private bool DetectControllerOrEndpoint(ClassDeclarationSyntax classDecl)
    {
        var className = classDecl.Identifier.Text;
        if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            return true;

        if (classDecl.BaseList != null)
        {
            foreach (var baseType in classDecl.BaseList.Types)
            {
                var name = baseType.Type.ToString().Split('<')[0];
                if (ControllerBaseClasses.Contains(name))
                    return true;
            }
        }

        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (ControllerAttributes.Contains(name))
                    return true;
            }
        }

        return false;
    }

    private void DetermineStrategy(CodeMetrics metrics)
    {
        if (metrics.IsControllerOrEndpoint)
        {
            metrics.RecommendedStrategy = "Both";
            metrics.Reasoning = $"Class is a controller/endpoint with {metrics.DependencyCount} dependencies. " +
                                "Both unit and component tests are needed.";
            return;
        }

        if (metrics.DependencyCount > 0)
        {
            bool allInterfaces = metrics.InjectedDependencies.All(d => d.StartsWith("I") && char.IsUpper(d.ElementAtOrDefault(1)));
            metrics.RecommendedStrategy = "Both";
            metrics.Reasoning = $"Class has {metrics.DependencyCount} injected dependencies " +
                                $"({string.Join(", ", metrics.InjectedDependencies)}). " +
                                (allInterfaces
                                    ? "All are interface-typed, suitable for mocking. Both unit and component tests will be generated."
                                    : "Dependencies should be mocked in component tests. Unit tests will cover standalone logic.");
            return;
        }

        if (metrics.CyclomaticComplexity <= 1)
        {
            metrics.RecommendedStrategy = "Skip";
            metrics.Reasoning = "Cyclomatic complexity is 1 or less — no meaningful logic to test.";
            return;
        }

        metrics.RecommendedStrategy = "Unit";
        metrics.Reasoning = $"Pure logic class with cyclomatic complexity {metrics.CyclomaticComplexity} " +
                            "and no external dependencies. Only unit tests are appropriate.";
    }
}
