using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace MAFDemo.Demos;

internal static class SimpleAgent
{
    private const string DefaultTravelRequest = "I want to plan a cruise for me and my parents. We are interested in the Mediterranean Sea, starting in Genoa and want to leave in June. Can you help me with that?";

    /// <summary>
    /// Runs the basic single-agent demo that creates a cruise plan from a user request and renders the answer in a presentation-friendly panel.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient();

        // create an agent from the chat client
        AIAgent writer = chatClient.AsAIAgent(
            name: "cruise-planner",
            instructions: "You are a helpful agent to help with cruise plans.");

        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 01 - Simple Agent",
                "Create a polished cruise proposal from a single travel request.");
            ConsoleHelper.WriteInfoPanel("Example Prompt", DefaultTravelRequest, Color.Grey);

            string? travelRequest = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter a custom cruise request or press Enter to run the example shown above.[/]",
                true,
                true,
                false);

            if (string.IsNullOrWhiteSpace(travelRequest))
            {
                travelRequest = DefaultTravelRequest;
            }

            ConsoleHelper.ShowDemoStage(
                "Demo 01 - Simple Agent",
                "Create a polished cruise proposal from a single travel request.");
            ConsoleHelper.WriteInfoPanel("Selected Prompt", travelRequest, Color.Grey);
            await ConsoleHelper.StreamResponsePanelAsync(
                "Agent Response",
                writer.RunStreamingAsync(travelRequest),
                Color.Red,
                "Generating cruise proposal...");

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
}
