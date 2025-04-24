using AI_Agent_Orchestrator.Models;
using AI_Agent_Orchestrator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System.Diagnostics;
using System.Net;
using System.Globalization;
using Serilog;
using Serilog.Sinks.File;

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

        // Configure Serilog for file logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "logs", "application.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
        });

        var logger = loggerFactory.CreateLogger<Program>();

        // Set up dependency injection
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(builder => builder.AddSerilog())
            .AddSingleton<AgentDiscoveryService>()
            .AddSingleton<AgentExecutionService>()
            .AddSingleton<SemanticKernelService>()
            .BuildServiceProvider();
        
        // Get required services
        var agentDiscoveryService = services.GetRequiredService<AgentDiscoveryService>();
        var agentExecutionService = services.GetRequiredService<AgentExecutionService>();
        var semanticKernelService = services.GetRequiredService<SemanticKernelService>();
        
        try
        {
            DisplayWelcomeScreen();
            
            // Prompt the user to select their preferred language
            AnsiConsole.MarkupLine("[bold cyan]Select your preferred language:[/]");
            var userLanguage = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Choose a language:[/]")
                    .AddChoices("English", "Spanish", "French", "German", "Chinese", "Japanese", "Hindi", "Other"));

            if (userLanguage == "Other")
            {
                AnsiConsole.MarkupLine("[bold yellow]Please type your preferred language:[/]");
                userLanguage = Console.ReadLine();
            }

            AnsiConsole.MarkupLine($"[bold green]You have selected:[/] {userLanguage}");

            var culture = CultureInfo.GetCultures(CultureTypes.AllCultures)
                .FirstOrDefault(c => c.EnglishName.Contains(userLanguage ?? "", StringComparison.OrdinalIgnoreCase) || 
                                    c.NativeName.Contains(userLanguage ?? "", StringComparison.OrdinalIgnoreCase));

            string languageCode;
            if (culture != null) {
                languageCode = culture.TwoLetterISOLanguageName;
            }
            else
            {
                AnsiConsole.MarkupLine("[bold red]Language not recognized. Defaulting to English.[/]");
                languageCode = "en"; // Default to English if the language is not recognized
            }
            AnsiConsole.MarkupLine($"[bold green]Language code selected:[/] {languageCode}");

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
                    
                    var agents = agentDiscoveryService.DiscoverAgentsAsync(languageCode).GetAwaiter().GetResult();
                    var workflows = agentDiscoveryService.DiscoverWorkflowsAsync(languageCode).GetAwaiter().GetResult();
                    
                    if (agents.Count == 0)
                    {
                        ctx.Status("No agents found. Check path configuration.");
                    }
                    else
                    {
                        ctx.Status($"Found {agents.Count} available agents and {workflows.Count} workflows");
                    }
                });
                
            var agents = await agentDiscoveryService.DiscoverAgentsAsync(languageCode);
            var workflows = await agentDiscoveryService.DiscoverWorkflowsAsync(languageCode);
            
            if (agents.Count == 0)
            {
                AnsiConsole.MarkupLine("[bold red]No agents were discovered.[/]");
                AnsiConsole.MarkupLine("[yellow]Please check the project structure and paths.[/]");
                return;
            }
            
            while (true)
            {
                AnsiConsole.MarkupLine("[bold cyan]Choose an option:[/]");
                var userChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold yellow]What would you like to do?[/]")
                        .AddChoices("Complete Audio Learning", "Whiteboard", "Both", "Chat with a Bot", "Exit"));

                if (userChoice == "Exit")
                {
                    AnsiConsole.MarkupLine("[bold red]Exiting the program.[/]");
                    break;
                }

                // Track which agents have been executed as part of workflows
                HashSet<string> executedAgents = new HashSet<string>();

                if (userChoice == "Complete Audio Learning")
                {
                    AnsiConsole.MarkupLine("[bold green]Executing Complete Audio Learning Assistant workflow...[/]");
                    var audioWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Complete Audio Learning", StringComparison.OrdinalIgnoreCase));

                    if (audioWorkflow != null)
                    {
                        AnsiConsole.MarkupLine($"[bold green]Executing comprehensive workflow:[/] {audioWorkflow.Name}");
                        var result = await agentExecutionService.ExecuteWorkflowAsync(audioWorkflow);

                        // Track all agents in this workflow as executed
                        foreach (var agent in audioWorkflow.Agents)
                        {
                            executedAgents.Add(agent.Name);
                        }

                        if (result)
                        {
                            AnsiConsole.MarkupLine($"[bold green]Workflow {audioWorkflow.Name} executed successfully.[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[bold red]Workflow {audioWorkflow.Name} execution failed.[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[bold red]Complete Audio Learning workflow not found.[/]");
                    }
                }
                else if (userChoice == "Whiteboard")
                {
                    AnsiConsole.MarkupLine("[bold green]Executing Complete Whiteboard Capture and Diagram Generation workflow...[/]");
                    var boardCaptureWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Whiteboard", StringComparison.OrdinalIgnoreCase));

                    if (boardCaptureWorkflow != null)
                    {
                        var result = await agentExecutionService.ExecuteWorkflowAsync(boardCaptureWorkflow);

                        // Track all agents in this workflow as executed
                        foreach (var agent in boardCaptureWorkflow.Agents)
                        {
                            executedAgents.Add(agent.Name);
                        }

                        if (result)
                        {
                            AnsiConsole.MarkupLine($"[bold green]Agent {boardCaptureWorkflow.Name} executed successfully.[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[bold red]Agent {boardCaptureWorkflow.Name} execution failed.[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[bold red]Complete Whiteboard Capture and Diagram Generation workflow not found.[/]");
                    }
                }
                else if (userChoice == "Both")
                {
                    AnsiConsole.MarkupLine("[bold green]Executing both workflows in parallel...[/]");

                    var completeAudioWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Complete Audio Learning", StringComparison.OrdinalIgnoreCase));
                    var boardCaptureWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Classroom Board Capture", StringComparison.OrdinalIgnoreCase));

                    if (completeAudioWorkflow != null && boardCaptureWorkflow != null)
                    {
                        var audioTask = agentExecutionService.ExecuteWorkflowAsync(completeAudioWorkflow);
                        var boardTask = agentExecutionService.ExecuteWorkflowAsync(boardCaptureWorkflow);

                        await Task.WhenAll(audioTask, boardTask);

                        // Track all agents in both workflows as executed
                        foreach (var agent in completeAudioWorkflow.Agents)
                        {
                            executedAgents.Add(agent.Name);
                        }
                        
                        foreach (var agent in boardCaptureWorkflow.Agents)
                        {
                            executedAgents.Add(agent.Name);
                        }

                        if (audioTask.Result)
                        {
                            AnsiConsole.MarkupLine($"[bold green]Workflow {completeAudioWorkflow.Name} executed successfully.[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[bold red]Workflow {completeAudioWorkflow.Name} execution failed.[/]");
                        }

                        if (boardTask.Result)
                        {
                            AnsiConsole.MarkupLine($"[bold green]Agent {boardCaptureWorkflow.Name} executed successfully.[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[bold red]Agent {boardCaptureWorkflow.Name} execution failed.[/]");
                        }
                    }
                }
                else if (userChoice == "Chat with a Bot")
                {
                    Console.Clear();
                    
                    DisplayChatbotWelcome();

                    // Initialize conversation history
                    string conversationHistory = string.Empty;

                    AnsiConsole.MarkupLine("[bold green]Chatbot:[/] Hello! How can I assist you today?");

                    while (true)
                    {
                        AnsiConsole.Markup("[bold cyan]You:[/] ");
                        string userInput = Console.ReadLine();

                        if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                        {
                            AnsiConsole.MarkupLine("[bold green]Chatbot:[/] Goodbye! Have a great day!");
                            break;
                        }

                        var (botResponse, updatedConversationHistory) = await semanticKernelService.ChatWithLLMAsync(userInput, conversationHistory);
                        conversationHistory = updatedConversationHistory;

                        AnsiConsole.MarkupLine($"[bold green]Chatbot:[/] {botResponse}");
                    }
                }
                // Wait for user input before continuing
                AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
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

    private static void DisplayChatbotWelcome()
    {
        var figlet = new FigletText("AI Chat") 
            .LeftJustified()
            .Color(Color.Green);

        AnsiConsole.Write(figlet);
        AnsiConsole.MarkupLine("[bold green]Welcome to the AI chat...[/]");
        AnsiConsole.MarkupLine("You can type 'exit' to stop the conversation at any time.");
        AnsiConsole.WriteLine();
    }
    
    private static AgentWorkflow? PromptForWorkflowSelectionAsync(List<AgentWorkflow> workflows)
    {
        AnsiConsole.Clear();
        var figlet = new FigletText("AI Workflow Hub") 
            .LeftJustified()
            .Color(Color.Purple);
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
    
    private static AgentInfo? PromptForAgentSelectionAsync(List<AgentInfo> agents)
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
