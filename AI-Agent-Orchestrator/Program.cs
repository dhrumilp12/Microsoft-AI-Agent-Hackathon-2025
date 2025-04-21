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
            
            // Discover available agents
            AnsiConsole.Status()
                .Start("Discovering AI agents...", ctx => 
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    
                    var agents = agentDiscoveryService.DiscoverAgentsAsync().GetAwaiter().GetResult();
                    
                    if (agents.Count == 0)
                    {
                        ctx.Status("No agents found. Check path configuration.");
                    }
                    else
                    {
                        ctx.Status($"Found {agents.Count} available agents");
                    }
                    
                    return agents;
                });
                
            var agents = await agentDiscoveryService.DiscoverAgentsAsync();
            
            if (agents.Count == 0)
            {
                AnsiConsole.MarkupLine("[bold red]No agents were discovered.[/]");
                AnsiConsole.MarkupLine("[yellow]Please check the project structure and paths.[/]");
                return;
            }
            
            // Use FindRelevantAgentsAsync to determine relevant agents based on user input
            Console.Write("Enter a query to find relevant agents: ");
            string userQuery = Console.ReadLine();

            var allAgents = await agentDiscoveryService.DiscoverAgentsAsync();
            var relevantAgents = await semanticKernelService.FindRelevantAgentsAsync(allAgents, userQuery);

            Console.WriteLine("Relevant agents:");
            foreach (var agent in relevantAgents)
            {
                Console.WriteLine($"- {agent.Name}: {agent.Description}");
            }
            
            while (true)
            {
                var selectedAgent = await PromptForAgentSelectionAsync(relevantAgents);
                
                if (selectedAgent == null)
                {
                    break; // User chose to exit
                }
                
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"[bold green]Launching:[/] {selectedAgent.Name}");
                AnsiConsole.MarkupLine($"[dim]The agent will open in a new window. Return here after you're done.[/]");
                
                // Show directory information for debugging
                if (!Directory.Exists(selectedAgent.WorkingDirectory))
                {
                    AnsiConsole.MarkupLine($"[bold red]Warning:[/] Working directory does not exist: {selectedAgent.WorkingDirectory}");
                    AnsiConsole.MarkupLine("[yellow]Would you like to provide an alternative path? (Y/N)[/]");
                    
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Y)
                    {
                        AnsiConsole.WriteLine();
                        var path = AnsiConsole.Ask<string>("Enter the correct path to the agent directory:");
                        if (Directory.Exists(path))
                        {
                            selectedAgent.WorkingDirectory = path;
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[bold red]Path does not exist:[/] {path}");
                            AnsiConsole.MarkupLine("[yellow]Press any key to return to agent selection...[/]");
                            Console.ReadKey(true);
                            continue;
                        }
                    }
                    else
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[yellow]Returning to agent selection...[/]");
                        continue;
                    }
                }
                
                var result = await agentExecutionService.ExecuteAgentAsync(selectedAgent);
                
                // After agent execution
                AnsiConsole.WriteLine();
                if (result)
                {
                    AnsiConsole.MarkupLine("[bold green]Agent execution completed successfully.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold red]Agent execution completed with errors.[/]");
                }
                AnsiConsole.MarkupLine("[dim]Press any key to return to agent selection...[/]");
                Console.ReadKey(true);
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
