using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class TestGenerationAgent
{
    private readonly Kernel _kernel;
    public string Name => "Test Generation Agent";

    public TestGenerationAgent(string apiKey, string model = "gpt-4o")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, apiKey);
        _kernel = builder.Build();
    }

    public async Task<string> ExecuteAsync(string input)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var prompt = $@"You are an expert C# test developer. Generate xUnit tests for this code.

{input}

Requirements:
- Use xUnit framework
- Include proper assertions
- Test both happy paths and edge cases
- Use descriptive test method names
- Include all necessary using statements

Generate ONLY the test class code, no explanations.";

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content ?? "No tests generated";
    }

    public async Task<TestSuite> GenerateTestsAsync(TestPlan plan, CodeUnderTest code)
    {
        var input = $@"Test Strategy: {plan.Strategy}
{plan.Suggestion}

CODE TO TEST:
{code.SourceCode}";

        var testCode = await ExecuteAsync(input);

        return new TestSuite
        {
            TestCode = testCode,
            TestFilePath = $"{code.ClassName}Tests.cs"
        };
    }
}