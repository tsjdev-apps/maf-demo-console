using System.Text;
using System.Text.RegularExpressions;
using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace MAFDemo.Demos;

internal static partial class MagenticPlannerWorkflow
{
    private const string LeadAgentName = "DeliveryLead";

    private const string DefaultPrompt = """
        Create a compact delivery proposal for a supervised recovery approval service in our B2B identity platform:
        helpdesk supervisors should be able to approve a temporary second-factor reset for locked-out partner users.
        Constraints:
        - approval requires step-up authentication and dual control for high-risk tenants
        - every approval and reset must be audit-safe and exportable for compliance review
        - the approval-check API should stay below 250 ms p95
        - rollout must start with three pilot tenants and a kill switch
        Work like a planner-led engineering team and produce a crisp technical proposal.
        """;

    /// <summary>
    /// Runs a Magentic-style planner workflow in which a lead agent assigns work to specialists.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient();

        AIAgent deliveryLead = chatClient.AsAIAgent(
            name: LeadAgentName,
            instructions: """
                You are the delivery lead in a planner-driven identity engineering workflow.
                Start by outlining a short work plan, then delegate exactly one specialist at a time.
                Use this format whenever you delegate:

                WORKPLAN:
                - short step
                - short step
                ASSIGN: IdentityArchitect | RiskAnalyst | RolloutEngineer
                BRIEF: a precise technical task for that specialist

                After a specialist responds, decide the next best specialist or finish the workflow.
                When enough input exists, answer with:

                DECISION:
                a compact proposal with solution shape, delivery plan, major risks, and rollout guidance

                Keep the output technical, concise, and implementation-oriented.
                """);

        AIAgent identityArchitect = chatClient.AsAIAgent(
            name: "IdentityArchitect",
            instructions: """
                You are the identity architecture specialist.
                Answer only the task assigned by DeliveryLead.
                Focus on API shape, domain model, authorization boundaries, and performance-sensitive design choices.
                Keep the answer short and technical.
                """);

        AIAgent riskAnalyst = chatClient.AsAIAgent(
            name: "RiskAnalyst",
            instructions: """
                You are the risk and controls specialist.
                Answer only the task assigned by DeliveryLead.
                Focus on abuse resistance, dual control, auditability, compliance export, and failure modes.
                Keep the answer short and technical.
                """);

        AIAgent rolloutEngineer = chatClient.AsAIAgent(
            name: "RolloutEngineer",
            instructions: """
                You are the rollout and operations specialist.
                Answer only the task assigned by DeliveryLead.
                Focus on feature flags, pilot sequencing, monitoring, rollback safety, and operational verification.
                Keep the answer short and technical.
                """);

        Workflow plannerWorkflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(static agents =>
            {
                PlannerDirectedGroupChatManager manager = new(agents, LeadAgentName)
                {
                    MaximumIterationCount = 10
                };

                return manager;
            })
            .WithName("Planner-Led Recovery Proposal")
            .WithDescription("A planner-led workflow in which a delivery lead directs specialists turn by turn.")
            .AddParticipants([deliveryLead, identityArchitect, riskAnalyst, rolloutEngineer])
            .Build();

        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 09 - Magentic (Planner-led)",
                "Let a delivery lead plan the work, direct specialists, and consolidate the final recommendation.");
            ConsoleHelper.WriteInfoPanel("Example Prompt", DefaultPrompt, Color.Grey);

            string? userPrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter a custom planner-style request or press Enter to run the example shown above.[/]",
                true,
                false,
                false);

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                userPrompt = DefaultPrompt;
            }

            const bool showDetails = true;

            ConsoleHelper.ShowDemoStage(
                "Demo 09 - Magentic (Planner-led)",
                "Let a delivery lead plan the work, direct specialists, and consolidate the final recommendation.");
            ConsoleHelper.WriteInfoPanel("Request", userPrompt, Color.Grey);

            if (showDetails)
            {
                ConsoleHelper.WriteInfoPanel(
                    "Execution Mode",
                    "Planner mode is enabled by default, so lead assignments, specialist turns, and the final synthesis will be shown.",
                    Color.Blue);
            }

            List<ChatMessage>? finalMessages = null;
            string? finalResponse = null;
            string? lastAgentResponse = null;
            PlannerDirective? latestDirective = null;

            List<ChatMessage> initialMessages =
            [
                new(ChatRole.User, userPrompt)
            ];

            await using StreamingRun run = await InProcessExecution.Concurrent.RunStreamingAsync(
                plannerWorkflow,
                initialMessages,
                Guid.NewGuid().ToString(),
                CancellationToken.None);

            await run.TrySendMessageAsync(new TurnToken(emitEvents: showDetails));

            await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
            {
                switch (workflowEvent)
                {
                    case WorkflowStartedEvent:
                        if (showDetails)
                        {
                            ConsoleHelper.WriteMarkup("[blue]Planner-led workflow started.[/]");
                            Console.WriteLine();
                        }
                        break;

                    case ExecutorInvokedEvent invokedEvent:
                        if (showDetails)
                        {
                            string agentName = FormatExecutorName(invokedEvent.ExecutorId);

                            if (string.Equals(agentName, LeadAgentName, StringComparison.Ordinal))
                            {
                                ConsoleHelper.WriteMarkup(
                                    $"[{GetAgentColor(agentName)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(agentName)}[/] is planning the next move.");
                            }
                            else
                            {
                                string taskSummary = latestDirective?.Task ?? "Work on the assigned specialist task.";

                                ConsoleHelper.WriteMarkup(
                                    $"[{GetAgentColor(agentName)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(agentName)}[/] was delegated by [bold]{LeadAgentName}[/].");
                                ConsoleHelper.WriteMarkup(
                                    $"[grey]Task:[/] {Markup.Escape(taskSummary)}");
                            }

                            Console.WriteLine();
                        }
                        break;

                    case AgentResponseEvent responseEvent:
                    {
                            string agentName = FormatExecutorName(responseEvent.ExecutorId);
                            string? responseText = ExtractText(responseEvent.Response);

                        if (!string.IsNullOrWhiteSpace(responseText))
                        {
                            lastAgentResponse = responseText;
                        }

                        if (string.Equals(agentName, LeadAgentName, StringComparison.Ordinal))
                        {
                            latestDirective = PlannerDirective.TryParse(responseText);
                        }

                        if (showDetails && !string.IsNullOrWhiteSpace(responseText))
                        {
                            ConsoleHelper.WriteMarkup(
                                $"[grey]{Markup.Escape(agentName)} output:[/] {Markup.Escape(responseText)}");
                            Console.WriteLine();
                        }

                        break;
                    }

                    case ExecutorCompletedEvent completedEvent:
                        if (showDetails)
                        {
                            string agentName = FormatExecutorName(completedEvent.ExecutorId);
                            ConsoleHelper.WriteMarkup(
                                $"[{GetAgentColor(agentName)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(agentName)}[/] completed this step.");
                            Console.WriteLine();
                        }
                        break;

                    case WorkflowOutputEvent outputEvent:
                        finalMessages = outputEvent.As<List<ChatMessage>>();
                        break;

                    case WorkflowErrorEvent errorEvent:
                        ConsoleHelper.WriteError(errorEvent.Data?.ToString() ?? "The workflow failed.");
                        break;
                }
            }

            if (finalMessages is not null)
            {
                finalResponse = ExtractLatestAssistantText(finalMessages) ?? FormatConversation(finalMessages);
            }

            finalResponse ??= lastAgentResponse;

            if (showDetails && finalMessages is not null)
            {
                string plannerSummary = FormatPlannerSummary(finalMessages);
                if (!string.IsNullOrWhiteSpace(plannerSummary))
                {
                    ConsoleHelper.WriteInfoPanel("Lead Assignments", plannerSummary, Color.Blue);
                }
            }

            ConsoleHelper.WriteInfoPanel("Final Result", finalResponse ?? "No final output was produced.", Color.Red);

            string action = ConsoleHelper.SelectFromOptions(
            [
                "Run demo again",
                "Back to demo selection"
            ],
            "[yellow]What would you like to do next?[/]",
            false);

            if (action == "Back to demo selection")
            {
                return;
            }
        }
    }

    private static string FormatPlannerSummary(IEnumerable<ChatMessage> messages)
    {
        StringBuilder builder = new();
        int step = 1;

        foreach (ChatMessage message in messages.Where(message =>
                     message.Role == ChatRole.Assistant &&
                     string.Equals(message.AuthorName, LeadAgentName, StringComparison.Ordinal)))
        {
            PlannerDirective? directive = PlannerDirective.TryParse(message.Text);
            if (directive is null)
            {
                continue;
            }

            if (directive.IsFinal)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append("Final synthesis prepared by ");
                builder.Append(LeadAgentName);
                builder.Append('.');
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(step++);
            builder.Append(". ");
            builder.Append(LeadAgentName);
            builder.Append(" -> ");
            builder.Append(directive.NextAgent);
            builder.Append(": ");
            builder.Append(directive.Task);
        }

        return builder.ToString();
    }

    private static string? ExtractText(object? value)
    {
        return value switch
        {
            AgentResponse response => response.Text,
            AgentResponseUpdate update => update.Text,
            ChatMessage message => message.Text,
            _ => value?.ToString()
        };
    }

    private static string? ExtractLatestAssistantText(IEnumerable<ChatMessage> messages)
    {
        return messages
            .Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => message.Text)
            .LastOrDefault();
    }

    private static string FormatConversation(IEnumerable<ChatMessage> messages)
    {
        StringBuilder builder = new();

        foreach (ChatMessage message in messages.Where(message => !string.IsNullOrWhiteSpace(message.Text)))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            string author = message.AuthorName ?? message.Role.Value ?? "unknown";
            builder.Append(author);
            builder.Append(": ");
            builder.Append(message.Text);
        }

        return builder.ToString();
    }

    private static string GetAgentColor(string speaker)
    {
        return speaker switch
        {
            LeadAgentName => "deepskyblue1",
            "IdentityArchitect" => "gold1",
            "RiskAnalyst" => "springgreen3",
            "RolloutEngineer" => "grey70",
            _ => "white"
        };
    }

    private static string FormatExecutorName(string? executorId)
    {
        if (string.IsNullOrWhiteSpace(executorId))
        {
            return "Unknown executor";
        }

        int separatorIndex = executorId.LastIndexOf('_');
        return separatorIndex > 0
            ? executorId[..separatorIndex]
            : executorId;
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("HH:mm:ss.fff");
    }

    [GeneratedRegex(@"^\s*ASSIGN:\s*(?<name>[A-Za-z0-9]+)\s*$", RegexOptions.Multiline)]
    private static partial Regex NextAgentRegex();

    [GeneratedRegex(@"^\s*BRIEF:\s*(?<task>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex TaskRegex();

    private sealed class PlannerDirectedGroupChatManager : GroupChatManager
    {
        private readonly Dictionary<string, AIAgent> _agentsByName;
        private readonly string _leadAgentName;
        private readonly IReadOnlyList<AIAgent> _workersInFallbackOrder;

        public PlannerDirectedGroupChatManager(IReadOnlyList<AIAgent> agents, string leadAgentName)
        {
            _leadAgentName = leadAgentName;
            _agentsByName = agents.ToDictionary(GetAgentName, StringComparer.Ordinal);
            _workersInFallbackOrder = agents
                .Where(agent => !string.Equals(GetAgentName(agent), leadAgentName, StringComparison.Ordinal))
                .ToArray();
        }

        protected override ValueTask<AIAgent> SelectNextAgentAsync(
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken)
        {
            ChatMessage? lastAssistantMessage = history.LastOrDefault(message =>
                message.Role == ChatRole.Assistant &&
                !string.IsNullOrWhiteSpace(message.AuthorName));

            if (lastAssistantMessage is null)
            {
                return new ValueTask<AIAgent>(_agentsByName[_leadAgentName]);
            }

            if (string.Equals(lastAssistantMessage.AuthorName, _leadAgentName, StringComparison.Ordinal))
            {
                PlannerDirective? directive = PlannerDirective.TryParse(lastAssistantMessage.Text);
                if (directive?.NextAgent is not null &&
                    _agentsByName.TryGetValue(directive.NextAgent, out AIAgent? delegatedAgent))
                {
                    return new ValueTask<AIAgent>(delegatedAgent);
                }

                return new ValueTask<AIAgent>(GetFirstUnseenWorker(history));
            }

            return new ValueTask<AIAgent>(_agentsByName[_leadAgentName]);
        }

        protected override ValueTask<bool> ShouldTerminateAsync(
            IReadOnlyList<ChatMessage> history,
            CancellationToken cancellationToken)
        {
            ChatMessage? lastAssistantMessage = history.LastOrDefault(message =>
                message.Role == ChatRole.Assistant &&
                !string.IsNullOrWhiteSpace(message.AuthorName));

            bool plannerFinished = string.Equals(lastAssistantMessage?.AuthorName, _leadAgentName, StringComparison.Ordinal) &&
                lastAssistantMessage?.Text?.Contains("DECISION:", StringComparison.OrdinalIgnoreCase) == true;

            return new ValueTask<bool>(plannerFinished || IterationCount >= MaximumIterationCount);
        }

        private static string GetAgentName(AIAgent agent)
        {
            return agent.Name ?? throw new InvalidOperationException("All planner workflow agents must have a name.");
        }

        private AIAgent GetFirstUnseenWorker(IReadOnlyList<ChatMessage> history)
        {
            HashSet<string> seenAuthors = history
                .Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.AuthorName))
                .Select(message => message.AuthorName!)
                .ToHashSet(StringComparer.Ordinal);

            return _workersInFallbackOrder.FirstOrDefault(worker => !seenAuthors.Contains(GetAgentName(worker)))
                ?? _workersInFallbackOrder[0];
        }
    }

    private sealed class PlannerDirective
    {
        public string? NextAgent { get; init; }

        public string? Task { get; init; }

        public bool IsFinal { get; init; }

        public static PlannerDirective? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (text.Contains("DECISION:", StringComparison.OrdinalIgnoreCase))
            {
                return new PlannerDirective { IsFinal = true };
            }

            Match nextMatch = NextAgentRegex().Match(text);
            Match taskMatch = TaskRegex().Match(text);

            if (!nextMatch.Success && !taskMatch.Success)
            {
                return null;
            }

            return new PlannerDirective
            {
                NextAgent = nextMatch.Success ? nextMatch.Groups["name"].Value.Trim() : null,
                Task = taskMatch.Success ? taskMatch.Groups["task"].Value.Trim() : null,
                IsFinal = false
            };
        }
    }
}
