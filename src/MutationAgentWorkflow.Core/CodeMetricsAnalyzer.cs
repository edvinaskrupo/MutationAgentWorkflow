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

    public CodeMetrics Analyze(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl is null)
            return new CodeMetrics
            {
                RecommendedStrategy = "Skip",
                Reasoning = "No class declaration found in source."
            };

        var metrics = new CodeMetrics();

        metrics.CyclomaticComplexity = CalculateCyclomaticComplexity(classDecl);
        AnalyzeDependencies(classDecl, metrics);
        metrics.IsControllerOrEndpoint = DetectControllerOrEndpoint(classDecl);

        DetermineStrategy(metrics);

        return metrics;
    }

    private int CalculateCyclomaticComplexity(ClassDeclarationSyntax classDecl)
    {
        int complexity = 0;

        foreach (var method in classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            // Base complexity of 1 per method
            complexity += 1;

            foreach (var node in method.DescendantNodes())
            {
                complexity += node switch
                {
                    IfStatementSyntax => 1,
                    ElseClauseSyntax => 0, // else itself doesn't branch; the if does
                    WhileStatementSyntax => 1,
                    ForStatementSyntax => 1,
                    ForEachStatementSyntax => 1,
                    DoStatementSyntax => 1,
                    CaseSwitchLabelSyntax => 1,
                    CasePatternSwitchLabelSyntax => 1,
                    CatchClauseSyntax => 1,
                    ConditionalExpressionSyntax => 1, // ternary ?:
                    BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.LogicalAndExpression) => 1,
                    BinaryExpressionSyntax bin2 when bin2.IsKind(SyntaxKind.LogicalOrExpression) => 1,
                    BinaryExpressionSyntax bin3 when bin3.IsKind(SyntaxKind.CoalesceExpression) => 1,
                    ConditionalAccessExpressionSyntax => 1, // ?.
                    SwitchExpressionArmSyntax => 1,
                    _ => 0
                };
            }
        }

        return complexity;
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
            metrics.RecommendedStrategy = "Integration";
            metrics.Reasoning = $"Class is a controller/endpoint with {metrics.DependencyCount} dependencies. " +
                                "Integration tests are needed to verify component interactions.";
            return;
        }

        if (metrics.DependencyCount > 0)
        {
            bool allInterfaces = metrics.InjectedDependencies.All(d => d.StartsWith("I") && char.IsUpper(d.ElementAtOrDefault(1)));
            metrics.RecommendedStrategy = "Integration";
            metrics.Reasoning = $"Class has {metrics.DependencyCount} injected dependencies " +
                                $"({string.Join(", ", metrics.InjectedDependencies)}). " +
                                (allInterfaces
                                    ? "All are interface-typed, suitable for mocking in integration tests."
                                    : "Dependencies should be mocked in integration tests.");
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
                            "and no external dependencies. Unit tests are appropriate.";
    }
}
