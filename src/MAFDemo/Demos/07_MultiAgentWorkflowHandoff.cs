using System.Text;
using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace MAFDemo.Demos;

internal static class MultiAgentWorkflowHandoff
{
    private const string DefaultPrompt = """
        Prepare a rollout recommendation for a new self-service second-factor recovery journey for enterprise tenants.
        We want to reduce helpdesk load, keep the recovery flow strongly audited, support pilot customers in Germany and the Netherlands,
        and enable a phased rollout next quarter without disrupting the existing administrator-driven recovery path.
        Include guidance on security controls, support readiness, rollout sequencing, and key risks.
        """;

    private static readonly Dictionary<(string From, string To), string> s_handoffReasons = new()
    {
        [("IdentityLead", "SecurityLead")] = "The next decision depends on stronger guidance around abuse resistance, authorization boundaries, and auditability.",
        [("IdentityLead", "SupportLead")] = "The request now needs support readiness, administrator enablement, and user communication planning.",
        [("IdentityLead", "RolloutLead")] = "The remaining questions are about rollout sequencing, feature flags, and operational safety.",
        [("SecurityLead", "SupportLead")] = "The security guardrails are clear, so the recovery experience and support process can now be shaped around them.",
        [("SecurityLead", "RolloutLead")] = "Security requirements are defined, and the next step is validating rollout feasibility and operational controls.",
        [("SecurityLead", "IdentityLead")] = "The security position is ready to be folded back into the overall recommendation.",
        [("SupportLead", "RolloutLead")] = "Support and communication needs are clear, so the focus shifts to rollout execution and tenant sequencing.",
        [("SupportLead", "IdentityLead")] = "Support readiness is covered and ready for final consolidation.",
        [("RolloutLead", "IdentityLead")] = "Rollout sequencing and safeguards are defined, so the final recommendation can be assembled."
    };

    private static readonly Dictionary<string, string> s_agentColors = new()
    {
        ["IdentityLead"] = "deepskyblue1",
        ["SecurityLead"] = "gold1",
        ["SupportLead"] = "springgreen3",
        ["RolloutLead"] = "grey70"
    };

    /// <summary>
    /// Runs the handoff workflow demo in which agents pass ownership of the request to one another based on specialization.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient();

        AIAgent identityLead = chatClient.AsAIAgent(
            name: "IdentityLead",
            instructions: """
                You are the lead coordinator for identity feature rollout planning.
                You decide which specialist should handle each part of the request and you present the final consolidated answer.
                Hand over to security when abuse resistance, authorization, auditability, or compliance controls dominate.
                Hand over to support when admin enablement, communications, training, or service readiness need deeper work.
                Hand over to rollout when sequencing, feature flags, monitoring, dependencies, or operational risk become central.
                When the specialists have completed their work, consolidate the recommendation into a clear final plan.
                """);

        AIAgent securityLead = chatClient.AsAIAgent(
            name: "SecurityLead",
            instructions: """
                You own the security and control perspective.
                Focus on authorization boundaries, approval safeguards, auditability, abuse prevention, and tenant risk differentiation.
                When your part is complete, hand over to the best next specialist or return control to IdentityLead.
                """);

        AIAgent supportLead = chatClient.AsAIAgent(
            name: "SupportLead",
            instructions: """
                You own support readiness and administrator experience.
                Focus on helpdesk workflows, operator guidance, customer communication, training, and user-facing recovery clarity.
                When your part is complete, hand over to the best next specialist or return control to IdentityLead.
                """);

        AIAgent rolloutLead = chatClient.AsAIAgent(
            name: "RolloutLead",
            instructions: """
                You own rollout execution.
                Focus on sequencing, feature flags, tenant onboarding, monitoring, rollback, dependencies, and delivery feasibility.
                When your part is complete, return control to IdentityLead for final consolidation.
                """);

#pragma warning disable MAAIW001
        Workflow handoffWorkflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(identityLead)
            .WithHandoff(identityLead, securityLead, "Hand off when authorization, abuse prevention, auditability, or compliance controls dominate.")
            .WithHandoff(identityLead, supportLead, "Hand off when operator readiness, communications, or support workflow design need specialist input.")
            .WithHandoff(identityLead, rolloutLead, "Hand off when sequencing, feature flags, rollout dependencies, or operational risk dominate.")
            .WithHandoff(securityLead, supportLead, "Hand off after the control model is clear and the support experience needs to fit within it.")
            .WithHandoff(securityLead, rolloutLead, "Hand off after security requirements are set and rollout feasibility must be validated.")
            .WithHandoff(securityLead, identityLead, "Hand off when the security recommendation is complete and ready for consolidation.")
            .WithHandoff(supportLead, rolloutLead, "Hand off after support readiness is defined and rollout sequencing must be finalized.")
            .WithHandoff(supportLead, identityLead, "Hand off when support guidance is complete and ready for consolidation.")
            .WithHandoff(rolloutLead, identityLead, "Hand off when rollout planning is complete and the final response can be assembled.")
            .EmitAgentResponseEvents(true)
            .Build();
#pragma warning restore MAAIW001

        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 07 - Multi-Agent Workflow (Handoff)",
                "Route an identity rollout request between security, support, and rollout specialists before consolidating the result.");
            ConsoleHelper.WriteInfoPanel("Example Prompt", DefaultPrompt, Color.Grey);

            string? userPrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter a custom rollout request or press Enter to run the example shown above.[/]",
                true,
                false,
                false);

            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                userPrompt = DefaultPrompt;
            }

            const bool showDetails = true;

            ConsoleHelper.ShowDemoStage(
                "Demo 07 - Multi-Agent Workflow (Handoff)",
                "Route an identity rollout request between security, support, and rollout specialists before consolidating the result.");
            ConsoleHelper.WriteInfoPanel("Request", userPrompt, Color.Grey);

            if (showDetails)
            {
                ConsoleHelper.WriteInfoPanel(
                    "Execution Mode",
                    "Detailed handoffs and specialist contributions are enabled by default.",
                    Color.Blue);
            }

            string? finalResponse = null;
            string? lastAgentResponse = null;
            List<ChatMessage>? finalMessages = null;
            string? previousAgent = null;
            List<string> batonTrail = [];

            List<ChatMessage> initialMessages =
            [
                new(ChatRole.User, userPrompt)
            ];

            await using StreamingRun run = await InProcessExecution.Concurrent.RunStreamingAsync(
                handoffWorkflow,
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
                            ConsoleHelper.WriteMarkup("[blue]Handoff workflow started.[/]");
                            Console.WriteLine();
                        }
                        break;

                    case ExecutorInvokedEvent invokedEvent:
                        if (showDetails)
                        {
                            string agentName = FormatExecutorName(invokedEvent.ExecutorId);
                            batonTrail.Add(agentName);

                            if (previousAgent is null)
                            {
                                ConsoleHelper.WriteMarkup(
                                    $"[{GetAgentColor(agentName)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(agentName)}[/] takes initial ownership of the request.");
                                ConsoleHelper.WriteMarkup(
                                    $"[grey]Role:[/] {Markup.Escape(DescribeAgentRole(agentName))}");
                            }
                            else if (!string.Equals(previousAgent, agentName, StringComparison.Ordinal))
                            {
                                ConsoleHelper.WriteMarkup(
                                    $"[{GetAgentColor(agentName)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(previousAgent)}[/] hands over to [bold]{Markup.Escape(agentName)}[/].");
                                ConsoleHelper.WriteMarkup(
                                    $"[grey]Handoff reason:[/] {Markup.Escape(GetHandoffReason(previousAgent, agentName))}");
                            }
                            else
                            {
                                ConsoleHelper.WriteMarkup(
                                    $"[{GetAgentColor(agentName)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(agentName)}[/] continues with the request.");
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

                        if (showDetails && !string.IsNullOrWhiteSpace(responseText))
                        {
                            ConsoleHelper.WriteMarkup(
                                $"[grey]{Markup.Escape(agentName)} contribution:[/] {Markup.Escape(Truncate(responseText, 180))}");
                            Console.WriteLine();
                        }

                        break;
                    }

                    case ExecutorCompletedEvent completedEvent:
                        if (showDetails)
                        {
                            string agentName = FormatExecutorName(completedEvent.ExecutorId);
                            ConsoleHelper.WriteMarkup(
                                $"[{GetAgentColor(agentName)}]{FormatTimestamp(DateTimeOffset.Now)}[/] [bold]{Markup.Escape(agentName)}[/] completed its current step.");
                            Console.WriteLine();
                        }

                        previousAgent = FormatExecutorName(completedEvent.ExecutorId);
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

            if (showDetails && batonTrail.Count > 0)
            {
                ConsoleHelper.WriteInfoPanel("Handoff Sequence", FormatBatonTrail(batonTrail), Color.Blue);
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

    /// <summary>
    /// Returns the narrative explanation shown when the workflow hands the request from one agent to another.
    /// </summary>
    /// <param name="from">The agent handing off the request.</param>
    /// <param name="to">The agent receiving the request.</param>
    /// <returns>A user-facing explanation of the handoff.</returns>
    private static string GetHandoffReason(string from, string to)
    {
        return s_handoffReasons.TryGetValue((from, to), out string? reason)
            ? reason
            : $"{to} is the better specialist for what just appeared in the request.";
    }

    /// <summary>
    /// Returns the role description shown for an agent when it first takes control of the handoff workflow.
    /// </summary>
    /// <param name="agentName">The agent name to describe.</param>
    /// <returns>A console-friendly description of the agent's responsibility.</returns>
    private static string DescribeAgentRole(string agentName)
    {
        return agentName switch
        {
            "IdentityLead" => "Coordinates the workflow, routes work to specialists, and consolidates the final rollout recommendation.",
            "SecurityLead" => "Owns authorization boundaries, abuse resistance, auditability, and control design.",
            "SupportLead" => "Owns helpdesk readiness, communication, operator guidance, and recovery experience clarity.",
            "RolloutLead" => "Owns sequencing, feature flags, tenant onboarding, monitoring, and operational risk.",
            _ => "Handles the next part of the workflow."
        };
    }

    /// <summary>
    /// Resolves the display color used for a specific handoff workflow agent.
    /// </summary>
    /// <param name="agentName">The agent name to look up.</param>
    /// <returns>A Spectre.Console color name.</returns>
    private static string GetAgentColor(string agentName)
    {
        return s_agentColors.TryGetValue(agentName, out string? color)
            ? color
            : "white";
    }

    /// <summary>
    /// Compacts the baton history into a readable handoff trail without consecutive duplicates.
    /// </summary>
    /// <param name="batonTrail">The ordered sequence of agents that touched the request.</param>
    /// <returns>A formatted baton trail string.</returns>
    private static string FormatBatonTrail(IEnumerable<string> batonTrail)
    {
        List<string> simplifiedTrail = [];

        foreach (string agent in batonTrail)
        {
            if (simplifiedTrail.Count == 0 || !string.Equals(simplifiedTrail[^1], agent, StringComparison.Ordinal))
            {
                simplifiedTrail.Add(agent);
            }
        }

        return string.Join(" -> ", simplifiedTrail);
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
    /// Truncates text to the requested maximum length after normalizing whitespace.
    /// </summary>
    /// <param name="text">The text to shorten.</param>
    /// <param name="maxLength">The maximum length of the returned string.</param>
    /// <returns>A normalized string that fits within the requested length.</returns>
    private static string Truncate(string text, int maxLength)
    {
        string normalized = NormalizeWhitespace(text) ?? string.Empty;
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..(maxLength - 3)] + "...";
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

    /// <summary>
    /// Formats a timestamp for handoff activity output.
    /// </summary>
    /// <param name="value">The timestamp to format.</param>
    /// <returns>A time string formatted with millisecond precision.</returns>
    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("HH:mm:ss.fff");
    }
}
