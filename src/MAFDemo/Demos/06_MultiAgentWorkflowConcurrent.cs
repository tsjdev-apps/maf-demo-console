using System.Text;
using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace MAFDemo.Demos;

internal static class MultiAgentWorkflowConcurrent
{
    private const string DefaultPrompt = """
        Pilot feedback for the new passkey sign-in rollout:
        - Adoption reached 68% across 14 enterprise tenants in the first 10 days.
        - Sign-in success improved from 91% to 97%, but helpdesk tickets rose by 23%.
        - Most praise mentions faster sign-in and fewer SMS delays.
        - Most complaints come from shared-device scenarios and recovery confusion.
        - Two regulated customers asked for clearer audit reporting before full rollout.
        - Product wants a decision memo by Friday on whether to expand the pilot.
        """;

    private static readonly string[] s_agentOrder =
    [
        "LeadershipBrief",
        "RiskLens",
        "SignalSpotter"
    ];

    /// <summary>
    /// Runs the concurrent workflow demo in which multiple agents analyze the same input in parallel.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient();

        AIAgent leadershipBrief = chatClient.AsAIAgent(
            name: "LeadershipBrief",
            instructions: "Summarize the rollout update for leadership in exactly 3 short bullets. Highlight decision-relevant context.");

        AIAgent riskLens = chatClient.AsAIAgent(
            name: "RiskLens",
            instructions: "Identify the rollout risks, blockers, and recommended follow-up actions from the update. Keep it practical and concise.");

        AIAgent signalSpotter = chatClient.AsAIAgent(
            name: "SignalSpotter",
            instructions: "Extract the most important metrics, stakeholder signals, and trend indicators from the update.");

        Workflow concurrent = AgentWorkflowBuilder.BuildConcurrent(
            [leadershipBrief, riskLens, signalSpotter],
            null);

        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 06 - Multi-Agent Workflow (Concurrent)",
                "Run multiple rollout specialists in parallel and compare their perspectives on the same update.");
            ConsoleHelper.WriteInfoPanel("Example Prompt", DefaultPrompt, Color.Grey);

            string? userPrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter custom source text or press Enter to analyze the example shown above.[/]",
                true,
                false,
                false);

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                userPrompt = DefaultPrompt;
            }

            const bool showDetails = true;

            ConsoleHelper.ShowDemoStage(
                "Demo 06 - Multi-Agent Workflow (Concurrent)",
                "Run multiple rollout specialists in parallel and compare their perspectives on the same update.");
            ConsoleHelper.WriteInfoPanel("Rollout Update", userPrompt, Color.Grey);

            if (showDetails)
            {
                ConsoleHelper.WriteInfoPanel(
                    "Execution Mode",
                    "Parallel activity tracking is enabled by default, so start times, previews, and completion events will be shown for each specialist lane.",
                    Color.Blue);
            }

            Dictionary<string, DateTimeOffset> startedAt = [];
            Dictionary<string, DateTimeOffset> finishedAt = [];
            Dictionary<string, string> responses = [];
            List<ChatMessage>? aggregatedMessages = null;
            HashSet<string> activeAgents = [];

            List<ChatMessage> initialMessages =
            [
                new(ChatRole.User, userPrompt)
            ];

            await using StreamingRun run = await InProcessExecution.Concurrent.RunStreamingAsync(
                concurrent,
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
                            ConsoleHelper.WriteMarkup("[blue]Parallel workflow started.[/]");
                            Console.WriteLine();
                        }
                        break;

                    case ExecutorInvokedEvent invokedEvent:
                        if (showDetails)
                        {
                            string executorName = FormatExecutorName(invokedEvent.ExecutorId);
                            DateTimeOffset startTime = DateTimeOffset.Now;
                            startedAt[executorName] = startTime;
                            activeAgents.Add(executorName);

                            ConsoleHelper.WriteMarkup(
                                $"[{GetAgentColor(executorName)}]{FormatTimestamp(startTime)}[/] [bold]{Markup.Escape(executorName)}[/] started.");
                            ConsoleHelper.WriteMarkup(
                                $"[grey]Task:[/] {Markup.Escape(DescribeExecutorTask(executorName))}");
                            ConsoleHelper.WriteMarkup(
                                $"[grey]Parallel lanes active:[/] {activeAgents.Count}/{s_agentOrder.Length}");
                            Console.WriteLine();
                        }
                        break;

                    case AgentResponseEvent responseEvent:
                    {
                            string executorName = FormatExecutorName(responseEvent.ExecutorId);
                            string? responseText = ExtractText(responseEvent.Response);

                        if (!string.IsNullOrWhiteSpace(responseText))
                        {
                            responses[executorName] = responseText;
                        }

                        if (showDetails && !string.IsNullOrWhiteSpace(responseText))
                        {
                            ConsoleHelper.WriteMarkup(
                                $"[grey]Result preview from {Markup.Escape(executorName)}:[/] {Markup.Escape(Truncate(responseText, 160))}");
                        }

                        break;
                    }

                    case ExecutorCompletedEvent completedEvent:
                        if (showDetails)
                        {
                            string executorName = FormatExecutorName(completedEvent.ExecutorId);
                            DateTimeOffset endTime = DateTimeOffset.Now;
                            finishedAt[executorName] = endTime;
                            activeAgents.Remove(executorName);

                            TimeSpan duration = startedAt.TryGetValue(executorName, out DateTimeOffset startTime)
                                ? endTime - startTime
                                : TimeSpan.Zero;

                            ConsoleHelper.WriteMarkup(
                                $"[{GetAgentColor(executorName)}]{FormatTimestamp(endTime)}[/] [bold]{Markup.Escape(executorName)}[/] finished in {FormatDuration(duration)}.");
                            ConsoleHelper.WriteMarkup(
                                $"[grey]Still running:[/] {activeAgents.Count}/{s_agentOrder.Length}");
                            Console.WriteLine();
                        }
                        break;

                    case WorkflowOutputEvent outputEvent:
                        aggregatedMessages = outputEvent.As<List<ChatMessage>>();
                        break;

                    case WorkflowErrorEvent errorEvent:
                        ConsoleHelper.WriteError(errorEvent.Data?.ToString() ?? "The workflow failed.");
                        break;
                }
            }

            if (showDetails)
            {
                RenderTimeline(startedAt, finishedAt);
            }

            Dictionary<string, string> finalResults = BuildFinalResults(responses, aggregatedMessages);

            foreach (string agentName in s_agentOrder)
            {
                if (!finalResults.TryGetValue(agentName, out string? result) || string.IsNullOrWhiteSpace(result))
                {
                    continue;
                }

                ConsoleHelper.WriteInfoPanel(agentName, result, GetAgentPanelColor(agentName));
            }

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

    /// <summary>
    /// Merges direct agent responses with any aggregated workflow messages to produce the final per-agent result map.
    /// </summary>
    /// <param name="responses">The responses collected directly from agent events.</param>
    /// <param name="aggregatedMessages">The aggregated workflow output messages, if available.</param>
    /// <returns>A dictionary keyed by agent name containing the final text for each agent.</returns>
    private static Dictionary<string, string> BuildFinalResults(
        Dictionary<string, string> responses,
        List<ChatMessage>? aggregatedMessages)
    {
        Dictionary<string, string> finalResults = new(responses, StringComparer.Ordinal);

        if (aggregatedMessages is null)
        {
            return finalResults;
        }

        foreach (ChatMessage message in aggregatedMessages.Where(message => !string.IsNullOrWhiteSpace(message.Text)))
        {
            string? author = message.AuthorName;
            if (string.IsNullOrWhiteSpace(author))
            {
                continue;
            }

            finalResults[author] = message.Text;
        }

        return finalResults;
    }

    /// <summary>
    /// Renders a simple start/finish timeline for each concurrently running agent.
    /// </summary>
    /// <param name="startedAt">The timestamp recorded when each agent started.</param>
    /// <param name="finishedAt">The timestamp recorded when each agent finished.</param>
    private static void RenderTimeline(
        Dictionary<string, DateTimeOffset> startedAt,
        Dictionary<string, DateTimeOffset> finishedAt)
    {
        if (startedAt.Count == 0)
        {
            return;
        }

        StringBuilder builder = new();

        foreach (string? agentName in s_agentOrder.Where(startedAt.ContainsKey))
        {
            DateTimeOffset start = startedAt[agentName];
            string started = FormatTimestamp(start);
            string ended = finishedAt.TryGetValue(agentName, out DateTimeOffset finish)
                ? FormatTimestamp(finish)
                : "--:--:--.---";
            string duration = finishedAt.TryGetValue(agentName, out DateTimeOffset completed)
                ? FormatDuration(completed - start)
                : "still running";

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(agentName);
            builder.Append("  start ");
            builder.Append(started);
            builder.Append("  finish ");
            builder.Append(ended);
            builder.Append("  duration ");
            builder.Append(duration);
        }

        ConsoleHelper.WriteInfoPanel("Parallel Timeline", builder.ToString(), Color.Blue);
    }

    /// <summary>
    /// Extracts text from a workflow payload that may contain agent responses, updates, messages, or plain objects.
    /// </summary>
    /// <param name="value">The workflow payload to inspect.</param>
    /// <returns>The extracted text when available; otherwise, <see langword="null"/>.</returns>
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

    /// <summary>
    /// Returns the human-readable task description shown for a concurrent workflow executor.
    /// </summary>
    /// <param name="executorName">The logical executor name.</param>
    /// <returns>A console-friendly description of the executor's role.</returns>
    private static string DescribeExecutorTask(string executorName)
    {
        return executorName switch
        {
            "LeadershipBrief" => "Builds a short leadership summary focused on rollout decisions.",
            "RiskLens" => "Surfaces blockers, rollout concerns, and next actions from the same update.",
            "SignalSpotter" => "Pulls out metrics, customer signals, and trend indicators from the source text.",
            _ => "Processes the shared input in parallel."
        };
    }

    /// <summary>
    /// Resolves the display color used for a specific concurrent workflow agent.
    /// </summary>
    /// <param name="executorName">The logical executor name.</param>
    /// <returns>A Spectre.Console color name.</returns>
    private static string GetAgentColor(string executorName)
    {
        return executorName switch
        {
            "LeadershipBrief" => "deepskyblue1",
            "RiskLens" => "gold1",
            "SignalSpotter" => "springgreen3",
            _ => "white"
        };
    }

    /// <summary>
    /// Resolves the panel border color used for a specific concurrent workflow agent.
    /// </summary>
    /// <param name="executorName">The logical executor name.</param>
    /// <returns>A panel border color.</returns>
    private static Color GetAgentPanelColor(string executorName)
    {
        return executorName switch
        {
            "LeadershipBrief" => Color.Blue,
            "RiskLens" => Color.Yellow,
            "SignalSpotter" => Color.Green,
            _ => Color.Grey
        };
    }

    /// <summary>
    /// Converts an executor identifier into the display name used in console output.
    /// </summary>
    /// <param name="executorId">The raw executor identifier emitted by the workflow runtime.</param>
    /// <returns>A readable executor name.</returns>
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

    /// <summary>
    /// Formats a timestamp for the workflow activity timeline.
    /// </summary>
    /// <param name="value">The timestamp to format.</param>
    /// <returns>A time string formatted with millisecond precision.</returns>
    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("HH:mm:ss.fff");
    }

    /// <summary>
    /// Formats a workflow duration using seconds for longer intervals and milliseconds for shorter ones.
    /// </summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>A compact duration string suitable for console output.</returns>
    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:0.00}s"
            : $"{duration.TotalMilliseconds:0}ms";
    }

    /// <summary>
    /// Truncates text to the requested maximum length after normalizing whitespace.
    /// </summary>
    /// <param name="text">The text to shorten.</param>
    /// <param name="maxLength">The maximum length of the returned string.</param>
    /// <returns>A normalized string that fits within the requested length.</returns>
    private static string Truncate(string text, int maxLength)
    {
        string normalized = NormalizeWhitespace(text);
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Collapses multi-line text into a single trimmed line by removing empty lines and normalizing whitespace.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    private static string NormalizeWhitespace(string text)
    {
        return string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
