using Spectre.Console;

using Microsoft.Agents.AI;
using Azure;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

using System.ClientModel;

namespace MAFDemo.Helpers;

/// <summary>
/// Provides shared console UI helpers for the demos.
/// </summary>
public static class ConsoleHelper
{
    /// <summary>
    /// Clears the console and renders the shared application banner used by all demos.
    /// </summary>
    public static void ShowHeader()
    {
        AnsiConsole.Clear();

        Grid grid = new();
        grid.AddColumn();

        grid.AddRow(
            new FigletText("MAF Demos")
                .Centered()
                .Color(Color.Red));

        Panel attributionPanel = new Panel(
            "[bold red]Samples by Thomas Sebastian Jensen[/]\n" +
            "[link]https://www.tsjdev-apps.de[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);
        attributionPanel.Expand = true;

        grid.AddRow(Align.Center(attributionPanel));

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Clears the console, renders the shared header, and shows the current demo title together with a short description.
    /// </summary>
    /// <param name="title">The title of the demo currently being shown.</param>
    /// <param name="subtitle">A short description that explains the purpose of the demo.</param>
    public static void ShowDemoStage(
        string title,
        string subtitle)
    {
        ShowHeader();
        WriteInfoPanel(title, subtitle, Color.Red);
    }

    /// <summary>
    /// Renders a bordered information panel with a title and escaped plain-text content.
    /// </summary>
    /// <param name="title">The panel title.</param>
    /// <param name="content">The plain-text content shown inside the panel.</param>
    /// <param name="borderColor">The border color used to accent the panel.</param>
    public static void WriteInfoPanel(
        string title,
        string content,
        Color borderColor)
    {
        AnsiConsole.Write(CreatePanel(title, content, borderColor));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Streams agent response updates into a live panel and returns the accumulated final text.
    /// </summary>
    /// <param name="title">The panel title shown while streaming.</param>
    /// <param name="updates">The asynchronous response update sequence emitted by an agent.</param>
    /// <param name="borderColor">The border color used to accent the live panel.</param>
    /// <param name="pendingContent">The placeholder text shown before the first response chunk arrives.</param>
    /// <returns>The accumulated response text, or a fallback message when no text was produced.</returns>
    public static async Task<string> StreamResponsePanelAsync(
        string title,
        IAsyncEnumerable<AgentResponseUpdate> updates,
        Color borderColor,
        string pendingContent = "Waiting for response...")
    {
        StringBuilder builder = new();

        await AnsiConsole.Live(CreatePanel(title, pendingContent, borderColor))
            .AutoClear(true)
            .StartAsync(async context =>
            {
                await foreach (AgentResponseUpdate update in updates)
                {
                    if (string.IsNullOrEmpty(update.Text))
                    {
                        continue;
                    }

                    builder.Append(update.Text);
                    context.UpdateTarget(CreatePanel(title, builder.ToString(), borderColor));
                }
            });

        string finalContent = builder.Length > 0
            ? builder.ToString()
            : "No response was produced.";
        AnsiConsole.Write(CreatePanel(title, finalContent, borderColor));
        AnsiConsole.WriteLine();
        return finalContent;
    }

    /// <summary>
    /// Displays a selection prompt and returns the option chosen by the user.
    /// </summary>
    /// <param name="options">The ordered list of options to display.</param>
    /// <param name="prompt">The prompt text shown above the selectable options.</param>
    /// <param name="showHeader">
    /// <see langword="true"/> to render the shared demo header before showing the prompt; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>The option selected by the user.</returns>
    public static string SelectFromOptions(
        List<string> options,
        string prompt,
        bool showHeader = true)
    {
        if (options.Count == 0)
        {
            throw new ArgumentException("At least one option must be provided.", nameof(options));
        }

        if (showHeader)
        {
            ShowHeader();
        }

        SelectionPrompt<string> selectionPrompt = new SelectionPrompt<string>()
            .Title(prompt)
            .MoreChoicesText("[grey](Use the arrow keys to navigate through the options.)[/]")
            .AddChoices(options);

        if (options.Count >= 3)
        {
            selectionPrompt.PageSize(Math.Min(options.Count, 10));
        }

        return AnsiConsole.Prompt(selectionPrompt);
    }

    /// <summary>
    /// Prompts the user for free-form text input with optional empty-value and length validation.
    /// </summary>
    /// <param name="prompt">The prompt text shown to the user.</param>
    /// <param name="allowEmptyValue">
    /// <see langword="true"/> to allow empty input; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="validateLength">
    /// <see langword="true"/> to enforce the helper's maximum input length; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="showHeader">
    /// <see langword="true"/> to clear the console and render the shared header before showing the prompt; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>
    /// The text entered by the user, or <see langword="null"/> when empty input is allowed and no value was provided.
    /// </returns>
    public static string? GetStringFromConsole(
        string prompt, 
        bool allowEmptyValue = false, 
        bool validateLength = true,
        bool showHeader = true)
    {
        if (showHeader)
        {
            ShowHeader();
        }

        TextPrompt<string?> textPrompt = new TextPrompt<string?>(prompt)
            .PromptStyle("white")
            .ValidationErrorMessage("[red]Invalid input[/]")
            .Validate(value => ValidateInput(value, allowEmptyValue, validateLength));

        if (allowEmptyValue)
        {
            textPrompt = textPrompt.AllowEmpty();
        }

        return AnsiConsole.Prompt(textPrompt);
    }

    /// <summary>
    /// Prompts the user for an absolute HTTP or HTTPS URL and validates the entered value.
    /// </summary>
    /// <param name="prompt">The prompt text shown to the user.</param>
    /// <param name="validateLength">
    /// <see langword="true"/> to enforce the helper's maximum input length; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="showHeader">
    /// <see langword="true"/> to clear the console and render the shared header before showing the prompt; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>A validated absolute HTTP or HTTPS URL string.</returns>
    public static string GetUrlFromConsole(
        string prompt, 
        bool validateLength = true,
        bool showHeader = true)
    {
        if (showHeader)
        {
            ShowHeader();
        }

        return AnsiConsole.Prompt(
            new TextPrompt<string>(prompt)
                .PromptStyle("white")
                .ValidationErrorMessage("[red]Invalid URL[/]")
                .Validate(value =>
                {
                    ValidationResult result = ValidateInput(value, false, validateLength);
                    if (!result.Successful)
                    {
                        return result;
                    }

                    if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uriResult) ||
                        (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                    {
                        return ValidationResult.Error("[red]Invalid URL format[/]");
                    }

                    return ValidationResult.Success();
                }));
    }

    /// <summary>
    /// Writes an error message using the shared error styling.
    /// </summary>
    /// <param name="errorMessage">The error message to display.</param>
    public static void WriteError(
        string errorMessage)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(errorMessage)}[/]");
    }

    /// <summary>
    /// Writes Spectre.Console markup using the shared console output flow.
    /// </summary>
    /// <param name="markup">The markup string to render.</param>
    public static void WriteMarkup(
        string markup)
    {
        AnsiConsole.MarkupLine(markup);
    }

    /// <summary>
    /// Renders an exception using Spectre.Console's formatted exception output.
    /// </summary>
    /// <param name="ex">The exception to display.</param>
    public static void WriteException(
        Exception ex)
    {
        AnsiConsole.WriteException(
            ex,
            ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);
    }

    /// <summary>
    /// Tries to render a more actionable explanation for known service and configuration failures.
    /// </summary>
    /// <param name="ex">The exception to analyze.</param>
    /// <returns>
    /// <see langword="true"/> when a friendly explanation was rendered; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryWriteFriendlyException(
        Exception ex)
    {
        if (ex is InvalidOperationException missingSettingException &&
            missingSettingException.Message.StartsWith("User Secret '", StringComparison.Ordinal))
        {
            WriteInfoPanel(
                "Configuration Missing",
                missingSettingException.Message +
                "\nUse dotnet user-secrets to add the missing value before running the demo again.",
                Color.Red);
            return true;
        }

        if (ex is InvalidOperationException invalidOperationException &&
            invalidOperationException.Message.StartsWith("GitHub Models", StringComparison.Ordinal))
        {
            WriteInfoPanel(
                "GitHub Models Configuration Missing",
                invalidOperationException.Message,
                Color.Red);
            WriteInfoPanel(
                "What To Configure",
                "Add the following configuration for Demo 03:\n" +
                "- GitHubModels:Token or the GITHUB_TOKEN environment variable\n" +
                "- GitHubModels:Model\n" +
                "- GitHubModels:Organization, only if you want organization-attributed requests",
                Color.Yellow);
            return true;
        }

        if (ex is UriFormatException uriFormatException)
        {
            WriteInfoPanel(
                "Invalid Endpoint Configuration",
                "One of the configured service URLs is not a valid absolute URI.\n" +
                uriFormatException.Message,
                Color.Red);
            WriteInfoPanel(
                "What To Check",
                "Verify AzureOpenAI:Endpoint, Ollama:Endpoint, and any custom GitHubModels:Endpoint override.",
                Color.Yellow);
            return true;
        }

        if (ex is HttpRequestException httpRequestException)
        {
            SocketException? socketException = FindInnerException<SocketException>(httpRequestException);
            string networkDetails = socketException is not null
                ? GetSocketErrorMessage(socketException)
                : httpRequestException.Message;

            WriteInfoPanel(
                "Network Error",
                "The service could not be reached.\n" + networkDetails,
                Color.Red);
            WriteInfoPanel(
                "What To Check",
                "Verify that the computer has internet connectivity, the endpoint URL is correct, and local services such as Ollama are running.",
                Color.Yellow);
            return true;
        }

        if (ex is TaskCanceledException)
        {
            WriteInfoPanel(
                "Request Timed Out",
                "The request did not complete in time. This can happen when the network connection is unstable or the target service is temporarily unavailable.",
                Color.Red);
            WriteInfoPanel(
                "What To Check",
                "Verify the internet connection, confirm the target service is online, and then try the demo again.",
                Color.Yellow);
            return true;
        }

        if (ex is SocketException standaloneSocketException)
        {
            WriteInfoPanel(
                "Network Error",
                GetSocketErrorMessage(standaloneSocketException),
                Color.Red);
            WriteInfoPanel(
                "What To Check",
                "Verify the internet connection, local DNS resolution, firewall rules, and any locally hosted endpoint such as Ollama.",
                Color.Yellow);
            return true;
        }

        if (ex is RequestFailedException requestFailedException)
        {
            if (requestFailedException.Status <= 0)
            {
                WriteInfoPanel(
                    "Network Error",
                    "The request could not be completed because the target service could not be reached.\n" +
                    requestFailedException.Message,
                    Color.Red);
                return true;
            }

            if (requestFailedException.Status == 401)
            {
                WriteInfoPanel(
                    "Authentication Failed",
                    "The model provider rejected the request with HTTP 401 Unauthorized.\n" +
                    "This usually means the configured credentials do not match the selected service or the token is missing a required permission.",
                    Color.Red);
                WriteInfoPanel(
                    "What To Check",
                    "For Azure OpenAI verify:\n" +
                    "- AzureOpenAI:Endpoint\n" +
                    "- AzureOpenAI:ApiKey\n" +
                    "- AzureOpenAI:Deployment\n\n" +
                    "For GitHub Models verify:\n" +
                    "- GitHubModels:Token or the GITHUB_TOKEN environment variable\n" +
                    "- the GitHub token has the models:read permission\n" +
                    "- GitHubModels:Model",
                    Color.Yellow);
                return true;
            }

            if (requestFailedException.Status == 404)
            {
                WriteInfoPanel(
                    "Resource Not Found",
                    "The request reached the service, but the target model endpoint or model identifier was not found.",
                    Color.Red);
                WriteInfoPanel(
                    "What To Check",
                    "For Azure OpenAI verify:\n" +
                    "- AzureOpenAI:Endpoint\n" +
                    "- AzureOpenAI:Deployment\n\n" +
                    "For GitHub Models verify:\n" +
                    "- GitHubModels:Model\n" +
                    "- GitHubModels:Organization, if you are using an organization-scoped endpoint\n" +
                    "- GitHubModels:Endpoint, if you intentionally overrode the default endpoint",
                    Color.Yellow);
                return true;
            }

            if (requestFailedException.Status == 429)
            {
                WriteInfoPanel(
                    "Rate Limit Reached",
                    "The provider accepted the connection but rejected the request because too many requests were sent in a short time.",
                    Color.Red);
                WriteInfoPanel(
                    "What To Check",
                    "Wait a moment, reduce retry frequency, or use a different model deployment if one is available.",
                    Color.Yellow);
                return true;
            }

            if (requestFailedException.Status is 502 or 503 or 504)
            {
                WriteInfoPanel(
                    "Service Unavailable",
                    $"The provider is currently unavailable or did not respond in time (HTTP {requestFailedException.Status}).",
                    Color.Red);
                WriteInfoPanel(
                    "What To Check",
                    "Verify internet connectivity and retry after a short pause. If the issue persists, check the provider status or endpoint configuration.",
                    Color.Yellow);
                return true;
            }

            WriteInfoPanel(
                "Service Request Failed",
                $"The provider returned HTTP {requestFailedException.Status}.\n{requestFailedException.Message}",
                Color.Red);
            return true;
        }

        if (ex is ClientResultException clientResultException)
        {
            if (clientResultException.Status <= 0)
            {
                WriteInfoPanel(
                    "Network Error",
                    "The request did not reach the target service.\n" +
                    clientResultException.Message,
                    Color.Red);
                return true;
            }

            if (clientResultException.Status == 401)
            {
                WriteInfoPanel(
                    "Authentication Failed",
                    "The model provider rejected the request with HTTP 401 Unauthorized.\n" +
                    "This usually means the configured credentials do not match the target service.",
                    Color.Red);
                WriteInfoPanel(
                    "What To Check",
                    "For Azure OpenAI verify:\n" +
                    "- AzureOpenAI:Endpoint\n" +
                    "- AzureOpenAI:ApiKey\n" +
                    "- AzureOpenAI:Deployment\n\n" +
                    "For GitHub Models verify:\n" +
                    "- GitHubModels:Token or the GITHUB_TOKEN environment variable\n" +
                    "- the GitHub token has the models:read permission\n" +
                    "- GitHubModels:Model",
                    Color.Yellow);
                return true;
            }

            if (clientResultException.Status == 404)
            {
                WriteInfoPanel(
                    "Resource Not Found",
                    "The request reached the service, but the target resource was not found.\n" +
                    "In Azure OpenAI this usually means the deployment name is wrong or the endpoint points to the wrong resource. In GitHub Models this can also indicate an invalid model identifier or organization endpoint.",
                    Color.Red);
                WriteInfoPanel(
                    "What To Check",
                    "Verify these settings:\n" +
                    "- AzureOpenAI:Endpoint\n" +
                    "- AzureOpenAI:Deployment\n" +
                    "- GitHubModels:Model\n" +
                    "- GitHubModels:Organization, if used",
                    Color.Yellow);
                return true;
            }

            if (clientResultException.Status == 429)
            {
                WriteInfoPanel(
                    "Rate Limit Reached",
                    "The provider rejected the request because too many requests were sent in a short period.",
                    Color.Red);
                return true;
            }

            if (clientResultException.Status is 502 or 503 or 504)
            {
                WriteInfoPanel(
                    "Service Unavailable",
                    $"The provider is currently unavailable or timed out (HTTP {clientResultException.Status}).",
                    Color.Red);
                return true;
            }

            WriteInfoPanel(
                "Service Request Failed",
                $"The provider returned HTTP {clientResultException.Status}.\n{clientResultException.Message}",
                Color.Red);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Displays an exit prompt and waits for a key press before returning.
    /// </summary>
    public static void WaitForExit()
    {
        AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Displays a continuation prompt and waits for a key press before returning.
    /// </summary>
    public static void WaitForContinue()
    {
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Validates a raw text input value against the helper's empty-value and maximum-length rules.
    /// </summary>
    /// <param name="value">The input value to validate.</param>
    /// <param name="allowEmptyValue">
    /// <see langword="true"/> to accept empty or whitespace-only values; otherwise, <see langword="false"/>.
    /// </param>
    /// <param name="validateLength">
    /// <see langword="true"/> to reject values longer than the configured maximum; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ValidationResult"/> describing whether the provided value satisfies the configured validation rules.
    /// </returns>
    static ValidationResult ValidateInput(string? value, bool allowEmptyValue, bool validateLength)
    {
        if (!allowEmptyValue && (string.IsNullOrWhiteSpace(value) || value.Length < 3))
        {
            return ValidationResult.Error("[red]Value too short[/]");
        }

        return validateLength && value?.Length > 200
            ? ValidationResult.Error("[red]Value too long[/]")
            : ValidationResult.Success();
    }

    /// <summary>
    /// Creates a reusable bordered panel for presentation-oriented console output.
    /// </summary>
    /// <param name="title">The title displayed in the panel header.</param>
    /// <param name="content">The plain-text content rendered inside the panel.</param>
    /// <param name="borderColor">The border color used to accent the panel.</param>
    /// <returns>A configured <see cref="Panel"/> instance ready for rendering.</returns>
    static Panel CreatePanel(string title, string content, Color borderColor)
    {
        Panel panel = new Panel(new Markup(Markup.Escape(content)))
            .Header($"[bold]{Markup.Escape(title)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(borderColor);
        panel.Expand = true;
        return panel;
    }

    private static TException? FindInnerException<TException>(Exception exception)
        where TException : Exception
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is TException matchingException)
            {
                return matchingException;
            }

            current = current.InnerException;
        }

        return null;
    }

    private static string GetSocketErrorMessage(SocketException socketException)
    {
        return socketException.SocketErrorCode switch
        {
            SocketError.HostNotFound or SocketError.NoData =>
                "The host name could not be resolved. This often indicates no internet connection or an incorrect service URL.",
            SocketError.ConnectionRefused =>
                "The connection was refused by the target host. A local service such as Ollama may not be running or the remote endpoint may reject the connection.",
            SocketError.TimedOut =>
                "The connection attempt timed out before the target service responded.",
            SocketError.NetworkUnreachable =>
                "The network is unreachable. The machine may be offline or blocked from reaching the target service.",
            _ =>
                $"The network request failed with socket error '{socketException.SocketErrorCode}': {socketException.Message}"
        };
    }
}
