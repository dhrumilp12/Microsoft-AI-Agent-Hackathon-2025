using AI_Agent_Orchestrator.Models;
using AI_Agent_Orchestrator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System.Diagnostics;
using System.Net;

namespace AI_Agent_Orchestrator;

public class Program
{
    static async Task Main(string[] args)
    {
        // Load environment variables from .env file if it exists
        if (File.Exists(".env"))
        {
            DotNetEnv.Env.Load();
        }
        
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        if (File.Exists(".env"))
        {
            var azureOpenAIConfig = new
            {
                Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
                ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"),
                DeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
            };

            configuration["AzureOpenAI:Endpoint"] = azureOpenAIConfig.Endpoint;
            configuration["AzureOpenAI:ApiKey"] = azureOpenAIConfig.ApiKey;
            configuration["AzureOpenAI:DeploymentName"] = azureOpenAIConfig.DeploymentName;
        }

        // Check if the configuration is valid
        if (string.IsNullOrEmpty(configuration["AzureOpenAI:Endpoint"]) ||
            string.IsNullOrEmpty(configuration["AzureOpenAI:ApiKey"]) ||
            string.IsNullOrEmpty(configuration["AzureOpenAI:DeploymentName"]))
        {
            AnsiConsole.MarkupLine("[bold red]Error:[/] Missing Azure OpenAI configuration in appsettings.json or environment variables.");
            return;
        }

        // Set up dependency injection
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
            .AddSingleton<AgentDiscoveryService>()
            .AddSingleton<AgentExecutionService>()
            .AddSingleton<SemanticKernelService>()
            .BuildServiceProvider();
        
        // Get required services
        var logger = services.GetRequiredService<ILogger<Program>>();
        var agentDiscoveryService = services.GetRequiredService<AgentDiscoveryService>();
        var agentExecutionService = services.GetRequiredService<AgentExecutionService>();
        var semanticKernelService = services.GetRequiredService<SemanticKernelService>();
        
        try
        {
            DisplayWelcomeScreen();
            
            // Detect root directory location
            var rootDirectory = GetSolutionRootDirectory();
            logger.LogInformation($"Solution root directory: {rootDirectory}");
            
            // Initialize Semantic Kernel in the background
            var semanticKernelInitTask = semanticKernelService.InitializeAsync();
            
            // Discover available agents and workflows
            AnsiConsole.Status()
                .Start("Discovering AI agents and workflows...", ctx => 
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    
                    var agents = agentDiscoveryService.DiscoverAgentsAsync().GetAwaiter().GetResult();
                    var workflows = agentDiscoveryService.DiscoverWorkflowsAsync().GetAwaiter().GetResult();
                    
                    if (agents.Count == 0)
                    {
                        ctx.Status("No agents found. Check path configuration.");
                    }
                    else
                    {
                        ctx.Status($"Found {agents.Count} available agents and {workflows.Count} workflows");
                    }
                });
                
            var agents = await agentDiscoveryService.DiscoverAgentsAsync();
            var workflows = await agentDiscoveryService.DiscoverWorkflowsAsync();
            
            if (agents.Count == 0)
            {
                AnsiConsole.MarkupLine("[bold red]No agents were discovered.[/]");
                AnsiConsole.MarkupLine("[yellow]Please check the project structure and paths.[/]");
                return;
            }
            
            while (true)
            {
                Console.Write("Enter a query to find relevant agents or workflows (or type 'exit' to quit): ");
                string userQuery = Console.ReadLine() ?? "";

                if (string.Equals(userQuery, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine("[bold red]Exiting the program.[/]");
                    break;
                }

                // DEBUG: Show all available workflows
                AnsiConsole.MarkupLine($"[dim]Found {workflows.Count} workflows in total.[/]");
                
                // First check if any workflows match the query
                var relevantWorkflows = await semanticKernelService.FindRelevantWorkflowsAsync(workflows, userQuery);
                
                // SPECIAL CASE: If query explicitly mentions speech/translate AND flashcards/vocabulary, always show workflow
                bool forceWorkflow = userQuery.Contains("speech", StringComparison.OrdinalIgnoreCase) && 
                    (userQuery.Contains("flashcard", StringComparison.OrdinalIgnoreCase) || 
                     userQuery.Contains("vocabulary", StringComparison.OrdinalIgnoreCase));
                
                // DEBUG: Output workflow matching info
                AnsiConsole.MarkupLine($"[dim]Query matched {relevantWorkflows.Count} workflows.[/]");
                
                // If we have relevant workflows OR we should force workflow selection, offer those first
                if (relevantWorkflows.Count > 0 || forceWorkflow)
                {
                    // If forceWorkflow is true but no workflows were matched, use all workflows
                    if (forceWorkflow && relevantWorkflows.Count == 0)
                    {
                        relevantWorkflows = workflows;
                        AnsiConsole.MarkupLine("[yellow]Direct workflow match detected.[/]");
                    }
                    
                    AnsiConsole.MarkupLine("[bold cyan]Relevant workflows found:[/]");
                    foreach (var workflow in relevantWorkflows)
                    {
                        AnsiConsole.MarkupLine($"- [bold]{workflow.Name}[/]: {workflow.Description}");
                        AnsiConsole.MarkupLine($"  [dim]Steps: {string.Join(" → ", workflow.Agents.Select(a => a.Name))}[/]");
                    }
                    
                    // Prompt to use a workflow
                    if (AnsiConsole.Confirm("Would you like to execute one of these workflows?", true))
                    {
                        var workflowSelection = await PromptForWorkflowSelectionAsync(relevantWorkflows);
                        if (workflowSelection != null)
                        {
                            AnsiConsole.MarkupLine($"[bold green]Executing workflow:[/] {workflowSelection.Name}");
                            var result = await agentExecutionService.ExecuteWorkflowAsync(workflowSelection);
                            
                            if (result)
                            {
                                AnsiConsole.MarkupLine($"[bold green]Workflow {workflowSelection.Name} executed successfully.[/]");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[bold red]Workflow {workflowSelection.Name} execution failed.[/]");
                            }
                            
                            continue;
                        }
                    }
                }

                // If no workflow was selected, fall back to individual agents
                var allAgents = await agentDiscoveryService.DiscoverAgentsAsync();
                var relevantAgents = await semanticKernelService.FindRelevantAgentsAsync(allAgents, userQuery);

                if (relevantAgents.Count == allAgents.Count)
                {
                    AnsiConsole.MarkupLine("[bold yellow]No specific agents were identified. Engaging in a chat with the LLM. Type \"exit\" to stop.[/]");

                    // Use the LLM to chat with the user based on the query
                    var chatResponse = await semanticKernelService.ChatWithLLMAsync(userQuery);
                    AnsiConsole.MarkupLine("\n[bold green]LinguaLearn Bot:[/]");
                    AnsiConsole.WriteLine(chatResponse);

                    // Continue engaging in a conversation with the user
                    while (true)
                    {
                        AnsiConsole.MarkupLine("\n[bold cyan]You:[/]");
                        string followUpQuery = Console.ReadLine() ?? "";

                        if (string.Equals(followUpQuery, "exit", StringComparison.OrdinalIgnoreCase))
                        {
                            AnsiConsole.MarkupLine("[bold red]Exiting chat with the LLM.[/]");
                            break;
                        }

                        var followUpResponse = await semanticKernelService.ChatWithLLMAsync(followUpQuery);
                        AnsiConsole.MarkupLine("\n[bold green]LinguaLearn Bot:[/]");
                        AnsiConsole.WriteLine(followUpResponse);
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold cyan]Relevant agents in order:[/]");
                    foreach (var agent in relevantAgents)
                    {
                        AnsiConsole.MarkupLine($"- [bold]{agent.Name}[/]: {agent.Description}");
                    }

                    // Prompt the user to select an agent
                    while (true) {
                        var selectedAgent = await PromptForAgentSelectionAsync(relevantAgents);

                        if (selectedAgent != null)
                        {
                            AnsiConsole.MarkupLine($"[bold green]Executing agent:[/] {selectedAgent.Name}");
                            var result = await agentExecutionService.ExecuteAgentAsync(selectedAgent);

                            if (result)
                            {
                                AnsiConsole.MarkupLine($"[bold green]Agent {selectedAgent.Name} executed successfully.[/]");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[bold red]Agent {selectedAgent.Name} execution failed.[/]");
                            }

                            relevantAgents.Remove(selectedAgent);
                            if (relevantAgents.Count == 0)
                            {
                                AnsiConsole.MarkupLine("[bold red]No more relevant agents available.[/]");
                                break;
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[bold yellow]No agent was selected. Returning to the main menu.[/]");
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in the AI Agent Orchestrator");
            AnsiConsole.MarkupLine($"[bold red]Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine("[dim]Press any key to exit...[/]");
            Console.ReadKey();
        }
    }
    
    private static string GetSolutionRootDirectory()
    {
        // Start with the directory of the executing assembly
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // Navigate upwards to find the solution root
        // Usually 3 levels up from bin/Debug/netX.X
        string rootDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".."));
        
        // If we're already at the solution root, the parent directory is the repo root
        string repoRoot = Path.GetFullPath(Path.Combine(rootDir, ".."));
        
        return repoRoot;
    }
    
    private static void DisplayWelcomeScreen()
    {
        AnsiConsole.Clear();
        
        var figlet = new FigletText("AI Agent Hub")
            .LeftJustified()
            .Color(Color.Blue);
        AnsiConsole.Write(figlet);
        
        AnsiConsole.MarkupLine("[bold]Welcome to the AI Agent Orchestrator[/]");
        AnsiConsole.MarkupLine("[dim]Your central hub for accessing all available AI agents[/]");
        AnsiConsole.WriteLine();
    }
    
    private static async Task<AgentWorkflow?> PromptForWorkflowSelectionAsync(List<AgentWorkflow> workflows)
    {
        AnsiConsole.Clear();
        var figlet = new FigletText("AI Workflow Hub") 
            .LeftJustified()
            .Color(Color.Purple); // Fix: Change from Magenta to Purple which exists in Spectre.Console
        AnsiConsole.Write(figlet);
        
        var selectionPrompt = new SelectionPrompt<string>()
            .Title("Select a workflow to run:")
            .PageSize(15)
            .HighlightStyle(new Style().Foreground(Color.Green));
            
        // Add all workflows
        foreach (var workflow in workflows)
        {
            selectionPrompt.AddChoice(workflow.Name);
        }
        
        // Add exit option
        selectionPrompt.AddChoice("Exit");
        
        var selection = AnsiConsole.Prompt(selectionPrompt);
        
        if (selection == "Exit")
        {
            return null;
        }
        
        return workflows.FirstOrDefault(w => w.Name == selection);
    }
    
    private static async Task<AgentInfo?> PromptForAgentSelectionAsync(List<AgentInfo> agents)
    {
        AnsiConsole.Clear();
        var figlet = new FigletText("AI Agent Hub")
            .LeftJustified()
            .Color(Color.Blue);
        AnsiConsole.Write(figlet);
        
        var selectionPrompt = new SelectionPrompt<string>()
            .Title("Select an agent to run:")
            .PageSize(15)
            .HighlightStyle(new Style().Foreground(Color.Green));
            
        // Add all agents directly - no categories
        foreach (var agent in agents)
        {
            selectionPrompt.AddChoice(agent.Name);
        }
        
        // Add exit option
        selectionPrompt.AddChoice("Exit");
        
        var selection = AnsiConsole.Prompt(selectionPrompt);
        
        if (selection == "Exit")
        {
            return null;
        }
        
        return agents.FirstOrDefault(a => a.Name == selection);
    }
}
