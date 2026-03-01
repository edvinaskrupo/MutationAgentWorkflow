# Mutation-Guided Agentic Test Generation Workflow

A Bachelor's thesis prototype demonstrating multi-agent workflow for improving unit test adequacy using mutation testing feedback.

## Architecture

- **Test Planning Agent**: Analyzes code and suggests test strategy
- **Test Generation Agent**: Generates xUnit tests based on the plan
- **Mutation Analysis Agent**: Coordinates mutation testing with Stryker.NET
- **Test Improvement Agent**: Suggests improvements based on survived mutants

## Prerequisites

1. **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
2. **OpenAI API Key** - [Get one here](https://platform.openai.com/api-keys)
3. **Stryker.NET** (optional for full mutation testing):
```bash
   dotnet tool install -g dotnet-stryker
```

## Setup

1. Clone or create the project structure.
2. Open `MutationAgentWorkflow/src/MutationAgentWorkflow.Console/appsettings.json` and set `OpenAI:ApiKey` to your API key (and optionally `OpenAI:Model`, e.g. `gpt-4o-mini`).
3. Build from the repository root (single solution including all workflow projects):
```bash
   dotnet build BachelorProject.sln
```
   Or from this folder: `dotnet build MutationAgentWorkflow.sln`

## Running the Prototype
```bash
cd MutationAgentWorkflow/src/MutationAgentWorkflow.Console
dotnet run
```

## Project structure

The repository root contains a single solution (`BachelorProject.sln`) with the four workflow projects (Core, Agents, Tools, Console). The nested `MutationAgentWorkflow.sln` in this folder can also be used. DotNetTestRunner and StrykerRunner exist in Tools for future integration but are not invoked in the current single-pass workflow.

## Current Limitations (Prototype Phase)

- Mutation testing uses simulated data (Stryker integration is stubbed)
- No iterative improvement loop (runs single pass)
- Test file is generated but not automatically compiled/run
- No actual project structure creation for test execution

## Extending This Prototype

### To add real Stryker integration:

1. Create a temporary test project structure
2. Write generated tests to actual .cs files
3. Run `dotnet stryker` command
4. Parse the JSON output from `StrykerOutput/reports/mutation-report.json`

### To add iterative improvement:

1. Add a loop in Program.cs after Stage 4
2. Re-generate tests based on improvement suggestions
3. Re-run mutation testing
4. Continue until target mutation score is reached

### To add different LLM providers:

Modify agent constructors to use Azure OpenAI, Anthropic Claude, or local models.

## Thesis Evaluation Metrics

Track these for your experiments:
- Mutation score improvement (initial vs final)
- Number of iterations needed
- Test compilation success rate
- Human edit requirements
- Time per iteration

## License

MIT (or as per your university requirements)