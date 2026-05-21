using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Agents;

public class IntegrationTestGenerationAgent
{
    private readonly Kernel _kernel;
    public string Name => "Integration Test Generation Agent";

    public IntegrationTestGenerationAgent(string apiKey, string model = "gpt-5.4-mini")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, apiKey);
        _kernel = builder.Build();
    }

    public async Task<TestSuite> GenerateTestsAsync(TestPlan plan, CodeUnderTest code)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var prompt = BuildPrompt(plan, code);

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var result = await chatService.GetChatMessageContentAsync(history);
        var testCode = result.Content ?? "// No tests generated";

        testCode = StripMarkdownFences(testCode);

        return new TestSuite
        {
            TestCode = testCode,
            TestFilePath = $"{code.ClassName}IntegrationTests.cs",
            TestType = "Integration"
        };
    }

    private string BuildPrompt(TestPlan plan, CodeUnderTest code)
    {
        var deps = plan.Metrics?.InjectedDependencies ?? new List<string>();
        var depsList = deps.Count > 0
            ? string.Join(", ", deps)
            : "identified dependencies";

        var metricsSection = "";
        if (plan.Metrics is not null)
        {
            var perMethodCc = plan.Metrics.MethodComplexities.Count > 0
                ? string.Join(", ", plan.Metrics.MethodComplexities
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}: {kv.Value}"))
                : "N/A";

            metricsSection = $@"
CODE METRICS:
- Total cyclomatic complexity: {plan.Metrics.CyclomaticComplexity}
- Per-method complexity (highest first): {perMethodCc}
- Injected dependencies: {depsList}
- Is controller/endpoint: {plan.Metrics.IsControllerOrEndpoint}
";
        }

        return $@"You are an expert C# integration test developer. Generate xUnit INTEGRATION (component) tests for the code below.

TEST STRATEGY: Integration (komponentų testai)
{metricsSection}
PLANNING SUGGESTIONS:
{plan.Suggestion}

CODE TO TEST:
{code.SourceCode}

MOCKING REQUIREMENTS:
- Use the Moq library (Mock<T>) to mock ALL injected dependencies: {depsList}.
- Include ""using Moq;"" at the top.
- In the Arrange section, create mocks with MockBehavior.Strict, set up behaviors with .Setup(...).
- In the Assert section, HEAVILY verify mock interactions:
  * Use .Verify(..., Times.Once) or .Verify(..., Times.Never) for EVERY mock method that could be called.
  * Verify that data structures passed between components are valid (check property values with It.Is<T>()).
  * Verify that information flows correctly: the output of one dependency call is correctly used as input to the next.
- The Assert section should be the LARGEST part of each test.

STRICT REQUIREMENTS:
1. Use the xUnit framework.
2. Every test method MUST be structured with explicit comment sections:
   // Arrange
   // Act
   // Assert
3. Use descriptive test method names following the pattern: MethodName_Scenario_ExpectedBehavior.
4. Use [Fact] for single-case tests and [Theory] with [InlineData(...)] for parameterized tests.
5. Test both happy paths and edge cases (null inputs, boundary values).
6. Include all necessary using statements at the top of the file.
7. Focus on component interaction correctness, not just return values.

Generate ONLY the complete test class code. No explanations, no markdown fences.";
    }

    private static string StripMarkdownFences(string code)
    {
        var lines = code.Split('\n').ToList();
        if (lines.Count > 0 && lines[0].TrimStart().StartsWith("```"))
            lines.RemoveAt(0);
        if (lines.Count > 0 && lines[^1].TrimStart().StartsWith("```"))
            lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines);
    }
}
