using System;
using System.Threading.Tasks;
using dotenv.net;
using SpeechTranslator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;

namespace SpeechTranslator
{
    class Program
    {
        static async Task Main(string[] args)
        {
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
            DotEnv.Load(new DotEnvOptions(envFilePaths: [".env"]));

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
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n==============================");
                Console.WriteLine("Welcome to the Real-time Speech Translator AI Agent!");
                Console.WriteLine("==============================\n");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("This application will help you translate spoken language in real-time.");
                Console.WriteLine("Press Enter to start the process.");
                Console.ResetColor();
                Console.ReadLine();

                logger.LogInformation("Prompting user for source and target languages.");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n==============================");
                Console.WriteLine("Enter the source and target languages");
                Console.WriteLine("==============================\n");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Enter the source language (e.g., 'en' for English):");
                Console.ResetColor();
                string sourceLanguage = Console.ReadLine();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Enter the target language (e.g., 'es' for Spanish):");
                Console.ResetColor();
                string targetLanguage = Console.ReadLine();

                logger.LogInformation("Starting speech-to-text and translation process.");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n==============================");
                Console.WriteLine("Start speaking. Press Enter to stop.");
                Console.WriteLine("==============================\n");
                Console.ResetColor();

                if(!Directory.Exists("Output"))
                {
                    Directory.CreateDirectory("Output");
                }

                // Create output files for recognized and translated text
                string recognizedTextFilePath = "Output/recognized_transcript.txt";
                string translatedTextFilePath = "Output/translated_transcript.txt";

                using (var recognizedTextWriter = new StreamWriter(recognizedTextFilePath, append: false))
                {
                    using (var translatedTextWriter = new StreamWriter(translatedTextFilePath, append: false))
                    {
                        var speechStream = speechService.GetSpeechStreamAsync(sourceLanguage, targetLanguage);

                        await foreach (var recognizedText in speechStream)
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine("\n[Recognized Speech]");
                            Console.WriteLine($"{recognizedText}");
                            Console.ResetColor();

                            logger.LogInformation($"Recognized text: {recognizedText}");

                            // Write recognized text to file
                            await recognizedTextWriter.WriteLineAsync(recognizedText);

                            var translationStream = translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, TextStream.GetSingleTextStream(recognizedText));
                            await foreach (var translatedText in translationStream)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine("\n[Translated Text]");
                                Console.WriteLine($"{translatedText}");
                                Console.ResetColor();

                                logger.LogInformation($"Translated text: {translatedText}");

                                // Write translated text to file
                                await translatedTextWriter.WriteLineAsync(translatedText);
                            }
                        }
                    }
                }

                // Initialize speech synthesizer
                var synthesizer = new SpeechSynthesizer(speechService.GetSpeechConfig());

                // Read and speak the translated text
                if (File.Exists(translatedTextFilePath))
                {
                    Console.WriteLine("\n[Speaking Translated Text]");
                    var translatedText = await File.ReadAllTextAsync(translatedTextFilePath);
                    await synthesizer.SpeakTextAsync(translatedText);
                }
                else
                {
                    Console.WriteLine("Translated text file not found.");
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
