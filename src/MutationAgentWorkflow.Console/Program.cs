using Microsoft.Extensions.Configuration;
using MutationAgentWorkflow.Agents;
using MutationAgentWorkflow.Core.Models;
using MutationAgentWorkflow.Tools;
using System.Diagnostics;

namespace MutationAgentWorkflow.Console;

class Program
{
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("=== Mutation-Guided Agentic Test Generation Workflow ===\n");

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var apiKey = config["OpenAI:ApiKey"] ?? throw new Exception("OpenAI API key not found in appsettings.json");
        var model = config["OpenAI:Model"] ?? "gpt-5.4-nano";
        var maxIterations = int.TryParse(config["Workflow:MaxIterations"], out var mi) ? mi : 3;
        var targetScore = double.TryParse(config["Workflow:TargetMutationScore"], out var ts) ? ts : 80.0;

        var codeUnderTest = await LoadCodeUnderTestAsync(config);

        var planningAgent = new TestPlanningAgent(apiKey, model);
        var generationAgent = new TestGenerationAgent(apiKey, model);
        var mutationAgent = new MutationAnalysisAgent();
        var improvementAgent = new TestImprovementAgent(apiKey, model);
        var scaffolder = new TestProjectScaffolder();

        var stopwatch = Stopwatch.StartNew();
        var workflowResult = new WorkflowResult();

        try
        {
            // ===== STAGE 1: Test Planning (deterministic strategy + AI suggestions) =====
            PrintStageHeader(1, "Test Planning");

            var testPlan = await planningAgent.GeneratePlanAsync(codeUnderTest);

            PrintMetrics(testPlan);

            if (testPlan.Strategy == "Skip")
            {
                System.Console.WriteLine("  Strategy: SKIP — no meaningful logic to test.");
                System.Console.WriteLine($"  Reason: {testPlan.Metrics?.Reasoning}\n");
                System.Console.WriteLine("Workflow complete — nothing to test.");
                return;
            }

            System.Console.WriteLine($"  AI Suggestions:\n{Indent(testPlan.Suggestion, 4)}\n");

            // ===== STAGE 2: Test Generation =====
            PrintStageHeader(2, "Test Generation");

            var testSuite = await generationAgent.GenerateTestsAsync(testPlan, codeUnderTest);
            System.Console.WriteLine($"  Generated {testSuite.TestFilePath}");
            System.Console.WriteLine($"  ({CountLines(testSuite.TestCode)} lines of test code)\n");

            workflowResult.TotalTestsGenerated++;

            // ===== STAGE 3: Scaffold & Build =====
            PrintStageHeader(3, "Project Scaffolding");

            var scaffold = await scaffolder.ScaffoldAsync(
                codeUnderTest.SourceCode,
                codeUnderTest.ClassName,
                testSuite.TestCode,
                testPlan.Strategy);

            if (!scaffold.BuildSucceeded)
            {
                System.Console.WriteLine("  Build FAILED. Generated tests have compilation errors.");
                System.Console.WriteLine($"  Output:\n{Indent(scaffold.BuildOutput, 4)}");
                System.Console.WriteLine("\n  Attempting to fix via re-generation...\n");

                testSuite = await generationAgent.GenerateTestsAsync(testPlan, codeUnderTest);
                await scaffolder.UpdateTestCode(scaffold, testSuite.TestCode, codeUnderTest.ClassName);

                if (!scaffold.BuildSucceeded)
                {
                    System.Console.WriteLine("  Build still FAILED after re-generation. Exiting.");
                    return;
                }
            }

            System.Console.WriteLine($"  Build succeeded.");
            System.Console.WriteLine($"  Temp project: {scaffold.SolutionDir}\n");

            // ===== STAGE 4: Iterative Mutation Testing & Improvement =====
            PrintStageHeader(4, $"Mutation Testing Loop (max {maxIterations} iterations, target {targetScore}%)");

            MutationReport? lastReport = null;

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                System.Console.WriteLine($"  --- Iteration {iteration}/{maxIterations} ---");

                var report = await mutationAgent.RunAnalysisAsync(scaffold.TestProjectPath, scaffold.SourceProjectPath);
                lastReport = report;

                if (iteration == 1)
                    workflowResult.InitialMutationScore = report.MutationScore;

                PrintMutationReport(report);
                workflowResult.IterationsCompleted = iteration;

                if (report.MutationScore >= targetScore)
                {
                    System.Console.WriteLine($"  Target mutation score ({targetScore}%) reached!\n");
                    break;
                }

                if (report.SurvivedMutants == 0)
                {
                    System.Console.WriteLine("  No survived mutants. Nothing to improve.\n");
                    break;
                }

                if (iteration < maxIterations)
                {
                    System.Console.WriteLine("  Improving tests to kill survived mutants...\n");
                    var improvedCode = await improvementAgent.ImproveTestsAsync(report, testSuite, codeUnderTest, testPlan);
                    testSuite.TestCode = improvedCode;

                    await scaffolder.UpdateTestCode(scaffold, improvedCode, codeUnderTest.ClassName);

                    if (!scaffold.BuildSucceeded)
                    {
                        System.Console.WriteLine("  Improved tests failed to build. Stopping iteration.");
                        break;
                    }

                    System.Console.WriteLine("  Improved tests compiled successfully. Re-running Stryker...\n");
                }
            }

            // ===== Final Report =====
            stopwatch.Stop();
            workflowResult.FinalMutationScore = lastReport?.MutationScore ?? 0;
            workflowResult.TotalDuration = stopwatch.Elapsed;

            PrintFinalReport(workflowResult);

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), testSuite.TestFilePath);
            await File.WriteAllTextAsync(outputPath, testSuite.TestCode);
            System.Console.WriteLine($"Final test file saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\nError: {ex.Message}");
            System.Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }

    private static async Task<CodeUnderTest> LoadCodeUnderTestAsync(IConfiguration config)
    {
        var sourceFilePath = config["CodeUnderTest:SourceFile"];
        if (!string.IsNullOrWhiteSpace(sourceFilePath))
            sourceFilePath = Path.IsPathRooted(sourceFilePath)
                ? sourceFilePath
                : Path.Combine(Directory.GetCurrentDirectory(), sourceFilePath.Trim());

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            var baseDir = Directory.GetCurrentDirectory();
            var fallback1 = Path.Combine(baseDir, "..", "MutationAgentWorkflow.Sample", "PasswordValidator.cs");
            var fallback2 = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MutationAgentWorkflow.Sample", "PasswordValidator.cs");
            if (File.Exists(fallback1))
                sourceFilePath = Path.GetFullPath(fallback1);
            else if (File.Exists(fallback2))
                sourceFilePath = Path.GetFullPath(fallback2);
            else
                throw new FileNotFoundException(
                    "Code-under-test source file not found. Set CodeUnderTest:SourceFile in appsettings.json.");
        }

        var sourceCode = await File.ReadAllTextAsync(sourceFilePath);
        var className = config["CodeUnderTest:ClassName"];
        if (string.IsNullOrWhiteSpace(className))
            className = Path.GetFileNameWithoutExtension(sourceFilePath);

        System.Console.WriteLine($"Source file: {sourceFilePath}");
        System.Console.WriteLine($"Class name:  {className}\n");

        return new CodeUnderTest
        {
            SourceCode = sourceCode,
            ClassName = className,
            FilePath = Path.GetFileName(sourceFilePath)
        };
    }

    private static void PrintStageHeader(int stage, string name)
    {
        System.Console.WriteLine($"[STAGE {stage}] {name}");
        System.Console.WriteLine(new string('-', 50));
    }

    private static void PrintMetrics(TestPlan plan)
    {
        var m = plan.Metrics;
        if (m is null) return;

        System.Console.WriteLine($"  Strategy:             {plan.Strategy}");
        System.Console.WriteLine($"  Cyclomatic complexity: {m.CyclomaticComplexity}");
        System.Console.WriteLine($"  Dependencies:          {m.DependencyCount} ({(m.InjectedDependencies.Count > 0 ? string.Join(", ", m.InjectedDependencies) : "none")})");
        System.Console.WriteLine($"  Controller/endpoint:   {m.IsControllerOrEndpoint}");
        System.Console.WriteLine($"  Reasoning:             {m.Reasoning}\n");
    }

    private static void PrintMutationReport(MutationReport report)
    {
        System.Console.WriteLine($"  Mutation Score: {report.MutationScore}%");
        System.Console.WriteLine($"  Total: {report.TotalMutants} | Killed: {report.KilledMutants} | Survived: {report.SurvivedMutants}");

        foreach (var mutant in report.SurvivedMutantDetails.Take(10))
        {
            System.Console.WriteLine($"    - [{mutant.MutationType}] {mutant.Location}");
            if (!string.IsNullOrWhiteSpace(mutant.OriginalCode))
                System.Console.WriteLine($"      '{mutant.OriginalCode}' -> '{mutant.MutatedCode}'");
        }

        if (report.SurvivedMutantDetails.Count > 10)
            System.Console.WriteLine($"    ... and {report.SurvivedMutantDetails.Count - 10} more survived mutants.");

        System.Console.WriteLine();
    }

    private static void PrintFinalReport(WorkflowResult result)
    {
        System.Console.WriteLine("\n=== WORKFLOW SUMMARY ===");
        System.Console.WriteLine($"  Initial Mutation Score: {result.InitialMutationScore}%");
        System.Console.WriteLine($"  Final Mutation Score:   {result.FinalMutationScore}%");
        System.Console.WriteLine($"  Score Improvement:      +{result.FinalMutationScore - result.InitialMutationScore}%");
        System.Console.WriteLine($"  Tests Generated:        {result.TotalTestsGenerated}");
        System.Console.WriteLine($"  Iterations Completed:   {result.IterationsCompleted}");
        System.Console.WriteLine($"  Total Duration:         {result.TotalDuration.TotalSeconds:F1}s");
        System.Console.WriteLine(new string('=', 40) + "\n");
    }

    private static string Indent(string text, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join("\n", text.Split('\n').Select(line => prefix + line));
    }

    private static int CountLines(string text) => text.Split('\n').Length;
}
