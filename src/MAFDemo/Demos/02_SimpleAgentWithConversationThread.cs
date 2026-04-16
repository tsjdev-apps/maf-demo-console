using System.Text;
using System.Text.Json;
using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace MAFDemo.Demos;

internal class SimpleAgentWithConversationThread
{
    private const string DefaultContextSeed = "I am preparing the Atlas MAF rollout for Fabrikam, and I prefer concise technical summaries.";
    private const string DefaultFollowUpQuestion = "Which rollout am I preparing, for which customer, and what communication style do I prefer?";

    private sealed record SessionSnapshot(
        string Transcript,
        string SerializedState);

    /// <summary>
    /// Runs the conversation-thread demo that preserves context across multiple user messages within the same agent session.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient();

        // create an agent from the chat client
        AIAgent assistantAgent = chatClient.AsAIAgent(
            name: "project-memory-assistant",
            instructions: "You are a helpful project copilot. Remember facts established earlier in the session and use them in later answers.");

        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 02 - Conversation Thread",
                "Keep project context across multiple turns inside one shared agent session.");
            ConsoleHelper.WriteInfoPanel("Example Context", DefaultContextSeed, Color.Grey);

            string userContext = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter a short project context sentence or press Enter to use the example shown above.[/]",
                true,
                true,
                false)
                ?? DefaultContextSeed;

            if (string.IsNullOrWhiteSpace(userContext))
            {
                userContext = DefaultContextSeed;
            }

            AgentSession session = await assistantAgent.CreateSessionAsync();
            SessionSnapshot sessionSnapshot = await CaptureSessionSnapshotAsync(assistantAgent, session);

            ConsoleHelper.ShowDemoStage(
                "Demo 02 - Conversation Thread",
                "Keep project context across multiple turns inside one shared agent session.");
            ConsoleHelper.WriteInfoPanel("Seed Prompt", userContext, Color.Grey);
            ConsoleHelper.WriteInfoPanel("Serialized Session State", sessionSnapshot.SerializedState, Color.Yellow);

            string introductionResponse = await ConsoleHelper.StreamResponsePanelAsync(
                "Session Setup Response",
                assistantAgent.RunStreamingAsync(
                    userContext,
                    session),
                Color.Blue,
                "Storing project context...");

            sessionSnapshot = await CaptureSessionSnapshotAsync(assistantAgent, session);

            while (true)
            {
                ConsoleHelper.ShowDemoStage(
                    "Demo 02 - Conversation Thread",
                    "The agent remembers what was established earlier in the session.");
                ConsoleHelper.WriteInfoPanel("Last Agent Response", introductionResponse, Color.Blue);
                ConsoleHelper.WriteInfoPanel("Session Transcript", sessionSnapshot.Transcript, Color.Green);
                ConsoleHelper.WriteInfoPanel("Serialized Session State", sessionSnapshot.SerializedState, Color.Yellow);
                ConsoleHelper.WriteInfoPanel("Example Follow-up", DefaultFollowUpQuestion, Color.Grey);

                string? userMessage = ConsoleHelper.GetStringFromConsole(
                    "[yellow]Enter your next message or press Enter to use the example shown above.[/]",
                    true,
                    true,
                    false);

                if (string.IsNullOrWhiteSpace(userMessage))
                {
                    userMessage = DefaultFollowUpQuestion;
                }

                ConsoleHelper.ShowDemoStage(
                    "Demo 02 - Conversation Thread",
                    "The agent remembers what was established earlier in the session.");
                ConsoleHelper.WriteInfoPanel("Last Agent Response", introductionResponse, Color.Blue);
                ConsoleHelper.WriteInfoPanel("Session Transcript", sessionSnapshot.Transcript, Color.Green);
                ConsoleHelper.WriteInfoPanel("Your Message", userMessage, Color.Grey);

                introductionResponse = await ConsoleHelper.StreamResponsePanelAsync(
                    "Agent Response",
                    assistantAgent.RunStreamingAsync(userMessage, session),
                    Color.Red,
                    "Generating context-aware response...");

                sessionSnapshot = await CaptureSessionSnapshotAsync(assistantAgent, session);

                ConsoleHelper.ShowDemoStage(
                    "Demo 02 - Conversation Thread",
                    "This is the updated session state after the latest call.");
                ConsoleHelper.WriteInfoPanel("Last Agent Response", introductionResponse, Color.Blue);
                ConsoleHelper.WriteInfoPanel("Session Transcript", sessionSnapshot.Transcript, Color.Green);
                ConsoleHelper.WriteInfoPanel("Serialized Session State", sessionSnapshot.SerializedState, Color.Yellow);

                string action = ConsoleHelper.SelectFromOptions(
                [
                    "Ask another question",
                    "Restart demo",
                    "Back to demo selection"
                ],
                "[yellow]What would you like to do next?[/]",
                false);

                if (action == "Ask another question")
                {
                    continue;
                }

                if (action == "Restart demo")
                {
                    break;
                }

                return;
            }
        }
    }

    private static async Task<SessionSnapshot> CaptureSessionSnapshotAsync(
        AIAgent assistantAgent,
        AgentSession session)
    {
        JsonElement serializedSession = await assistantAgent.SerializeSessionAsync(session);
        string serializedState = JsonSerializer.Serialize(
            serializedSession,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        string transcript = session.TryGetInMemoryChatHistory(out List<ChatMessage>? messages) &&
            messages is not null &&
            messages.Count > 0
            ? FormatConversation(messages)
            : "No readable in-memory chat history is currently exposed for this session. The serialized JSON below still shows the live session state.";

        return new SessionSnapshot(transcript, serializedState);
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

        return builder.Length > 0
            ? builder.ToString()
            : "The session currently contains no text messages.";
    }
}
