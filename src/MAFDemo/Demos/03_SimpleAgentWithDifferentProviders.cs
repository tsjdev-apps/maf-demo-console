using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace MAFDemo.Demos;

internal class SimpleAgentWithDifferentProviders
{
    private const string DefaultAzurePrompt = "Write a short story about Pforzheim, Germany.";
    private const string DefaultOllamaPrompt = "Write a short story about Munich, Germany.";
    private const string DefaultGitHubModelsPrompt = "Write a short story about Kiel, Germany.";

    /// <summary>
    /// Runs the provider-comparison demo that sends separate prompts to Azure OpenAI, Ollama, and GitHub Models and prints the streamed responses.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        while (true)
        {
            ConsoleHelper.ShowDemoStage(
                "Demo 03 - Different Providers",
                "Compare the same agent pattern across Azure OpenAI, Ollama, and GitHub Models.");
            ConsoleHelper.WriteInfoPanel("Azure OpenAI Example Prompt", DefaultAzurePrompt, Color.Blue);
            ConsoleHelper.WriteInfoPanel("Ollama Example Prompt", DefaultOllamaPrompt, Color.Green);
            ConsoleHelper.WriteInfoPanel("GitHub Models Example Prompt", DefaultGitHubModelsPrompt, Color.DarkOrange);

            string? azurePrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter the Azure OpenAI prompt or press Enter to use the example shown above.[/]",
                true,
                true,
                false);

            if (string.IsNullOrWhiteSpace(azurePrompt))
            {
                azurePrompt = DefaultAzurePrompt;
            }

            string? ollamaPrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter the Ollama prompt or press Enter to use the example shown above.[/]",
                true,
                true,
                false);

            if (string.IsNullOrWhiteSpace(ollamaPrompt))
            {
                ollamaPrompt = DefaultOllamaPrompt;
            }

            string? githubModelsPrompt = ConsoleHelper.GetStringFromConsole(
                "[yellow]Enter the GitHub Models prompt or press Enter to use the example shown above.[/]",
                true,
                true,
                false);

            if (string.IsNullOrWhiteSpace(githubModelsPrompt))
            {
                githubModelsPrompt = DefaultGitHubModelsPrompt;
            }

            ConsoleHelper.ShowDemoStage(
                "Demo 03 - Different Providers",
                "Compare the same agent pattern across Azure OpenAI, Ollama, and GitHub Models.");
            ConsoleHelper.WriteInfoPanel("Azure OpenAI Prompt", azurePrompt, Color.Blue);
            await RunProviderAsync(
                "Azure OpenAI",
                "Azure OpenAI Response",
                azurePrompt,
                Color.Blue,
                static config => ChatClientHelper.CreateAzureOpenAIChatClient(config).AsAIAgent(
                    name: "azure-storyteller",
                    instructions: "You are a concise storytelling assistant. Turn the prompt into a vivid, compact story."));

            ConsoleHelper.WriteInfoPanel("Ollama Prompt", ollamaPrompt, Color.Green);
            await RunProviderAsync(
                "Ollama",
                "Ollama Response",
                ollamaPrompt,
                Color.Green,
                static config => ChatClientHelper.CreateOllamaChatClient(config).AsAIAgent(
                    name: "ollama-storyteller",
                    instructions: "You are a concise storytelling assistant. Turn the prompt into a vivid, compact story."));

            ConsoleHelper.WriteInfoPanel("GitHub Models Prompt", githubModelsPrompt, Color.DarkOrange);
            await RunProviderAsync(
                "GitHub Models",
                "GitHub Models Response",
                githubModelsPrompt,
                Color.DarkOrange,
                static config => ChatClientHelper.CreateGitHubModelsChatClient(config).AsAIAgent(
                    name: "github-models-storyteller",
                    instructions: "You are a concise storytelling assistant. Turn the prompt into a vivid, compact story."));

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

    private static async Task RunProviderAsync(
        string providerName,
        string responsePanelTitle,
        string prompt,
        Color panelColor,
        Func<IConfiguration, AIAgent> createAgent)
    {
        IConfigurationRoot config = ConfigurationHelper.CreateConfiguration();

        try
        {
            AIAgent agent = createAgent(config);
            await ConsoleHelper.StreamResponsePanelAsync(
                responsePanelTitle,
                agent.RunStreamingAsync(prompt),
                panelColor,
                $"Streaming {providerName} response...");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteInfoPanel(
                $"{providerName} Error",
                $"{providerName} could not complete the request. The other providers can still continue.",
                Color.Red);

            if (!ConsoleHelper.TryWriteFriendlyException(ex))
            {
                ConsoleHelper.WriteException(ex);
            }
        }
    }
}
