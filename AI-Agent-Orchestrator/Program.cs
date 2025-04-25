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
        if (File.Exists("../.env"))
        {
            DotNetEnv.Env.Load(@"../.env");
        }
        
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        if (File.Exists("../.env"))
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

            var cosmosDBConfig = new {
                ConnectionString = Environment.GetEnvironmentVariable("COSMOSDB_CONNECTION_STRING"),
                DatabaseName = Environment.GetEnvironmentVariable("COSMOSDB_DATABASENAME"),
                ContainerName = Environment.GetEnvironmentVariable("COSMOSDB_CONTAINERNAME")
            };
            configuration["CosmosDb:ConnectionString"] = cosmosDBConfig.ConnectionString;
            configuration["CosmosDb:DatabaseName"] = cosmosDBConfig.DatabaseName;
            configuration["CosmosDb:ContainerName"] = cosmosDBConfig.ContainerName;
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
            .AddSingleton<AzureTranslationService>()
            .BuildServiceProvider();
        
        // Get required services
        var agentDiscoveryService = services.GetRequiredService<AgentDiscoveryService>();
        var agentExecutionService = services.GetRequiredService<AgentExecutionService>();
        var semanticKernelService = services.GetRequiredService<SemanticKernelService>();
        
        try
        {
            DisplayWelcomeScreen();
            
            // Create a translation service to get available languages
            var translationService = new AzureTranslationService(configuration, loggerFactory.CreateLogger<AzureTranslationService>());
            var availableLanguages = await translationService.GetAvailableLanguagesAsync();
            
            // Get target language
            await TranslationHelper.MarkupLineAsync("[bold cyan]Select your target language (language to translate to):[/]");
            
            // Display retrieving languages message
            AnsiConsole.Status()
                .Start("Retrieving available languages...", ctx => 
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("green"));
                });
            
            await TranslationHelper.MarkupLineAsync("\n[bold yellow]Available languages:[/]");
            
            // Display languages in a 3-column format
            const int columns = 3;
            var languageList = availableLanguages.ToList();
            
            for (int i = 0; i < languageList.Count; i += columns)
            {
                for (int j = 0; j < columns && i + j < languageList.Count; j++)
                {
                    var lang = languageList[i + j];
                    Console.Write($"{lang.Key}: {lang.Value}".PadRight(30));
                }
                Console.WriteLine();
            }
            
            Console.WriteLine();
            await TranslationHelper.MarkupAsync("[bold yellow]Enter the language code to translate TO:[/] ");
            string targetLanguageCode = Console.ReadLine()?.Trim().ToLower() ?? "en";
            
            // Validate the target language code
            if (!availableLanguages.ContainsKey(targetLanguageCode))
            {
                await TranslationHelper.MarkupLineAsync($"[bold red]Language code '{targetLanguageCode}' not recognized. Defaulting to English (en).[/]");
                targetLanguageCode = "en";
            }
            else
            {
                await TranslationHelper.MarkupLineAsync($"[bold green]Target language:[/] {availableLanguages[targetLanguageCode]} ({targetLanguageCode})");
            }
            
            // Now ask for source language
            await TranslationHelper.MarkupAsync("\n[bold yellow]Enter the language code to translate FROM (default: auto-detect):[/] ");
            string sourceLanguageCode = Console.ReadLine()?.Trim().ToLower() ?? "";
            
            // Validate the source language code if provided
            if (!string.IsNullOrEmpty(sourceLanguageCode) && !availableLanguages.ContainsKey(sourceLanguageCode))
            {
                await TranslationHelper.MarkupLineAsync($"[bold red]Language code '{sourceLanguageCode}' not recognized. Auto-detection will be used.[/]");
                sourceLanguageCode = "";
            }
            else if (!string.IsNullOrEmpty(sourceLanguageCode))
            {
                await TranslationHelper.MarkupLineAsync($"[bold green]Source language:[/] {availableLanguages[sourceLanguageCode]} ({sourceLanguageCode})");
            }
            else
            {
                await TranslationHelper.MarkupLineAsync("[bold green]Source language:[/] Auto-detect");
            }

            // Initialize the translation helper with the selected language
            TranslationHelper.Initialize(translationService, targetLanguageCode, sourceLanguageCode);

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
                    
                    var agents = agentDiscoveryService.DiscoverAgentsAsync(targetLanguageCode, sourceLanguageCode).GetAwaiter().GetResult();
                    var workflows = agentDiscoveryService.DiscoverWorkflowsAsync(targetLanguageCode, sourceLanguageCode).GetAwaiter().GetResult();
                    
                    if (agents.Count == 0)
                    {
                        ctx.Status("No agents found. Check path configuration.");
                    }
                    else
                    {
                        ctx.Status($"Found {agents.Count} available agents and {workflows.Count} workflows");
                    }
                });
                
            var agents = await agentDiscoveryService.DiscoverAgentsAsync(targetLanguageCode, sourceLanguageCode);
            var workflows = await agentDiscoveryService.DiscoverWorkflowsAsync(targetLanguageCode, sourceLanguageCode);
            
            if (agents.Count == 0)
            {
                await TranslationHelper.MarkupLineAsync("[bold red]No agents were discovered.[/]");
                await TranslationHelper.MarkupLineAsync("[yellow]Please check the project structure and paths.[/]");
                return;
            }
            
            while (true)
            {
                // Display the title without using markup in translated text
                Console.WriteLine(); // Add blank line
                string optionsTitle = await TranslationHelper.TranslateAsync("Choose an option:");
                Console.WriteLine(optionsTitle);
                Console.WriteLine(); // Add blank line after title

                // Get translated choices
                var choices = new List<string> { "Complete Audio Learning", "Whiteboard", "Both", "Chat with a Bot", "Exit" };
                var translatedChoices = await TranslationHelper.TranslateListAsync(choices);
                var choiceMap = choices.Zip(translatedChoices, (original, translated) => new { Original = original, Translated = translated })
                                       .ToDictionary(x => x.Translated, x => x.Original);
                
                // Create a plain selection prompt without translated markup
                var selectionPrompt = new SelectionPrompt<string>()
                    .Title("") // Don't use title in the prompt itself
                    .PageSize(15)
                    .HighlightStyle(new Style().Foreground(Color.Green))
                    .AddChoices(translatedChoices);
                
                var userChoice = AnsiConsole.Prompt(selectionPrompt);

                // Map back to original choice for code logic
                string originalChoice = choiceMap.TryGetValue(userChoice, out string? originalValue) ? originalValue : userChoice;

                if (originalChoice == "Exit")
                {
                    // Use plain text output instead of markup for translated text
                    string exitMessage = await TranslationHelper.TranslateAsync("Exiting the program.");
                    Console.WriteLine(exitMessage);
                    break;
                }

                // Track which agents have been executed as part of workflows
                HashSet<string> executedAgents = new HashSet<string>();

                if (originalChoice == "Complete Audio Learning")
                {
                    await TranslationHelper.MarkupLineAsync("[bold green]Executing Complete Audio Learning Assistant workflow...[/]");
                    var audioWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Complete Audio Learning", StringComparison.OrdinalIgnoreCase));

                    if (audioWorkflow != null)
                    {
                        var translatedName = await TranslationHelper.TranslateAsync(audioWorkflow.Name);
                        await TranslationHelper.MarkupLineAsync($"[bold green]Executing comprehensive workflow:[/] {translatedName}");
                        var result = await agentExecutionService.ExecuteWorkflowAsync(audioWorkflow);

                        // Track all agents in this workflow as executed
                        foreach (var agent in audioWorkflow.Agents)
                        {
                            executedAgents.Add(agent.Name);
                        }

                        if (result)
                        {
                            await TranslationHelper.MarkupLineAsync($"[bold green]Workflow {translatedName} executed successfully.[/]");
                        }
                        else
                        {
                            await TranslationHelper.MarkupLineAsync($"[bold red]Workflow {translatedName} execution failed.[/]");
                        }
                    }
                    else
                    {
                        await TranslationHelper.MarkupLineAsync("[bold red]Complete Audio Learning workflow not found.[/]");
                    }
                }
                else if (originalChoice == "Whiteboard")
                {
                    await TranslationHelper.MarkupLineAsync("[bold green]Executing Complete Whiteboard Capture and Diagram Generation workflow...[/]");
                    var boardCaptureWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Whiteboard", StringComparison.OrdinalIgnoreCase));

                    if (boardCaptureWorkflow != null)
                    {
                        var translatedName = await TranslationHelper.TranslateAsync(boardCaptureWorkflow.Name);
                        var result = await agentExecutionService.ExecuteWorkflowAsync(boardCaptureWorkflow);

                        // Track all agents in this workflow as executed
                        foreach (var agent in boardCaptureWorkflow.Agents)
                        {
                            executedAgents.Add(agent.Name);
                        }

                        if (result)
                        {
                            await TranslationHelper.MarkupLineAsync($"[bold green]Workflow {translatedName} executed successfully.[/]");
                        }
                        else
                        {
                            await TranslationHelper.MarkupLineAsync($"[bold red]Workflow {translatedName} execution failed.[/]");
                        }
                    }
                    else
                    {
                        await TranslationHelper.MarkupLineAsync("[bold red]Complete Whiteboard Capture and Diagram Generation workflow not found.[/]");
                    }
                }
                else if (originalChoice == "Both")
                {
                    await TranslationHelper.MarkupLineAsync("[bold green]Executing both workflows in parallel...[/]");

                    var completeAudioWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Complete Audio Learning", StringComparison.OrdinalIgnoreCase));
                    var boardCaptureWorkflow = workflows.FirstOrDefault(w => w.Name.Contains("Classroom Board Capture", StringComparison.OrdinalIgnoreCase));

                    if (completeAudioWorkflow != null && boardCaptureWorkflow != null)
                    {
                        var translatedAudioName = await TranslationHelper.TranslateAsync(completeAudioWorkflow.Name);
                        var translatedBoardName = await TranslationHelper.TranslateAsync(boardCaptureWorkflow.Name);
                        
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
                            await TranslationHelper.MarkupLineAsync($"[bold green]Workflow {translatedAudioName} executed successfully.[/]");
                        }
                        else
                        {
                            await TranslationHelper.MarkupLineAsync($"[bold red]Workflow {translatedAudioName} execution failed.[/]");
                        }

                        if (boardTask.Result)
                        {
                            await TranslationHelper.MarkupLineAsync($"[bold green]Workflow {translatedBoardName} executed successfully.[/]");
                        }
                        else
                        {
                            await TranslationHelper.MarkupLineAsync($"[bold red]Workflow {translatedBoardName} execution failed.[/]");
                        }
                    }
                }
                else if (originalChoice == "Chat with a Bot")
                {
                    Console.Clear();
                    
                    await DisplayChatbotWelcomeAsync();

                    // Initialize Cosmos DB service
                    var cosmosDbService = new CosmosDbService(
                        configuration["CosmosDb:ConnectionString"],
                        configuration["CosmosDb:DatabaseName"],
                        configuration["CosmosDb:ContainerName"]);
                    
                    var chatbotGreeting = await TranslationHelper.TranslateAsync("Hello! How can I assist you today?");
                    await TranslationHelper.MarkupLineAsync($"[bold green]Chatbot:[/] {chatbotGreeting}");

                    while (true)
                    {
                        var conversations = await cosmosDbService.GetConversationsAsync("user123");

                        string conversationHistory = cosmosDbService.ConvertConversationsToString(conversations);

                        logger.LogInformation($"Conversation history: {conversationHistory}");

                        await TranslationHelper.MarkupAsync("[bold cyan]You:[/] ");
                        string userInput = Console.ReadLine() ?? "";

                        if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                        {
                            var goodbyeMsg = await TranslationHelper.TranslateAsync("Goodbye! Have a great day!");
                            await TranslationHelper.MarkupLineAsync($"[bold green]Chatbot:[/] {goodbyeMsg}");
                            break;
                        }

                        // If user is not typing in English, translate the input to English for the LLM
                        string llmInput = userInput;
                        if (_targetLanguage != "en" && !string.IsNullOrEmpty(_targetLanguage))
                        {
                            // Translate user input to English for the LLM
                            llmInput = await translationService.TranslateTextAsync(userInput, "en", _targetLanguage);
                        }

                        var botResponse = await semanticKernelService.ChatWithLLMAsync(llmInput, conversationHistory);
                        
                        // Translate bot response to target language
                        string translatedResponse = botResponse;
                        if (_targetLanguage != "en" && !string.IsNullOrEmpty(_targetLanguage))
                        {
                            translatedResponse = await translationService.TranslateTextAsync(botResponse, _targetLanguage);
                        }

                        // Store the conversation in Cosmos DB (store original English response for future context)
                        await cosmosDbService.AddConversationAsync("user123", userInput, botResponse, conversations);

                        await TranslationHelper.MarkupLineAsync($"[bold green]Chatbot:[/] {translatedResponse}");
                    }
                }
                // Wait for user input before continuing
                await TranslationHelper.MarkupLineAsync("[dim]Press any key to continue...[/]");
                Console.ReadKey(true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred in the AI Agent Orchestrator");
            await TranslationHelper.MarkupLineAsync($"[bold red]Error:[/] {ex.Message}");
            await TranslationHelper.MarkupLineAsync("[dim]Press any key to exit...[/]");
            Console.ReadKey();
        }
    }
    
    // Track language settings as class variables to use throughout the program
    private static string _targetLanguage = "en";
    private static string _sourceLanguage = "";
    
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
        
        TranslationHelper.MarkupLineAsync("[bold]Welcome to the AI Agent Orchestrator[/]").Wait();
        TranslationHelper.MarkupLineAsync("[dim]Your central hub for accessing all available AI agents[/]").Wait();
        AnsiConsole.WriteLine();
    }

    private static async Task DisplayChatbotWelcomeAsync()
    {
        var figlet = new FigletText("AI Chat") 
            .LeftJustified()
            .Color(Color.Green);

        AnsiConsole.Write(figlet);
        await TranslationHelper.MarkupLineAsync("[bold green]Welcome to the AI chat...[/]");
        await TranslationHelper.MarkupLineAsync("You can type 'exit' to stop the conversation at any time.");
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
