using AI_Agent_Orchestrator.Services;
using Microsoft.Extensions.Configuration;
using SpeechTranslator.Services;
using System;
using System.Threading.Tasks;
using VocabularyBank.Services;

namespace AI_Agent_Orchestrator
{
    public class LinguaLearn
    {
        private readonly OrchestratorService _orchestratorService;

        public LinguaLearn(OrchestratorService orchestratorService)
        {
            _orchestratorService = orchestratorService;
        }

        public async Task<string> RunOrchestrationAsync(string audioOrVideoFilePath, string sourceLanguage, string targetLanguage)
        {
            await _orchestratorService.RunAsync(audioOrVideoFilePath, sourceLanguage, targetLanguage);
            return "Orchestration completed successfully.";
        }

        public static string GetWelcomeMessage()
        {
            return "Welcome to the AI Agent Orchestrator!";
        }

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing AI Agent Orchestrator...");

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Example initialization of services
            var kernelService = new KernelService();

            var orchestratorService = new OrchestratorService(kernelService, configuration, new AzureOpenAIService(configuration));

            var class1 = new LinguaLearn(orchestratorService);

            Console.WriteLine(GetWelcomeMessage());

            // Example usage
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: <sourceLanguage> <targetLanguage> <audioOrVideoFilePath=null>");
                return;
            }

            string sourceLanguage = args[0];
            string targetLanguage = args[1];
            string audioOrVideoFilePath = args[2];

            string result = await class1.RunOrchestrationAsync(audioOrVideoFilePath, sourceLanguage, targetLanguage);
            Console.WriteLine(result);
        }
    }
}
