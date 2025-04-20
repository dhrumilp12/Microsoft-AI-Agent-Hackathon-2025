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
            // Load .env variables
            Env.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env")));
            
            // Load configuration from appsettings.json
            //var configuration = new ConfigurationBuilder()
            //    .SetBasePath(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")))
            //    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            //    .AddEnvironmentVariables()
            //    .Build();

            //// Retrieve Speech Service configuration
            //string speechApiKey = configuration["SpeechService:ApiKey"];
            //string speechEndpoint = configuration["SpeechService:Endpoint"];

            //// Retrieve Translator Service configuration
            //string translatorApiKey = configuration["TranslatorService:ApiKey"];
            //string translatorRegion = configuration["TranslatorService:Region"];

            // Override with .env variables if available
            string speechApiKey = Environment.GetEnvironmentVariable("SPEECH_API_KEY");
            string speechEndpoint = Environment.GetEnvironmentVariable("SPEECH_ENDPOINT");
            string translatorApiKey = Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY");
            string translatorRegion = Environment.GetEnvironmentVariable("TRANSLATOR_REGION");

            // Initialize services
            var speechService = new SpeechToTextService(speechEndpoint, speechApiKey);

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

                Console.WriteLine("Choose an input method:");
                Console.WriteLine("1. Speak into the microphone");
                Console.WriteLine("2. Upload a video lecture");
                Console.WriteLine("3. Upload an audio file");

                string choice = Console.ReadLine();

                var recognizedText = string.Empty; 

                switch (choice)
                {
                    case "1":
                        Console.WriteLine("Start speaking. Press Enter to stop.");

                        recognizedText = await speechService.ConvertSpeechToTextAsync(sourceLanguage: sourceLanguage, targetLanguage: targetLanguage);
                        break;

                    case "2":
                        Console.WriteLine("You chose to upload a video lecture.");
                        Console.WriteLine("Please provide the path to the video file:");
                        string videoPath = Console.ReadLine();
                        // Process the video file (e.g., extract audio)

                        recognizedText = await speechService.ConvertSpeechToTextFromVideoAsync(videoPath, sourceLanguage, targetLanguage);
                        logger.LogInformation($"Recognized text from video: {recognizedText}");
                        Console.WriteLine($"Final Recognized from Video: {recognizedText}");
                        break;

                    case "3":
                        Console.WriteLine("You chose to upload an audio file.");
                        Console.WriteLine("Please provide the path to the audio file:");
                        string audioPath = Console.ReadLine();
                        // Process the audio file

                        recognizedText = await speechService.ConvertSpeechToTextAsync(audioPath, sourceLanguage, targetLanguage);
                        logger.LogInformation($"Recognized text from audio: {recognizedText}");
                        Console.WriteLine($"Final Recognized from Audio: {recognizedText}");
                        break;

                    default:
                        Console.WriteLine("Invalid choice. Please restart the application and try again.");
                        return;
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
