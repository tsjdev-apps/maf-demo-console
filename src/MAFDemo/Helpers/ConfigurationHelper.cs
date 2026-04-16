using Microsoft.Extensions.Configuration;

namespace MAFDemo.Helpers;

/// <summary>
/// Loads demo configuration and validates required settings.
/// </summary>
internal static class ConfigurationHelper
{
    private static readonly string[] s_azureOpenAISettings =
    [
        "AzureOpenAI:Endpoint",
        "AzureOpenAI:ApiKey",
        "AzureOpenAI:Deployment"
    ];

    private static readonly string[] s_ollamaSettings =
    [
        "Ollama:Endpoint",
        "Ollama:Deployment"
    ];

    /// <summary>
    /// Builds the demo configuration from the user-secrets store associated with the application.
    /// </summary>
    /// <returns>An <see cref="IConfigurationRoot"/> containing the loaded demo settings.</returns>
    public static IConfigurationRoot CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();
    }

    /// <summary>
    /// Retrieves a required configuration value and throws when the setting is missing or empty.
    /// </summary>
    /// <param name="config">The configuration source to read from.</param>
    /// <param name="key">The configuration key to resolve.</param>
    /// <returns>The non-empty configuration value for the specified key.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified configuration value is missing, empty, or whitespace-only.
    /// </exception>
    public static string GetRequiredSetting(IConfiguration config, string key)
    {
        string? value = config[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"User Secret '{key}' is missing.");
        }

        return value;
    }

    /// <summary>
    /// Retrieves an optional configuration value and normalizes empty or whitespace-only values to <see langword="null"/>.
    /// </summary>
    /// <param name="config">The configuration source to read from.</param>
    /// <param name="key">The configuration key to resolve.</param>
    /// <returns>
    /// The normalized configuration value, or <see langword="null"/> when the setting is missing, empty, or whitespace-only.
    /// </returns>
    public static string? GetOptionalSetting(IConfiguration config, string key)
    {
        string? value = config[key];

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    /// <summary>
    /// Returns the Azure OpenAI settings that are currently missing.
    /// </summary>
    /// <param name="config">The configuration source to inspect.</param>
    /// <returns>The missing Azure OpenAI settings.</returns>
    public static IReadOnlyList<string> GetMissingAzureOpenAISettings(IConfiguration config)
    {
        return GetMissingSettings(config, s_azureOpenAISettings);
    }

    /// <summary>
    /// Returns the GitHub Models settings that are currently missing.
    /// </summary>
    /// <param name="config">The configuration source to inspect.</param>
    /// <returns>The missing GitHub Models settings.</returns>
    public static IReadOnlyList<string> GetMissingGitHubModelsSettings(IConfiguration config)
    {
        List<string> missingSettings = [];

        if (!HasGitHubModelsToken(config))
        {
            missingSettings.Add("GitHubModels:Token or GITHUB_TOKEN");
        }

        if (string.IsNullOrWhiteSpace(GetOptionalSetting(config, "GitHubModels:Model")) &&
            string.IsNullOrWhiteSpace(GetOptionalSetting(config, "GitHubModels:Deployment")))
        {
            missingSettings.Add("GitHubModels:Model or GitHubModels:Deployment");
        }

        return missingSettings;
    }

    /// <summary>
    /// Returns the Ollama settings that are currently missing.
    /// </summary>
    /// <param name="config">The configuration source to inspect.</param>
    /// <returns>The missing Ollama settings.</returns>
    public static IReadOnlyList<string> GetMissingOllamaSettings(IConfiguration config)
    {
        return GetMissingSettings(config, s_ollamaSettings);
    }

    /// <summary>
    /// Returns the OpenWeatherMap settings that are currently missing.
    /// </summary>
    /// <param name="config">The configuration source to inspect.</param>
    /// <returns>The missing OpenWeatherMap settings.</returns>
    public static IReadOnlyList<string> GetMissingOpenWeatherMapSettings(IConfiguration config)
    {
        return GetMissingSettings(config, ["OpenWeatherMapApiKey"]);
    }

    /// <summary>
    /// Merges multiple missing-settings lists into one distinct list.
    /// </summary>
    /// <param name="missingSettingsSets">The missing-setting collections to merge.</param>
    /// <returns>A distinct list preserving first-seen order.</returns>
    public static IReadOnlyList<string> CombineMissingSettings(params IReadOnlyList<string>[] missingSettingsSets)
    {
        List<string> combined = [];

        foreach (IReadOnlyList<string> missingSettings in missingSettingsSets)
        {
            foreach (string setting in missingSettings)
            {
                if (!combined.Contains(setting, StringComparer.Ordinal))
                {
                    combined.Add(setting);
                }
            }
        }

        return combined;
    }

    private static IReadOnlyList<string> GetMissingSettings(IConfiguration config, params string[] keys)
    {
        List<string> missingSettings = [];

        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(GetOptionalSetting(config, key)))
            {
                missingSettings.Add(key);
            }
        }

        return missingSettings;
    }

    private static bool HasGitHubModelsToken(IConfiguration config)
    {
        return !string.IsNullOrWhiteSpace(GetOptionalSetting(config, "GitHubModels:Token")) ||
               !string.IsNullOrWhiteSpace(GetOptionalSetting(config, "GitHubModels:ApiKey")) ||
               !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
    }
}
