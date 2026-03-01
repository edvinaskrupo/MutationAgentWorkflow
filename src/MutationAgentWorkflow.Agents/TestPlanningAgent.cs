using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class TestPlanningAgent
{
    private readonly Kernel _kernel;
    public string Name => "Test Planning Agent";

    public TestPlanningAgent(string apiKey, string model = "gpt-4o")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, apiKey);
        _kernel = builder.Build();
    }

    public async Task<string> ExecuteAsync(string sourceCode)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var prompt = $@"You are a test planning expert. Analyze this C# code and suggest a test strategy.

CODE:
{sourceCode}

Provide:
1. Whether to write Unit tests or Integration tests (choose one)
2. What dependencies should be mocked
3. Which methods are most critical to test first
4. Brief reasoning

Format your response as:
STRATEGY: [Unit/Integration]
MOCKING: [list of dependencies to mock]
PRIORITY METHODS: [list of methods]
REASONING: [your reasoning]";

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content ?? "No response generated";
    }

    public async Task<TestPlan> GeneratePlanAsync(CodeUnderTest code)
    {
        var response = await ExecuteAsync(code.SourceCode);

        // Simple parsing (you can make this more robust)
        var plan = new TestPlan
        {
            Suggestion = response
        };

        if (response.Contains("STRATEGY: Unit", StringComparison.OrdinalIgnoreCase))
            plan.Strategy = "Unit";
        else if (response.Contains("STRATEGY: Integration", StringComparison.OrdinalIgnoreCase))
            plan.Strategy = "Integration";

        return plan;
    }
}