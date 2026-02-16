namespace MutationAgentWorkflow.Core.Interfaces;

public interface IAgent
{
    string Name { get; }
    Task<string> ExecuteAsync(string input);
}