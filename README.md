# Microsoft Agent Framework Demo Console Application

Console application to demonstrate the [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) with a set of focused single-agent and multi-agent workflow demos.

![header](/docs/header.png)

## Global Azure Munich 2026

I presented this repository as part of my speaker session at [Global Azure Munich 2026](https://global26.azuredev.org/).

- Session title: _More than just a Prompt: Multi-Agent Orchestration with the Microsoft Agent Framework_

The slides for the session are available in the `/docs` folder:

- [docs/GlobalAzure2026-Munich-MoreThanJustAPrompt.pdf](docs/GlobalAzure2026-Munich-MoreThanJustAPrompt.pdf)

## What This Repository Covers

This sample console application shows how to use the Microsoft Agent Framework for:

- single-agent interactions
- conversation threads and session state
- provider abstraction across Azure OpenAI, GitHub Models, and Ollama
- tool-enabled agents with live external data
- multi-agent orchestration with sequential, concurrent, handoff, group-chat, and planner-led workflows

## Prerequisites

- .NET SDK `10.0.201` or a compatible SDK selected through `global.json`
- Access to Azure OpenAI for the majority of demos
- Optional access to GitHub Models, Ollama, and OpenWeatherMap depending on the demo you want to run

## Running The Application

1. Configure the required .NET User Secrets for `src/MAFDemo/MAFDemo.csproj`.
2. Run the console app:

	```powershell
	dotnet run --project src/MAFDemo/MAFDemo.csproj
	```

3. Select a demo from the menu.

The application can start without secrets, but each demo validates its own requirements before execution and will show which settings are still missing.

## Required User Secrets

The application reads configuration from .NET User Secrets. For GitHub Models authentication, `GITHUB_TOKEN` can be used as an alternative to `GitHubModels:Token` or `GitHubModels:ApiKey`.

### Required For Most Demos

These settings are needed for demos `01`, `02`, `05`, `06`, `07`, `08`, and `09`:

- `AzureOpenAI:Endpoint`
- `AzureOpenAI:ApiKey`
- `AzureOpenAI:Deployment`

### Required For Demo 03 - Different Providers

To run the full provider comparison, configure all of the following:

- `AzureOpenAI:Endpoint`
- `AzureOpenAI:ApiKey`
- `AzureOpenAI:Deployment`
- `GitHubModels:Model` or `GitHubModels:Deployment`
- `GitHubModels:Token` or `GITHUB_TOKEN`
- `Ollama:Endpoint`
- `Ollama:Deployment`

Optional GitHub Models settings:

- `GitHubModels:Endpoint`
- `GitHubModels:Organization`

### Required For Demo 04 - Agents With Tools

- `AzureOpenAI:Endpoint`
- `AzureOpenAI:ApiKey`
- `AzureOpenAI:Deployment`
- `OpenWeatherMapApiKey`

### Example User Secrets Setup

```powershell
dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "AzureOpenAI:Endpoint" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "AzureOpenAI:ApiKey" "<your-azure-openai-key>"
dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "AzureOpenAI:Deployment" "<your-chat-deployment>"

dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "GitHubModels:Model" "openai/gpt-4.1-mini"
dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "GitHubModels:Token" "<your-github-models-token>"

dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "Ollama:Endpoint" "http://localhost:11434"
dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "Ollama:Deployment" "llama3.2"

dotnet user-secrets set --project src/MAFDemo/MAFDemo.csproj "OpenWeatherMapApiKey" "<your-openweathermap-key>"
```

## Demo Overview

### Demo 01 - Simple Agent

File: [src/MAFDemo/Demos/01_SimpleAgent.cs](src/MAFDemo/Demos/01_SimpleAgent.cs)

Reason: This is the smallest useful Microsoft Agent Framework example in the repository and establishes the baseline before introducing sessions, tools, or orchestration.

Purpose: A single Azure OpenAI-backed agent takes one travel request and turns it into a polished cruise proposal.

### Demo 02 - Simple Agent with Conversation Thread

File: [src/MAFDemo/Demos/02_SimpleAgentWithConversationThread.cs](src/MAFDemo/Demos/02_SimpleAgentWithConversationThread.cs)

Reason: A one-shot prompt is not enough for most real workflows, so this demo shows how session state changes the behavior of an agent across multiple turns.

Purpose: The agent stores project context, keeps a running transcript, serializes the session state, and answers follow-up questions using what was established earlier in the same conversation.

### Demo 03 - Simple Agent with Different Providers

File: [src/MAFDemo/Demos/03_SimpleAgentWithDifferentProviders.cs](src/MAFDemo/Demos/03_SimpleAgentWithDifferentProviders.cs)

Reason: Provider flexibility is an important part of the framework story, so this demo shows that the same agent pattern can be reused across different backends.

Purpose: The application runs equivalent storyteller agents against Azure OpenAI, Ollama, and GitHub Models and shows how each provider responds, including isolated error handling per provider.

### Demo 04 - Agents with Tools

File: [src/MAFDemo/Demos/04_AgentsWithTools.cs](src/MAFDemo/Demos/04_AgentsWithTools.cs)

Reason: Agents become more useful when they can call tools instead of relying only on model knowledge.

Purpose: This demo combines an Azure OpenAI agent with OpenWeatherMap and timezone lookups so the agent can answer weather and current-time questions with live external data.

### Demo 05 - Multi-Agent Workflow (Sequential)

File: [src/MAFDemo/Demos/05_MultiAgentWorkflowSequential.cs](src/MAFDemo/Demos/05_MultiAgentWorkflowSequential.cs)

Reason: Some delivery workflows are predictable and benefit from a fixed pipeline where each specialist improves the output in a known order.

Purpose: A request for an identity rollout brief flows through scope analysis, solution drafting, architecture review, and executive editing before the final result is shown.

### Demo 06 - Multi-Agent Workflow (Concurrent)

File: [src/MAFDemo/Demos/06_MultiAgentWorkflowConcurrent.cs](src/MAFDemo/Demos/06_MultiAgentWorkflowConcurrent.cs)

Reason: Independent perspectives do not always need to wait for each other, so this demo highlights the value of parallel fan-out.

Purpose: Multiple specialists analyze the same rollout update at the same time, and the console shows lane-by-lane timing plus the final outputs from leadership, risk, and signal-analysis perspectives.

### Demo 07 - Multi-Agent Workflow (Handoff)

File: [src/MAFDemo/Demos/07_MultiAgentWorkflowHandoff.cs](src/MAFDemo/Demos/07_MultiAgentWorkflowHandoff.cs)

Reason: Real work often shifts between specialists dynamically, so a fixed sequence is not always the right orchestration model.

Purpose: The workflow routes an identity rollout request between identity, security, support, and rollout leads, showing why ownership moves and how the final recommendation is consolidated.

### Demo 08 - Multi-Agent Workflow (Group Chat)

File: [src/MAFDemo/Demos/08_MultiAgentWorkflowGroupChat.cs](src/MAFDemo/Demos/08_MultiAgentWorkflowGroupChat.cs)

Reason: Some scenarios are better represented as a live conversation than as a pipeline, especially when several specialists need to react to one another.

Purpose: The demo simulates an authentication incident bridge where a lead, an identity engineer, and a reliability engineer discuss the same issue until a stakeholder-ready final update is produced.

### Demo 09 - Magentic (Planner-led)

File: [src/MAFDemo/Demos/09_MagenticPlannerWorkflow.cs](src/MAFDemo/Demos/09_MagenticPlannerWorkflow.cs)

Reason: Planner-led orchestration is useful when a lead agent should decide the next step instead of following a static execution graph.

Purpose: A delivery lead creates a work plan, delegates precise tasks to architecture, risk, and rollout specialists one at a time, and then synthesizes the final proposal.
