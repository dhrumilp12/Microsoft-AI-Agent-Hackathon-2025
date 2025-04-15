using System;
using System.Threading.Tasks;
using dotenv.net;
using SpeechTranslator.Services;

namespace SpeechTranslator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Explicitly specify the path to the .env file
            DotEnv.Load(new DotEnvOptions(envFilePaths: ["./.env"]));

            string speechKey = Environment.GetEnvironmentVariable("SPEECH_API_KEY");
            string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
            string translatorKey = Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY");
            string translatorRegion = Environment.GetEnvironmentVariable("TRANSLATOR_REGION");
            string translatorEndpoint = "https://api.cognitive.microsofttranslator.com/";

            var speechService = new SpeechToTextService(speechKey, speechRegion);
            var translationService = new TranslationService(translatorKey, translatorEndpoint, translatorRegion);

            try
            {
                Console.WriteLine("Enter the source language (e.g., 'en' for English):");
                string sourceLanguage = Console.ReadLine();

                Console.WriteLine("Enter the target language (e.g., 'es' for Spanish):");
                string targetLanguage = Console.ReadLine();

                Console.WriteLine("Start speaking. Press Enter to stop.");

                var speechStream = speechService.GetSpeechStreamAsync(); // Assuming this method streams speech-to-text
                await foreach (var recognizedText in speechStream)
                {
                    Console.WriteLine($"Recognized: {recognizedText}");

                    var translationStream = translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, speechStream);
                    await foreach (var translatedText in translationStream)
                    {
                        Console.WriteLine($"Translated: {translatedText}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}
