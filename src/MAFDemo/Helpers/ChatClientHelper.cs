using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using System.ClientModel;

namespace MAFDemo.Helpers;

/// <summary>
/// Creates chat clients for the demo providers.
/// </summary>
internal static class ChatClientHelper
{
    private const string DefaultGitHubModelsEndpoint = "https://models.github.ai/inference";

    /// <summary>
    /// Creates an <see cref="IChatClient"/> backed by Azure OpenAI using the configured endpoint, API key, and deployment name.
    /// </summary>
    /// <param name="config">
    /// An optional configuration source. When <see langword="null"/>, the demo user-secrets configuration is loaded automatically.
    /// </param>
    /// <returns>An Azure OpenAI chat client wrapped as <see cref="IChatClient"/>.</returns>
    public static IChatClient CreateAzureOpenAIChatClient(IConfiguration? config = null)
    {
        config ??= ConfigurationHelper.CreateConfiguration();

        string endpoint = ConfigurationHelper.GetRequiredSetting(config, "AzureOpenAI:Endpoint");
        string apiKey = ConfigurationHelper.GetRequiredSetting(config, "AzureOpenAI:ApiKey");
        string deploymentName = ConfigurationHelper.GetRequiredSetting(config, "AzureOpenAI:Deployment");

        return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
            .GetChatClient(deploymentName)
            .AsIChatClient();
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> backed by an Ollama endpoint using the configured endpoint and model name.
    /// </summary>
    /// <param name="config">
    /// An optional configuration source. When <see langword="null"/>, the demo user-secrets configuration is loaded automatically.
    /// </param>
    /// <returns>An Ollama chat client exposed as <see cref="IChatClient"/>.</returns>
    public static IChatClient CreateOllamaChatClient(IConfiguration? config = null)
    {
        config ??= ConfigurationHelper.CreateConfiguration();

        string endpoint = ConfigurationHelper.GetRequiredSetting(config, "Ollama:Endpoint");
        string deploymentName = ConfigurationHelper.GetRequiredSetting(config, "Ollama:Deployment");

        return new OllamaApiClient(new Uri(endpoint), deploymentName);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> backed by GitHub Models using the configured inference endpoint, GitHub token, and model identifier.
    /// </summary>
    /// <param name="config">
    /// An optional configuration source. When <see langword="null"/>, the demo user-secrets configuration is loaded automatically.
    /// </param>
    /// <returns>A GitHub Models chat client wrapped as <see cref="IChatClient"/>.</returns>
    public static IChatClient CreateGitHubModelsChatClient(IConfiguration? config = null)
    {
        config ??= ConfigurationHelper.CreateConfiguration();

        string endpoint = GetGitHubModelsEndpoint(config);
        string token = GetGitHubModelsToken(config);
        string modelId = GetGitHubModelsModelId(config);
        ChatCompletionsClient chatCompletionsClient = new ChatCompletionsClient(
            new Uri(endpoint),
            new AzureKeyCredential(token));

        return chatCompletionsClient.AsIChatClient(modelId);
    }

    /// <summary>
    /// Resolves the GitHub Models inference endpoint from configuration or constructs the default endpoint, including the optional organization-scoped variant.
    /// </summary>
    /// <param name="config">The configuration source to read from.</param>
    /// <returns>The fully qualified GitHub Models inference endpoint URL.</returns>
    private static string GetGitHubModelsEndpoint(IConfiguration config)
    {
        string? configuredEndpoint = ConfigurationHelper.GetOptionalSetting(config, "GitHubModels:Endpoint");
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return configuredEndpoint;
        }

        string? organization = ConfigurationHelper.GetOptionalSetting(config, "GitHubModels:Organization");

        return string.IsNullOrWhiteSpace(organization)
            ? DefaultGitHubModelsEndpoint
            : $"https://models.github.ai/orgs/{organization}/inference";
    }

    /// <summary>
    /// Resolves the GitHub Models token from user secrets or the standard <c>GITHUB_TOKEN</c> environment variable.
    /// </summary>
    /// <param name="config">The configuration source to read from.</param>
    /// <returns>The GitHub token used to authenticate the inference request.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither a configured token nor the <c>GITHUB_TOKEN</c> environment variable is available.
    /// </exception>
    private static string GetGitHubModelsToken(IConfiguration config)
    {
        string? token = ConfigurationHelper.GetOptionalSetting(config, "GitHubModels:Token")
            ?? ConfigurationHelper.GetOptionalSetting(config, "GitHubModels:ApiKey")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "GitHub Models credentials are missing. Configure 'GitHubModels:Token' or set the 'GITHUB_TOKEN' environment variable.");
        }

        return token;
    }

    /// <summary>
    /// Resolves the GitHub Models identifier from configuration, preferring the dedicated model key and falling back to the legacy deployment key.
    /// </summary>
    /// <param name="config">The configuration source to read from.</param>
    /// <returns>The model identifier used for GitHub Models inference requests.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no GitHub Models identifier is configured.
    /// </exception>
    private static string GetGitHubModelsModelId(IConfiguration config)
    {
        string? modelId = ConfigurationHelper.GetOptionalSetting(config, "GitHubModels:Model")
            ?? ConfigurationHelper.GetOptionalSetting(config, "GitHubModels:Deployment");

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException(
                "GitHub Models is missing the model identifier. Configure 'GitHubModels:Model' with a value such as 'openai/gpt-4.1-mini'.");
        }

        return modelId;
    }
}
