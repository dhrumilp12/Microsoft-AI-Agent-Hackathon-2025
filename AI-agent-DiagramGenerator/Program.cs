using DiagramGenerator.Services;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiagramGenerator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Load environment variables from .env file using the simpler API
            Env.Load();
            
            // For debugging, print the loaded environment variables
            Console.WriteLine($"Loaded environment variables:");
            Console.WriteLine($"AZURE_OPENAI_ENDPOINT: {Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")}");
            
            // Only print first few characters of the key for security
            var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
            Console.WriteLine($"AZURE_OPENAI_KEY: {(string.IsNullOrEmpty(key) ? "null" : key.Substring(0, Math.Min(5, key.Length)) + "... (truncated)")}");
            
            Console.WriteLine($"AZURE_OPENAI_DEPLOYMENT_NAME: {Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")}");
            
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables() // This will include .env variables loaded by DotNetEnv
                .Build();

            // Setup DI
            var services = new ServiceCollection()
                .AddLogging(builder => 
                {
                    builder.AddConsole();
                    // Change minimum level to Debug to see more details about JSON parsing
                    builder.SetMinimumLevel(LogLevel.Debug);
                })
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<AzureOpenAIClientService>() // Register our new service
                .AddSingleton<ISpeechRecognitionService, SpeechRecognitionService>()
                .AddSingleton<IConceptExtractorService, ConceptExtractorService>()
                .AddSingleton<IDiagramGeneratorService, DiagramGeneratorService>()
                .AddSingleton<IWhiteboardIntegrationService, WhiteboardIntegrationService>()
                .AddSingleton<IDiagramInteractionService, DiagramInteractionService>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Starting Diagram Generator...");

            var diagramManager = new DiagramManager(
                services.GetRequiredService<ISpeechRecognitionService>(),
                services.GetRequiredService<IConceptExtractorService>(),
                services.GetRequiredService<IDiagramGeneratorService>(),
                services.GetRequiredService<IWhiteboardIntegrationService>(),
                services.GetRequiredService<IDiagramInteractionService>(),
                services.GetRequiredService<ILogger<DiagramManager>>()
            );

            await RunApplicationAsync(diagramManager, logger);
        }

        private static async Task RunApplicationAsync(DiagramManager diagramManager, ILogger logger)
        {
            Console.WriteLine("Welcome to Visual Diagram Generator!");
            Console.WriteLine("===================================\n");
            Console.WriteLine("Options:");
            Console.WriteLine("1. Start listening to lecture and generate diagrams");
            Console.WriteLine("2. Load transcript from file and generate diagrams");
            Console.WriteLine("3. Interact with existing diagram");
            Console.WriteLine("4. Exit");

            while (true)
            {
                Console.Write("\nSelect an option (1-4): ");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await diagramManager.StartListeningAndGenerateDiagramAsync();
                        break;
                    case "2":
                        Console.Write("Enter file path: ");
                        var filePath = Console.ReadLine();
                        if (!string.IsNullOrEmpty(filePath))
                            await diagramManager.GenerateDiagramFromFileAsync(filePath);
                        break;
                    case "3":
                        await diagramManager.InteractWithDiagramAsync();
                        break;
                    case "4":
                        logger.LogInformation("Exiting application");
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }
    }
}
