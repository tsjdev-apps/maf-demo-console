using Microsoft.Extensions.Configuration;

namespace MAFDemo.Helpers;

/// <summary>
/// Centralizes demo metadata, startup explanations, and configuration readiness checks.
/// </summary>
internal static class DemoCatalog
{
    private static readonly DemoDefinition[] s_demos =
    [
        new(
            "01 - Simple Agent",
            "Turns one user request into one polished answer with a single agent.",
            ConfigurationHelper.GetMissingAzureOpenAISettings),
        new(
            "02 - Simple Agent with Conversation Thread",
            "Shows how an agent remembers project context across multiple turns in the same session.",
            ConfigurationHelper.GetMissingAzureOpenAISettings),
        new(
            "03 - Simple Agent with Different Providers",
            "Runs the same agent pattern against Azure OpenAI, GitHub Models, and Ollama.",
            static config => ConfigurationHelper.CombineMissingSettings(
                ConfigurationHelper.GetMissingAzureOpenAISettings(config),
                ConfigurationHelper.GetMissingGitHubModelsSettings(config),
                ConfigurationHelper.GetMissingOllamaSettings(config))),
        new(
            "04 - Agents with Tools",
            "Combines an agent with live weather and local-time tools for grounded answers.",
            static config => ConfigurationHelper.CombineMissingSettings(
                ConfigurationHelper.GetMissingAzureOpenAISettings(config),
                ConfigurationHelper.GetMissingOpenWeatherMapSettings(config))),
        new(
            "05 - Multi-Agent Workflow (Sequential)",
            "Routes one delivery brief through a fixed sequence of planning specialists.",
            ConfigurationHelper.GetMissingAzureOpenAISettings),
        new(
            "06 - Multi-Agent Workflow (Concurrent)",
            "Lets multiple specialists analyze the same rollout update in parallel.",
            ConfigurationHelper.GetMissingAzureOpenAISettings),
        new(
            "07 - Multi-Agent Workflow (Handoff)",
            "Hands an identity rollout request to the specialist that best fits the current need.",
            ConfigurationHelper.GetMissingAzureOpenAISettings),
        new(
            "08 - Multi-Agent Workflow (Group Chat)",
            "Simulates an authentication incident bridge in which multiple agents discuss the same problem.",
            ConfigurationHelper.GetMissingAzureOpenAISettings),
        new(
            "09 - Multi-Agent Workflow (Magentic (Planner-led))",
            "Uses a delivery lead to plan, delegate work, and synthesize the final answer.",
            ConfigurationHelper.GetMissingAzureOpenAISettings)
    ];

    /// <summary>
    /// Returns the ordered demo definitions displayed in the application menu.
    /// </summary>
    public static IReadOnlyList<DemoDefinition> All => s_demos;

    /// <summary>
    /// Resolves a demo definition by its menu label.
    /// </summary>
    /// <param name="menuLabel">The label shown in the selection menu.</param>
    /// <returns>The matching definition, or <see langword="null"/> when none exists.</returns>
    public static DemoDefinition? Find(string menuLabel)
    {
        return s_demos.FirstOrDefault(demo => string.Equals(demo.MenuLabel, menuLabel, StringComparison.Ordinal));
    }

    /// <summary>
    /// Builds the short overview shown at the top of the application so each demo explains its purpose before selection.
    /// </summary>
    /// <returns>A multi-line summary of all demos.</returns>
    public static string BuildOverviewText()
    {
        return string.Join(
            Environment.NewLine,
            s_demos.Select(demo => $"{demo.MenuLabel}: {demo.ShortDescription}"));
    }

    /// <summary>
    /// Builds a readable startup summary of configuration readiness across all external dependencies.
    /// </summary>
    /// <param name="config">The configuration source to inspect.</param>
    /// <returns>A multi-line readiness summary.</returns>
    public static string BuildConfigurationSummary(IConfiguration config)
    {
        return string.Join(
            Environment.NewLine,
            [
                FormatReadinessLine("Azure OpenAI", ConfigurationHelper.GetMissingAzureOpenAISettings(config)),
                FormatReadinessLine("GitHub Models", ConfigurationHelper.GetMissingGitHubModelsSettings(config)),
                FormatReadinessLine("Ollama", ConfigurationHelper.GetMissingOllamaSettings(config)),
                FormatReadinessLine("OpenWeatherMap", ConfigurationHelper.GetMissingOpenWeatherMapSettings(config))
            ]);
    }

    /// <summary>
    /// Formats a demo-specific missing-settings message for user-facing output.
    /// </summary>
    /// <param name="demo">The demo that was selected.</param>
    /// <param name="missingSettings">The missing configuration entries.</param>
    /// <returns>A detailed message describing what needs to be configured.</returns>
    public static string BuildMissingRequirementsText(DemoDefinition demo, IReadOnlyList<string> missingSettings)
    {
        return $"The selected demo is not ready yet: {demo.ShortDescription}{Environment.NewLine}{Environment.NewLine}" +
               "Missing settings:" + Environment.NewLine +
               string.Join(Environment.NewLine, missingSettings.Select(setting => $"- {setting}"));
    }

    private static string FormatReadinessLine(string dependencyName, IReadOnlyList<string> missingSettings)
    {
        return missingSettings.Count == 0
            ? $"{dependencyName}: Ready"
            : $"{dependencyName}: Missing {string.Join(", ", missingSettings)}";
    }
}

/// <summary>
/// Describes one selectable demo entry together with its readiness rule.
/// </summary>
/// <param name="MenuLabel">The label shown in the demo menu.</param>
/// <param name="ShortDescription">A one-line explanation of the demo's purpose.</param>
/// <param name="GetMissingRequirements">A function that returns the configuration entries still required for the demo.</param>
internal sealed record DemoDefinition(
    string MenuLabel,
    string ShortDescription,
    Func<IConfiguration, IReadOnlyList<string>> GetMissingRequirements);
