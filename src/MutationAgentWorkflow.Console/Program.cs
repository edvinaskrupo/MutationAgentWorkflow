using Microsoft.Extensions.Configuration;
using MutationAgentWorkflow.Agents;
using MutationAgentWorkflow.Core.Models;
using System.Diagnostics;

namespace MutationAgentWorkflow.Console;

class Program
{
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("=== Mutation-Guided Agentic Test Generation Workflow ===\n");

        // Load configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var apiKey = config["OpenAI:ApiKey"] ?? throw new Exception("OpenAI API key not found in appsettings.json");
        var model = config["OpenAI:Model"] ?? "gpt-4o";

        // Load code under test from a real class file (optional: set CodeUnderTest:SourceFile and CodeUnderTest:ClassName in appsettings.json)
        var sourceFilePath = config["CodeUnderTest:SourceFile"];
        if (!string.IsNullOrWhiteSpace(sourceFilePath))
            sourceFilePath = Path.IsPathRooted(sourceFilePath) ? sourceFilePath : Path.Combine(Directory.GetCurrentDirectory(), sourceFilePath.Trim());
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            var baseDir = Directory.GetCurrentDirectory();
            var fallback1 = Path.Combine(baseDir, "..", "MutationAgentWorkflow.Sample", "Calculator.cs");
            var fallback2 = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MutationAgentWorkflow.Sample", "Calculator.cs");
            if (File.Exists(fallback1))
                sourceFilePath = Path.GetFullPath(fallback1);
            else if (File.Exists(fallback2))
                sourceFilePath = Path.GetFullPath(fallback2);
            else
                throw new FileNotFoundException("Code-under-test source file not found. Set CodeUnderTest:SourceFile in appsettings.json to the path to a .cs file (e.g. ../MutationAgentWorkflow.Sample/Calculator.cs).");
        }

        var sourceCode = await File.ReadAllTextAsync(sourceFilePath);
        var className = config["CodeUnderTest:ClassName"];
        if (string.IsNullOrWhiteSpace(className))
            className = Path.GetFileNameWithoutExtension(sourceFilePath);

        var codeUnderTest = new CodeUnderTest
        {
            SourceCode = sourceCode,
            ClassName = className,
            FilePath = Path.GetFileName(sourceFilePath)
        };

        // Initialize agents
        var planningAgent = new TestPlanningAgent(apiKey, model);
        var generationAgent = new TestGenerationAgent(apiKey, model);
        var mutationAgent = new MutationAnalysisAgent();
        var improvementAgent = new TestImprovementAgent(apiKey, model);

        var stopwatch = Stopwatch.StartNew();
        var workflowResult = new WorkflowResult();

        try
        {
            // ===== STAGE 1: Test Planning =====
            System.Console.WriteLine("📋 STAGE 1: Test Planning");
            System.Console.WriteLine("─────────────────────────────────────────");

            var testPlan = await planningAgent.GeneratePlanAsync(codeUnderTest);
            System.Console.WriteLine(testPlan.Suggestion);
            System.Console.WriteLine();

            // Human-in-the-loop: Select strategy
            System.Console.WriteLine("Select test strategy:");
            System.Console.WriteLine("  1 = Unit Tests");
            System.Console.WriteLine("  2 = Integration Tests");
            System.Console.Write("Your choice: ");

            var choice = System.Console.ReadLine();
            testPlan.Strategy = choice == "2" ? "Integration" : "Unit";
            System.Console.WriteLine($"✓ Selected: {testPlan.Strategy} Tests\n");

            // ===== STAGE 2: Test Generation =====
            System.Console.WriteLine("🔨 STAGE 2: Test Generation");
            System.Console.WriteLine("─────────────────────────────────────────");

            var testSuite = await generationAgent.GenerateTestsAsync(testPlan, codeUnderTest);
            System.Console.WriteLine("Generated test code:");
            System.Console.WriteLine(testSuite.TestCode);
            System.Console.WriteLine();

            // Save the test file (optional)
            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), testSuite.TestFilePath);
            await File.WriteAllTextAsync(outputPath, testSuite.TestCode);
            System.Console.WriteLine($"✓ Test file saved to: {outputPath}\n");

            workflowResult.TotalTestsGenerated++;

            // ===== STAGE 3: Mutation Testing =====
            System.Console.WriteLine("🧬 STAGE 3: Mutation Analysis");
            System.Console.WriteLine("─────────────────────────────────────────");
            System.Console.WriteLine("NOTE: This is a prototype. In production, this would run Stryker.NET");
            System.Console.WriteLine("For now, using simulated mutation data...\n");

            // For real Stryker integration: use StrykerRunner.RunMutationTestingAsync(projectPath)
            // after creating a temp test project and writing generated tests to disk.
            var mutationReport = mutationAgent.ParseStrykerReport("");
            workflowResult.InitialMutationScore = mutationReport.MutationScore;

            System.Console.WriteLine($"Mutation Score: {mutationReport.MutationScore}%");
            System.Console.WriteLine($"Total Mutants: {mutationReport.TotalMutants}");
            System.Console.WriteLine($"Killed: {mutationReport.KilledMutants}");
            System.Console.WriteLine($"Survived: {mutationReport.SurvivedMutants}");
            System.Console.WriteLine();

            if (mutationReport.SurvivedMutantDetails.Any())
            {
                System.Console.WriteLine("Survived mutants:");
                foreach (var mutant in mutationReport.SurvivedMutantDetails)
                {
                    System.Console.WriteLine($"  • {mutant.MutationType} at {mutant.Location}");
                    System.Console.WriteLine($"    Original: {mutant.OriginalCode}");
                    System.Console.WriteLine($"    Mutated:  {mutant.MutatedCode}");
                }
                System.Console.WriteLine();
            }

            // ===== STAGE 4: Test Improvement =====
            if (mutationReport.SurvivedMutants > 0)
            {
                System.Console.WriteLine("💡 STAGE 4: Test Improvement Suggestions");
                System.Console.WriteLine("─────────────────────────────────────────");

                var improvements = await improvementAgent.SuggestImprovementsAsync(mutationReport, testSuite);
                System.Console.WriteLine(improvements);
                System.Console.WriteLine();
            }

            // ===== Final Report =====
            stopwatch.Stop();
            workflowResult.FinalMutationScore = mutationReport.MutationScore;
            workflowResult.TotalDuration = stopwatch.Elapsed;
            workflowResult.IterationsCompleted = 1;

            System.Console.WriteLine("📊 WORKFLOW SUMMARY");
            System.Console.WriteLine("═════════════════════════════════════════");
            System.Console.WriteLine($"Initial Mutation Score:  {workflowResult.InitialMutationScore}%");
            System.Console.WriteLine($"Final Mutation Score:    {workflowResult.FinalMutationScore}%");
            System.Console.WriteLine($"Tests Generated:         {workflowResult.TotalTestsGenerated}");
            System.Console.WriteLine($"Iterations:              {workflowResult.IterationsCompleted}");
            System.Console.WriteLine($"Total Duration:          {workflowResult.TotalDuration.TotalSeconds:F2}s");
            System.Console.WriteLine("═════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\n❌ Error: {ex.Message}");
            System.Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }

        System.Console.WriteLine("\nPress any key to exit...");
        System.Console.ReadKey();
    }
}