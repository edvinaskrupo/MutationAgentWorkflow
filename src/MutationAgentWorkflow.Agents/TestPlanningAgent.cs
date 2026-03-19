using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class TestPlanningAgent
{
    private readonly Kernel _kernel;
    private readonly CodeMetricsAnalyzer _metricsAnalyzer = new();
    public string Name => "Test Planning Agent";

    public TestPlanningAgent(string apiKey, string model = "gpt-4o")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, apiKey);
        _kernel = builder.Build();
    }

    public async Task<TestPlan> GeneratePlanAsync(CodeUnderTest code)
    {
        var metrics = _metricsAnalyzer.Analyze(code.SourceCode);

        var plan = new TestPlan
        {
            Strategy = metrics.RecommendedStrategy,
            Metrics = metrics
        };

        if (metrics.RecommendedStrategy == "Skip")
        {
            plan.Suggestion = metrics.Reasoning;
            return plan;
        }

        var suggestion = await GetAiSuggestionAsync(code.SourceCode, metrics);
        plan.Suggestion = suggestion;

        return plan;
    }

    private async Task<string> GetAiSuggestionAsync(string sourceCode, CodeMetrics metrics)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var depsInfo = metrics.InjectedDependencies.Count > 0
            ? $"Injected dependencies: {string.Join(", ", metrics.InjectedDependencies)}"
            : "No injected dependencies (pure logic class).";

        var prompt = $@"You are a test planning expert. The test strategy has already been determined to be ""{metrics.RecommendedStrategy}"" based on code metrics.

CODE METRICS:
- Cyclomatic complexity: {metrics.CyclomaticComplexity}
- {depsInfo}
- Is controller/endpoint: {metrics.IsControllerOrEndpoint}
- Strategy reasoning: {metrics.Reasoning}

CODE:
{sourceCode}

Based on this analysis, provide:
1. Which methods are most critical to test first (ordered by priority)
2. What edge cases and boundary conditions should be covered
3. Which dependencies should be mocked (if integration tests)

Format your response as:
PRIORITY METHODS: [list of methods with brief reason]
EDGE CASES: [list of edge cases to cover]
MOCKING: [list of dependencies to mock, or ""None"" for unit tests]";

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content ?? "No suggestions generated.";
    }
}
