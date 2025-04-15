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
            DotEnv.Load();

            string speechKey = Environment.GetEnvironmentVariable("SPEECH_API_KEY");
            string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
            string translatorKey = Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY");
            string translatorEndpoint = "https://api.cognitive.microsofttranslator.com";

            var speechService = new SpeechToTextService(speechKey, speechRegion);
            var translationService = new TranslationService(translatorKey, translatorEndpoint);

            try
            {
                string recognizedText = await speechService.ConvertSpeechToTextAsync();
                Console.WriteLine($"Recognized: {recognizedText}");

                Console.WriteLine("Enter the source language (e.g., 'en' for English):");
                string sourceLanguage = Console.ReadLine();

                Console.WriteLine("Enter the target language (e.g., 'es' for Spanish):");
                string targetLanguage = Console.ReadLine();

                string translatedText = await translationService.TranslateTextAsync(sourceLanguage, targetLanguage, recognizedText);
                Console.WriteLine($"Translated: {translatedText}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}
