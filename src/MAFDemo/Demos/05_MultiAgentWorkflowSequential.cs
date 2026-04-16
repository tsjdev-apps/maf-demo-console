using System.Text;
using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace MAFDemo.Demos;

internal class MultiAgentWorkflowSequential
{
    private const string DefaultPrompt = "Prepare a delivery brief for adding passkey-based step-up authentication to our B2B admin portal. Cover scope, implementation approach, rollout dependencies, and open risks.";

    /// <summary>
    /// Runs the sequential workflow demo in which four agents process the same request one after another.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient();

        AIAgent scopeAnalyst = chatClient.AsAIAgent(
            name: "ScopeAnalyst",
            instructions: "Clarify the business objective, affected actors, constraints, and missing assumptions in the request.");

        AIAgent solutionDrafter = chatClient.AsAIAgent(
            name: "SolutionDrafter",
            instructions: "Turn the scoped request into a practical delivery brief with implementation approach and milestones.");

        AIAgent architectureReviewer = chatClient.AsAIAgent(
            name: "ArchitectureReviewer",
            instructions: "Strengthen the brief by calling out technical risks, dependencies, and design trade-offs.");

        AIAgent executiveEditor = chatClient.AsAIAgent(
            name: "ExecutiveEditor",
            instructions: "Polish the brief so it is concise, structured, and suitable for engineering leadership.");

        Workflow pipeline = AgentWorkflowBuilder.BuildSequential(
            scopeAnalyst, solutionDrafter, architectureReviewer, executiveEditor);

        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 05 - Multi-Agent Workflow (Sequential)",
                "Pass one delivery request through a fixed sequence of planning specialists.");
            ConsoleHelper.WriteInfoPanel("Example Prompt", DefaultPrompt, Color.Grey);

            string? userPrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter a custom request or press Enter to run the example shown above.[/]",
                true,
                true,
                false);

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                userPrompt = DefaultPrompt;
            }

            const bool showDetails = true;

            ConsoleHelper.ShowDemoStage(
                "Demo 05 - Multi-Agent Workflow (Sequential)",
                "Pass one delivery request through a fixed sequence of planning specialists.");
            ConsoleHelper.WriteInfoPanel("Request", userPrompt, Color.Grey);

            if (showDetails)
            {
                ConsoleHelper.WriteInfoPanel(
                    "Execution Mode",
                    "Detailed workflow activity is enabled by default, so each planning stage and intermediate response will be displayed.",
                    Color.Blue);
            }

            string? finalResponse = null;
            string? lastAgentResponse = null;
            string? currentStreamingExecutorId = null;
            List<ChatMessage>? finalMessages = null;
            List<ChatMessage> initialMessages =
            [
                new(ChatRole.User, userPrompt)
            ];

            await using StreamingRun run = await InProcessExecution.Concurrent.RunStreamingAsync(
                pipeline,
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
                            ConsoleHelper.WriteMarkup("[blue]Workflow started.[/]");
                            Console.WriteLine();
                        }
                        break;

                    case ExecutorInvokedEvent invokedEvent:
                        if (showDetails)
                        {
                            string executorName = FormatExecutorName(invokedEvent.ExecutorId);
                            string? taskSummary = DescribeExecutorTask(executorName, userPrompt, invokedEvent.Data);

                            ConsoleHelper.WriteMarkup($"[bold yellow]{Markup.Escape(executorName)}[/] is now working...");

                            if (!string.IsNullOrWhiteSpace(taskSummary))
                            {
                                Console.WriteLine();
                                ConsoleHelper.WriteMarkup($"[grey]Task:[/] {Markup.Escape(taskSummary)}");
                            }
                        }
                        break;

                    case AgentResponseUpdateEvent updateEvent:
                        if (showDetails && !string.IsNullOrWhiteSpace(updateEvent.Update.Text))
                        {
                            if (currentStreamingExecutorId != updateEvent.ExecutorId)
                            {
                                currentStreamingExecutorId = updateEvent.ExecutorId;
                                Console.WriteLine();
                                ConsoleHelper.WriteMarkup($"[grey]{Markup.Escape(FormatExecutorName(updateEvent.ExecutorId))} output:[/]");
                                Console.WriteLine();
                            }

                            Console.Write(updateEvent.Update.Text);
                        }
                        break;

                    case AgentResponseEvent responseEvent:
                        string? responseText = ExtractText(responseEvent.Response);
                        if (!string.IsNullOrWhiteSpace(responseText))
                        {
                            lastAgentResponse = responseText;
                        }

                        if (showDetails && !string.IsNullOrWhiteSpace(responseText))
                        {
                            Console.WriteLine();
                            ConsoleHelper.WriteMarkup($"[green]{Markup.Escape(FormatExecutorName(responseEvent.ExecutorId))} completed.[/]");
                            Console.WriteLine();
                        }

                        currentStreamingExecutorId = null;
                        break;

                    case ExecutorCompletedEvent completedEvent:
                        if (showDetails)
                        {
                            ConsoleHelper.WriteMarkup($"[green]{Markup.Escape(FormatExecutorName(completedEvent.ExecutorId))} finished.[/]");
                            Console.WriteLine();
                        }
                        break;

                    case WorkflowOutputEvent outputEvent:
                        finalMessages = outputEvent.As<List<ChatMessage>>();
                        finalResponse = ExtractText(outputEvent.Data) ?? DescribeEventPayload(outputEvent.Data);
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

    /// <summary>
    /// Extracts human-readable text from a workflow payload that may represent a response, update, or arbitrary object.
    /// </summary>
    /// <param name="value">The workflow payload to inspect.</param>
    /// <returns>The extracted text when available; otherwise, <see langword="null"/>.</returns>
    private static string? ExtractText(object? value)
    {
        return value switch
        {
            AgentResponse response => response.Text,
            AgentResponseUpdate update => update.Text,
            _ => value?.ToString()
        };
    }

    /// <summary>
    /// Produces a normalized textual summary for a workflow payload so it can be shown in the console.
    /// </summary>
    /// <param name="value">The workflow payload to summarize.</param>
    /// <returns>A normalized text representation, or <see langword="null"/> when no meaningful text is available.</returns>
    private static string? DescribeEventPayload(object? value)
    {
        return value switch
        {
            TurnToken => null,
            string text => NormalizeWhitespace(text),
            ChatMessage message => NormalizeWhitespace(message.Text),
            IEnumerable<ChatMessage> messages => NormalizeWhitespace(messages.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Text))?.Text),
            AgentResponse response => NormalizeWhitespace(response.Text),
            AgentResponseUpdate update => NormalizeWhitespace(update.Text),
            _ => NormalizeWhitespace(value?.ToString())
        };
    }

    /// <summary>
    /// Builds a readable description of the work currently assigned to a sequential workflow executor.
    /// </summary>
    /// <param name="executorName">The logical executor name.</param>
    /// <param name="userPrompt">The original user request that started the workflow.</param>
    /// <param name="payload">The workflow payload associated with the executor invocation.</param>
    /// <returns>A task description for display purposes, or <see langword="null"/> when no description can be derived.</returns>
    private static string? DescribeExecutorTask(string executorName, string userPrompt, object? payload)
    {
        string? payloadSummary = DescribeEventPayload(payload);
        if (!string.IsNullOrWhiteSpace(payloadSummary))
        {
            return payloadSummary;
        }

        return executorName switch
        {
            "ScopeAnalyst" => $"Clarifies the request scope and constraints: {NormalizeWhitespace(userPrompt)}",
            "SolutionDrafter" => "Turns the scoped request into an actionable delivery brief.",
            "ArchitectureReviewer" => "Adds implementation risks, dependencies, and trade-offs.",
            "ExecutiveEditor" => "Condenses the brief into a leadership-ready final version.",
            _ => null
        };
    }

    /// <summary>
    /// Returns the most recent assistant-authored message from a conversation transcript.
    /// </summary>
    /// <param name="messages">The ordered message sequence to inspect.</param>
    /// <returns>The latest non-empty assistant message text, or <see langword="null"/> when none exists.</returns>
    private static string? ExtractLatestAssistantText(IEnumerable<ChatMessage> messages)
    {
        return messages
            .Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => message.Text)
            .LastOrDefault();
    }

    /// <summary>
    /// Formats a message sequence as a readable transcript with author labels.
    /// </summary>
    /// <param name="messages">The ordered message sequence to format.</param>
    /// <returns>A multi-line conversation transcript.</returns>
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

    /// <summary>
    /// Collapses multi-line text into a single trimmed line by removing empty lines and normalizing whitespace.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text, or <see langword="null"/> when the input is empty or whitespace-only.</returns>
    private static string? NormalizeWhitespace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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
}
