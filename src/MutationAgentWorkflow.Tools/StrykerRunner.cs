using System.Diagnostics;
using MutationAgentWorkflow.Core.Models;

namespace MutationAgentWorkflow.Tools;

public class StrykerRunner
{
    public async Task<MutationReport> RunMutationTestingAsync(string projectPath)
    {
        // NOTE: This requires Stryker.NET to be installed globally or locally
        // Install with: dotnet tool install -g dotnet-stryker

        try
        {
            var output = await RunCommandAsync("dotnet", $"stryker -p \"{projectPath}\" --reporter json");

            // TODO: Parse actual JSON output from Stryker
            // For prototype, return mock data
            return new MutationReport
            {
                MutationScore = 75.0,
                TotalMutants = 20,
                KilledMutants = 15,
                SurvivedMutants = 5
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stryker error: {ex.Message}");
            Console.WriteLine("Make sure Stryker.NET is installed: dotnet tool install -g dotnet-stryker");

            return new MutationReport { MutationScore = 0 };
        }
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
        await process.WaitForExitAsync();

        return output;
    }
}