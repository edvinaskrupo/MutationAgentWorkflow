using System.Diagnostics;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Tools;

public class DotNetTestRunner
{
    public async Task<TestSuite> RunTestsAsync(string projectPath)
    {
        var result = new TestSuite();

        try
        {
            // Build first
            var buildOutput = await RunCommandAsync("dotnet", $"build \"{projectPath}\"");
            result.CompilesSuccessfully = !buildOutput.Contains("error");

            if (!result.CompilesSuccessfully)
            {
                result.CompilationErrors.Add(buildOutput);
                return result;
            }

            // Run tests
            var testOutput = await RunCommandAsync("dotnet", $"test \"{projectPath}\" --no-build");
            result.AllTestsPass = testOutput.Contains("Passed!");

            // Parse test count (simple regex would be better)
            if (testOutput.Contains("Passed!"))
            {
                result.TestCount = 1; // Simplified
            }
        }
        catch (Exception ex)
        {
            result.CompilationErrors.Add(ex.Message);
        }

        return result;
    }

    private async Task<string> RunCommandAsync(string fileName, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output + error;
    }
}