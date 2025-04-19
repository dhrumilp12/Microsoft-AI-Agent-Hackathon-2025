using AI_Agent_Orchestrator.Services;
using Microsoft.Extensions.Configuration;
using DotNetEnv;
using SpeechTranslator.Services;
using System;
using System.Threading.Tasks;
using VocabularyBank.Services;

namespace AI_Agent_Orchestrator
{
    public class LinguaLearn
    {
        public static string GetWelcomeMessage()
        {
            return "Welcome to the AI Agent Orchestrator!";
        }

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing AI Agent Orchestrator...");

            Env.Load();

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            Console.WriteLine($"SpeechKey: {configuration["Azure__SpeechKey"]}");

            // Example initialization of services
            var kernelService = new KernelService(configuration);

            var orchestratorService = new OrchestratorService(kernelService, configuration, new AzureOpenAIService(configuration));

            Console.Clear();

            Console.WriteLine(GetWelcomeMessage());

            Console.WriteLine("What would you like to do?");
            Console.WriteLine("1. Speech-to-Text and Translation");
            Console.WriteLine("2. Vocabulary Extraction and Flashcard Generation");
            Console.Write("Enter your choice (1 or 2): ");
            string choice = Console.ReadLine();

            if (choice == "1")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Usage for Speech-to-Text and Translation: <audioOrVideoFilePath> <sourceLanguage> <targetLanguage>");
                    return;
                }

                string audioOrVideoFilePath = args[0];
                string sourceLanguage = args[1];
                string targetLanguage = args[2];

                await orchestratorService.RunAsync("1", audioOrVideoFilePath: audioOrVideoFilePath, sourceLanguage: sourceLanguage, targetLanguage: targetLanguage);
            }
            else if (choice == "2")
            {
                Console.WriteLine("Enter the text for vocabulary extraction:");
                string text = Console.ReadLine();

                await orchestratorService.RunAsync("2", text: text);
            }
            else
            {
                Console.WriteLine("Invalid choice. Please restart the application and choose either 1 or 2.");
            }
        }
    }
}
