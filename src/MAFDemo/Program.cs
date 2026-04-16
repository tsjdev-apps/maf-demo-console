using MAFDemo.Demos;
using MAFDemo.Helpers;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

while (true)
{
    IConfigurationRoot config = ConfigurationHelper.CreateConfiguration();

    ConsoleHelper.ShowHeader();
    ConsoleHelper.WriteInfoPanel(
        "Demo Overview",
        DemoCatalog.BuildOverviewText(),
        Color.Blue);
    ConsoleHelper.WriteInfoPanel(
        "Configuration Check",
        DemoCatalog.BuildConfigurationSummary(config),
        Color.Yellow);

    string selectedDemo = ConsoleHelper.SelectFromOptions(
        [.. DemoCatalog.All.Select(demo => demo.MenuLabel), "Exit"],
        "[yellow]Please select a demo:[/]",
        false);

    if (selectedDemo == "Exit")
    {
        break;
    }

    DemoDefinition? selectedDefinition = DemoCatalog.Find(selectedDemo);

    if (selectedDefinition is not null)
    {
        IReadOnlyList<string> missingRequirements = selectedDefinition.GetMissingRequirements(config);
        if (missingRequirements.Count > 0)
        {
            ConsoleHelper.ShowHeader();
            ConsoleHelper.WriteInfoPanel(
                "Demo Not Ready",
                DemoCatalog.BuildMissingRequirementsText(selectedDefinition, missingRequirements),
                Color.Red);
            ConsoleHelper.WaitForContinue();
            continue;
        }
    }

    try
    {
        switch (selectedDemo)
        {
            case "01 - Simple Agent":
                await SimpleAgent.RunAsync();
                break;

            case "02 - Simple Agent with Conversation Thread":
                await SimpleAgentWithConversationThread.RunAsync();
                break;

            case "03 - Simple Agent with Different Providers":
                await SimpleAgentWithDifferentProviders.RunAsync();
                break;

            case "04 - Agents with Tools":
                await AgentsWithTools.RunAsync();
                break;

            case "05 - Multi-Agent Workflow (Sequential)":
                await MultiAgentWorklowSequential.RunAsync();
                break;

            case "06 - Multi-Agent Workflow (Concurrent)":
                await MultiAgentWorkflowConcurrent.RunAsync();
                break;

            case "07 - Multi-Agent Workflow (Handoff)":
                await MultiAgentWorkflowHandoff.RunAsync();
                break;

            case "08 - Multi-Agent Workflow (Group Chat)":
                await MultiAgentWorkflowGroupChat.RunAsync();
                break;

            case "09 - Multi-Agent Workflow (Magentic (Planner-led))":
                await MagenticPlannerWorkflow.RunAsync();
                break;
        }
    }
    catch (Exception ex)
    {
        ConsoleHelper.ShowHeader();
        ConsoleHelper.WriteError("The demo could not be executed.");
        if (!ConsoleHelper.TryWriteFriendlyException(ex))
        {
            ConsoleHelper.WriteException(ex);
        }
        ConsoleHelper.WaitForContinue();
    }
}
