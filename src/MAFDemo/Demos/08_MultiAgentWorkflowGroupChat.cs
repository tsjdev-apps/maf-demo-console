using System.Text;
using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace MAFDemo.Demos;

internal static class MultiAgentWorkflowGroupChat
{
    private const string DefaultPrompt = """
        We are on an authentication incident bridge for partner sign-in.
        Since 07:45 UTC, passkey completion for B2B users in eu-west dropped from 96% to 72% and the fallback OTP path is timing out for 4.1% of requests.
        Deployment auth-gateway 2026.04.15.2 completed 12 minutes before the spike. Token cache hit rate fell from 89% to 58%,
        while the session store in West Europe shows elevated write latency.
        Work together like an engineering response room: identify the most likely fault line, propose immediate containment,
        assign next technical actions, and finish with a short stakeholder-ready update.
        """;

    private static readonly string[] s_participantOrder =
    [
        "BridgeLead",
        "IdentityEngineer",
        "ReliabilityEngineer"
    ];

    /// <summary>
    /// Runs the group chat workflow demo in which multiple agents conduct a technical incident discussion.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient();

        AIAgent bridgeLead = chatClient.AsAIAgent(
            name: "BridgeLead",
            instructions: """
                You lead a focused authentication incident bridge.
                Keep each turn concise, build on what the others already said, and avoid repetition.
                Ask for or synthesize only the next most valuable point.
                When the team has enough signal, answer with 'FINAL UPDATE:' followed by:
                probable cause, immediate containment, owner actions, and a short stakeholder update.
                """);

        AIAgent identityEngineer = chatClient.AsAIAgent(
            name: "IdentityEngineer",
            instructions: """
                You are the identity engineer on the bridge.
                Focus on authentication flow behavior, token handling, recovery paths, and code-level regression clues.
                Respond in 2 to 4 short technical bullets and reference the incident context already discussed.
                """);

        AIAgent reliabilityEngineer = chatClient.AsAIAgent(
            name: "ReliabilityEngineer",
            instructions: """
                You are the reliability engineer on the bridge.
                Focus on rollout effects, infrastructure health, cache and session-store signals, and mitigation options.
                Respond in 2 to 4 short technical bullets and keep it suitable for live incident communication.
                """);

        Workflow groupChatWorkflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(static agents =>
            {
                RoundRobinGroupChatManager manager = new(
                    agents,
                    static (_, history, _) => new ValueTask<bool>(ContainsMarker(history, "FINAL UPDATE:")))
                {
                    MaximumIterationCount = 8
                };
                return manager;
            })
            .WithName("Authentication Incident Group Chat")
            .WithDescription("A technical group chat in which engineers investigate an authentication incident together.")
            .AddParticipants([bridgeLead, identityEngineer, reliabilityEngineer])
            .Build();

        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 08 - Multi-Agent Workflow (Group Chat)",
                "Let multiple agents discuss an authentication incident like a live engineering bridge.");
            ConsoleHelper.WriteInfoPanel("Example Prompt", DefaultPrompt, Color.Grey);

            string? userPrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter a custom authentication incident or press Enter to run the example shown above.[/]",
                true,
                false,
                false);

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                userPrompt = DefaultPrompt;
            }

            const bool showDetails = true;

            ConsoleHelper.ShowDemoStage(
                "Demo 08 - Multi-Agent Workflow (Group Chat)",
                "Let multiple agents discuss an authentication incident like a live engineering bridge.");
            ConsoleHelper.WriteInfoPanel("Incident Brief", userPrompt, Color.Grey);

            if (showDetails)
            {
                ConsoleHelper.WriteInfoPanel(
                    "Execution Mode",
                    "Live conversation mode is enabled by default, so every speaker turn in the incident bridge will be shown.",
                    Color.Blue);
            }

            List<ChatMessage>? finalMessages = null;
            string? finalResponse = null;
            string? lastAgentResponse = null;

            List<ChatMessage> initialMessages =
            [
                new(ChatRole.User, userPrompt)
            ];

            await using StreamingRun run = await InProcessExecution.Concurrent.RunStreamingAsync(
                groupChatWorkflow,
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
                            ConsoleHelper.WriteMarkup("[blue]Technical group chat started.[/]");
                            Console.WriteLine();
                        }
                        break;

                    case ExecutorInvokedEvent invokedEvent:
                        if (showDetails)
                        {
                            string speaker = FormatExecutorName(invokedEvent.ExecutorId);

                            ConsoleHelper.WriteMarkup(
                                $"[{GetAgentColor(speaker)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(speaker)}[/] joins the bridge.");
                            ConsoleHelper.WriteMarkup(
                                $"[grey]Role:[/] {Markup.Escape(DescribeRole(speaker))}");
                            Console.WriteLine();
                        }
                        break;

                    case AgentResponseEvent responseEvent:
                    {
                            string speaker = FormatExecutorName(responseEvent.ExecutorId);
                            string? responseText = ExtractText(responseEvent.Response);

                        if (!string.IsNullOrWhiteSpace(responseText))
                        {
                            lastAgentResponse = responseText;
                        }

                        if (showDetails && !string.IsNullOrWhiteSpace(responseText))
                        {
                            ConsoleHelper.WriteMarkup(
                                $"[grey]{Markup.Escape(speaker)} says:[/] {Markup.Escape(responseText)}");
                            Console.WriteLine();
                        }

                        break;
                    }

                    case ExecutorCompletedEvent completedEvent:
                        if (showDetails)
                        {
                            string speaker = FormatExecutorName(completedEvent.ExecutorId);
                            ConsoleHelper.WriteMarkup(
                                $"[{GetAgentColor(speaker)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(speaker)}[/] finished this turn.");
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
                ConsoleHelper.WriteInfoPanel(
                    "Conversation Flow",
                    FormatSpeakingTrail(finalMessages),
                    Color.Blue);
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

    private static bool ContainsMarker(IEnumerable<ChatMessage> history, string marker)
    {
        return history.Any(message =>
            !string.IsNullOrWhiteSpace(message.Text) &&
            message.Text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeRole(string speaker)
    {
        return speaker switch
        {
            "BridgeLead" => "Coordinates the bridge, keeps the discussion tight, and closes with the incident update.",
            "IdentityEngineer" => "Checks auth-flow regressions, token handling, and application-layer recovery paths.",
            "ReliabilityEngineer" => "Checks rollout health, infra signals, cache degradation, and mitigation options.",
            _ => "Contributes to the shared technical discussion."
        };
    }

    private static string GetAgentColor(string speaker)
    {
        return speaker switch
        {
            "BridgeLead" => "deepskyblue1",
            "IdentityEngineer" => "gold1",
            "ReliabilityEngineer" => "springgreen3",
            _ => "white"
        };
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

    private static string FormatSpeakingTrail(IEnumerable<ChatMessage> messages)
    {
        List<string> speakers = [];

        foreach (ChatMessage message in messages.Where(message => message.Role == ChatRole.Assistant))
        {
            string? author = message.AuthorName;
            if (string.IsNullOrWhiteSpace(author))
            {
                continue;
            }

            if (speakers.Count == 0 || !string.Equals(speakers[^1], author, StringComparison.Ordinal))
            {
                speakers.Add(author);
            }
        }

        return speakers.Count == 0
            ? string.Join(" -> ", s_participantOrder)
            : string.Join(" -> ", speakers);
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
}
