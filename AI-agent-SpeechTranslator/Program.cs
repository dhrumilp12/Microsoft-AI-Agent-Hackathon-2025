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

            var speechService = new SpeechToTextService(speechKey, speechRegion);

            try
            {
                Console.WriteLine("Enter the source language (e.g., 'en' for English):");
                string sourceLanguage = Console.ReadLine();

                Console.WriteLine("Enter the target language (e.g., 'es' for Spanish):");
                string targetLanguage = Console.ReadLine();

                Console.WriteLine("Start speaking. Press Enter to stop.");

                var speechStream = speechService.GetSpeechStreamAsync(sourceLanguage, targetLanguage);

                await foreach (var recognizedText in speechStream)
                {
                    Console.WriteLine($"Final Recognized: {recognizedText}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}
