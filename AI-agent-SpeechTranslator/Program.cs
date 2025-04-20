using System;
using System.Threading.Tasks;
using DotNetEnv;
using SpeechTranslator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SpeechTranslator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Env.Load(Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, ".env"));
            // Load configuration from appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Retrieve Speech Service configuration
            string speechApiKey = configuration["SpeechService:ApiKey"];
            string speechEndpoint = configuration["SpeechService:Endpoint"];

            // Retrieve Translator Service configuration
            string translatorApiKey = configuration["TranslatorService:ApiKey"];
            string translatorRegion = configuration["TranslatorService:Region"];

            // Load .env variables
            //DotEnv.Load(new DotEnvOptions(envFilePaths: [".env"]));

            // Override with .env variables if available
            speechApiKey = Environment.GetEnvironmentVariable("SPEECH_API_KEY") ?? speechApiKey;
            speechEndpoint = Environment.GetEnvironmentVariable("SPEECH_ENDPOINT") ?? speechEndpoint;
            translatorApiKey = Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY") ?? translatorApiKey;
            translatorRegion = Environment.GetEnvironmentVariable("TRANSLATOR_REGION") ?? translatorRegion;

            // Initialize services
            var speechService = new SpeechToTextService(speechEndpoint, speechApiKey);
            var translationService = new TranslationService(translatorApiKey, "https://api.cognitive.microsofttranslator.com/", translatorRegion);

            // Add logging configuration
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });
            var logger = loggerFactory.CreateLogger<Program>();

            logger.LogInformation("Application started.");

            try
            {
                Console.WriteLine("Welcome to the Real-time Speech Translator AI Agent!");
                Console.WriteLine("This application will help you translate spoken language in real-time.");
                Console.WriteLine("Press Enter to start the process.");
                Console.ReadLine();

                logger.LogInformation("Prompting user for source and target languages.");
                Console.WriteLine("Enter the source language (e.g., 'en' for English):");
                string sourceLanguage = Console.ReadLine();

                Console.WriteLine("Enter the target language (e.g., 'es' for Spanish):");
                string targetLanguage = Console.ReadLine();

                logger.LogInformation("Starting speech-to-text and translation process.");
                Console.WriteLine("Start speaking. Press Enter to stop.");

                var speechStream = speechService.GetSpeechStreamAsync(sourceLanguage, targetLanguage);

                await foreach (var recognizedText in speechStream)
                {
                    logger.LogInformation($"Recognized text: {recognizedText}");
                    Console.WriteLine($"Final Recognized: {recognizedText}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during execution.");
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                logger.LogInformation("Application ended.");
            }
        }
    }
}
