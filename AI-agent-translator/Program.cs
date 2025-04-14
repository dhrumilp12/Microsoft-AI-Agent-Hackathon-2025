using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.CognitiveServices.Speech;
using Newtonsoft.Json;
using dotenv.net;
using Azure.AI.Translation.Text;

class Program
{
    static async Task Main(string[] args)
    {
        DotEnv.Load();

        string speechKey = Environment.GetEnvironmentVariable("SPEECH_API_KEY");
        string speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
        string translatorKey = Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY");
        string translatorEndpoint = "https://api.cognitive.microsofttranslator.com";

        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        using var recognizer = new SpeechRecognizer(speechConfig);

        Console.WriteLine("Speak into your microphone.");

        var speechRecognitionResult = await recognizer.RecognizeOnceAsync();

        if (speechRecognitionResult.Reason == ResultReason.RecognizedSpeech)
        {
            Console.WriteLine($"Recognized: {speechRecognitionResult.Text}");

            Console.WriteLine("Enter the source language (e.g., 'en' for English):");
            string sourceLanguage = Console.ReadLine();

            Console.WriteLine("Enter the target language (e.g., 'es' for Spanish):");
            string targetLanguage = Console.ReadLine();

            if (sourceLanguage.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Source and target languages are the same. Translation is not necessary.");
                return;
            }

            var client = new TextTranslationClient(new Uri(translatorEndpoint), new AzureKeyCredential(translatorKey));
            var response = await client.TranslateAsync(sourceLanguage, new[] { targetLanguage }, new[] { speechRecognitionResult.Text });

            foreach (var translation in response.Value[0].Translations)
            {
                Console.WriteLine($"Translated: {translation.Text}");
            }
        }
        else
        {
            Console.WriteLine("Speech could not be recognized.");
        }
    }
}
