# Mutation-Guided Agentic Test Generation Workflow

A Bachelor's thesis prototype demonstrating multi-agent workflow for improving unit test adequacy using mutation testing feedback.

## Architecture

- **Test Planning Agent**: Analyzes code and suggests test strategy
- **Test Generation Agent**: Generates xUnit tests based on the plan
- **Mutation Analysis Agent**: Coordinates mutation testing with Stryker.NET
- **Test Improvement Agent**: Suggests improvements based on survived mutants

## Prerequisites

1. **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **OpenAI API Key** - [Get one here](https://platform.openai.com/api-keys)
3. **Stryker.NET** (optional for full mutation testing):
```bash
   dotnet tool install -g dotnet-stryker
```

## Setup

1. Clone or create the project structure
2. Open `src/MutationAgentWorkflow.Console/appsettings.json`
3. Replace `YOUR_OPENAI_API_KEY_HERE` with your actual OpenAI API key
4. Build the solution:
```bash
   dotnet build
```

## Running the Prototype
```bash
cd src/MutationAgentWorkflow.Console
dotnet run
```

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