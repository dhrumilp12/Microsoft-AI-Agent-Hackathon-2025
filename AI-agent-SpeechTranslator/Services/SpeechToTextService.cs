using Microsoft.CognitiveServices.Speech;
using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SpeechTranslator.Services
{
    public class SpeechToTextService
    {
        private readonly SpeechConfig _speechConfig;
        private readonly TranslationService _translationService;
        private readonly ILogger _logger;

        public SpeechToTextService(string speechEndpoint, string speechKey, ILogger logger)
        {
            _speechConfig = SpeechConfig.FromEndpoint(new Uri(speechEndpoint), speechKey);
            _translationService = new TranslationService(
                Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY"),
                Environment.GetEnvironmentVariable("TRANSLATOR_ENDPOINT"),
                Environment.GetEnvironmentVariable("TRANSLATOR_REGION"),
                logger
            );
            _logger = logger;
        }

        /// <summary>
        /// Gets the SpeechConfig object.
        /// </summary>
        public SpeechConfig GetSpeechConfig()
        {
            return _speechConfig;
        }

        /// <summary>
        /// Asynchronously gets the speech stream and translates it (if necessary).
        /// </summary>
        /// <param name="sourceLanguage">The source language code.</param>
        /// <param name="targetLanguage">The target language code.</param>
        /// <returns>An async enumerable of recognized speech.</returns>
        public async IAsyncEnumerable<string> GetSpeechStreamAsync(string sourceLanguage, string targetLanguage)
        {
            var speechRecognizer = new SpeechRecognizer(_speechConfig);

            var recognizedTexts = new Queue<string>();

            // Invoke the translation service for interim results
            speechRecognizer.Recognizing += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    Console.WriteLine($"Interim Recognized: {e.Result.Text}");

                    var translationStream = _translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, TextStream.GetSingleTextStream(e.Result.Text));
                    await foreach (var translatedText in translationStream)
                    {
                        Console.WriteLine($"Translated (Interim): {translatedText}");
                    }
                }
            };

            speechRecognizer.Recognized += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    var translationStream = _translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, TextStream.GetSingleTextStream(e.Result.Text));
                    await foreach (var translatedText in translationStream)
                    {
                        Console.WriteLine($"Translated (Final): {translatedText}");
                        recognizedTexts.Enqueue(e.Result.Text);
                    }
                }
            };

            await speechRecognizer.StartContinuousRecognitionAsync();

            while (!Console.KeyAvailable || Console.ReadKey(true).Key != ConsoleKey.Enter)
            {
                while (recognizedTexts.Count > 0)
                {
                    yield return recognizedTexts.Dequeue();
                }

                await Task.Delay(30); // Allow recognition to continue
            }

            await speechRecognizer.StopContinuousRecognitionAsync();

            yield break;
        }
    }
}