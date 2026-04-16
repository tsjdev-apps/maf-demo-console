using System.ComponentModel;
using System.Net.Http;
using GeoTimeZone;
using MAFDemo.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenWeatherMapSharp;
using OpenWeatherMapSharp.Models;
using OpenWeatherMapSharp.Models.Enums;
using Spectre.Console;

namespace MAFDemo.Demos;

internal class AgentsWithTools
{
    private const string DefaultPrompt = "What's the weather like in Pforzheim, and what time is it there right now?";

    private static OpenWeatherMapService? s_weatherService;

    private sealed record LookupResult<T>(T? Value, string? ErrorMessage);

    /// <summary>
    /// Resolves a city name to the first matching geocoding result returned by OpenWeatherMap.
    /// </summary>
    /// <param name="location">The city or location name to resolve.</param>
    /// <returns>
    /// The first matching <see cref="GeocodeInfo"/> result or a friendly error message when the lookup fails.
    /// </returns>
    private static async Task<LookupResult<GeocodeInfo>> GetLocationAsync(string location)
    {
        if (s_weatherService is null)
        {
            return new LookupResult<GeocodeInfo>(null, "The weather service is not configured.");
        }

        try
        {
            OpenWeatherMapServiceResponse<List<GeocodeInfo>> locationResponse = await s_weatherService.GetLocationByNameAsync(location);

            if (!locationResponse.IsSuccess || locationResponse.Response is null)
            {
                return new LookupResult<GeocodeInfo>(
                    null,
                    $"I could not reach the location service for {location}. Please verify the internet connection and OpenWeatherMap API key.");
            }

            GeocodeInfo? locationMatch = locationResponse.Response.FirstOrDefault();
            return locationMatch is null
                ? new LookupResult<GeocodeInfo>(null, $"I could not find a matching location for {location}. Please verify the city name.")
                : new LookupResult<GeocodeInfo>(locationMatch, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new LookupResult<GeocodeInfo>(null, BuildWeatherServiceUnavailableMessage(location, ex));
        }
    }

    /// <summary>
    /// Retrieves the current weather for a location by first resolving its coordinates and then querying OpenWeatherMap.
    /// </summary>
    /// <param name="location">The city or location name to query.</param>
    /// <returns>
    /// A <see cref="WeatherRoot"/> payload when weather data can be retrieved; otherwise, a friendly error message.
    /// </returns>
    private static async Task<LookupResult<WeatherRoot>> GetWeatherDataAsync(string location)
    {
        LookupResult<GeocodeInfo> locationResult = await GetLocationAsync(location);

        if (locationResult.Value is null || s_weatherService is null)
        {
            return new LookupResult<WeatherRoot>(null, locationResult.ErrorMessage ?? "The weather service is not available.");
        }

        try
        {
            OpenWeatherMapServiceResponse<WeatherRoot> weatherResponse = await s_weatherService.GetWeatherAsync(
                locationResult.Value.Latitude,
                locationResult.Value.Longitude,
                LanguageCode.EN,
                Unit.Metric);

            if (!weatherResponse.IsSuccess || weatherResponse.Response is null)
            {
                return new LookupResult<WeatherRoot>(
                    null,
                    $"I could not retrieve live weather data for {location}. Please verify the internet connection and OpenWeatherMap API key.");
            }

            return new LookupResult<WeatherRoot>(weatherResponse.Response, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new LookupResult<WeatherRoot>(null, BuildWeatherServiceUnavailableMessage(location, ex));
        }
    }

    /// <summary>
    /// Exposes current weather information as a callable agent tool for the specified location.
    /// </summary>
    /// <param name="location">The city or location name to query.</param>
    /// <returns>
    /// A weather payload when the lookup succeeds; otherwise, a descriptive error string suitable for agent output.
    /// </returns>
    [Description("Get the current weather for a given location.")]
    private static async Task<object> GetWeatherAsync(
        [Description("The location to get the weather for.")]
        string location)
    {
        LookupResult<WeatherRoot> weatherResult = await GetWeatherDataAsync(location);

        if (weatherResult.Value is null)
        {
            return weatherResult.ErrorMessage ?? $"I could not retrieve the weather for {location}.";
        }

        return weatherResult.Value;
    }

    /// <summary>
    /// Exposes the current local time for a location as a callable agent tool by resolving the location's timezone.
    /// </summary>
    /// <param name="location">The city or location name to query.</param>
    /// <returns>
    /// A user-facing message containing the current local time, or an explanatory error message when the lookup fails.
    /// </returns>
    [Description("Get the current local time for a given location.")]
    private static async Task<string> GetCurrentTimeAsync(
        [Description("The location to get the current local time for.")]
        string location)
    {
        LookupResult<GeocodeInfo> locationResult = await GetLocationAsync(location);

        if (locationResult.Value is null)
        {
            return locationResult.ErrorMessage ?? $"I could not retrieve the current time for {location}.";
        }

        TimeZoneResult timeZoneResult = TimeZoneLookup.GetTimeZone(locationResult.Value.Latitude, locationResult.Value.Longitude);
        string[] timeZoneIds = [timeZoneResult.Result, .. timeZoneResult.AlternativeResults.Where(id => !string.IsNullOrWhiteSpace(id))];

        foreach (string? timeZoneId in timeZoneIds.Distinct(StringComparer.Ordinal))
        {
            try
            {
                TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                DateTimeOffset localTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
                return $"The current local time in {location} is {localTime:dddd, yyyy-MM-dd HH:mm:ss}.";
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return $"I could not resolve the timezone details for {location}.";
    }

    private static string BuildWeatherServiceUnavailableMessage(string location, Exception ex)
    {
        return ex switch
        {
            HttpRequestException => $"I could not reach the weather service for {location}. Please verify the internet connection or whether the service is currently reachable.",
            TaskCanceledException => $"The weather service timed out while retrieving data for {location}. Please try again in a moment.",
            _ => $"I could not retrieve weather data for {location} because the weather service is currently unavailable."
        };
    }

    /// <summary>
    /// Runs the tool-enabled demo that lets an agent answer weather and local-time questions with live helper functions.
    /// </summary>
    /// <returns>A task that completes when the user leaves the demo.</returns>
    public static async Task RunAsync()
    {
        IConfigurationRoot config = ConfigurationHelper.CreateConfiguration();
        string openWeatherMapApiKey = ConfigurationHelper.GetRequiredSetting(config, "OpenWeatherMapApiKey");

        s_weatherService = new OpenWeatherMapService(openWeatherMapApiKey);

        AIFunction weatherTool = AIFunctionFactory.Create(GetWeatherAsync);
        AIFunction currentTimeTool = AIFunctionFactory.Create(GetCurrentTimeAsync);

        IChatClient chatClient = ChatClientHelper.CreateAzureOpenAIChatClient(config);

        AIAgent weatherAssistant = chatClient.AsAIAgent(
            name: "weather-assistant",
            instructions: "You are a helpful assistant that can check the weather and the current local time for a city. Use the available tools whenever the user asks for weather or time information.",
            tools: [weatherTool, currentTimeTool]);

        while (true)
        {
            AgentSession session = await weatherAssistant.CreateSessionAsync();

            while (true)
            {
                ConsoleHelper.ShowDemoStage(
                    "Demo 04 - Agents with Tools",
                    "Answer real-world questions by combining an agent with weather and local-time tools.");
                ConsoleHelper.WriteInfoPanel("Example Prompt", DefaultPrompt, Color.Grey);

                string? userMessage = ConsoleHelper.GetStringFromConsole(
                    "[yellow]Ask about weather or local time, or press Enter to run the example shown above.[/]",
                    true,
                    true,
                    false);

                if (string.IsNullOrWhiteSpace(userMessage))
                {
                    userMessage = DefaultPrompt;
                }

                ConsoleHelper.ShowDemoStage(
                    "Demo 04 - Agents with Tools",
                    "Answer real-world questions by combining an agent with weather and local-time tools.");
                ConsoleHelper.WriteInfoPanel("Question", userMessage, Color.Grey);
                await ConsoleHelper.StreamResponsePanelAsync(
                    "Agent Response",
                    weatherAssistant.RunStreamingAsync(userMessage, session),
                    Color.Red,
                    "Calling tools and composing answer...");

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
}
