using Microsoft.CognitiveServices.Speech;
using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Collections.Generic;
using Xabe.FFmpeg;

namespace SpeechTranslator.Services
{
    public class SpeechToTextService
    {
        private readonly SpeechConfig _speechConfig;

        private readonly TranslationService _translationService;

        public SpeechToTextService(string speechEndpoint, string speechKey)
        {
            FFmpeg.SetExecutablesPath(@"C:\Program Files\ffmpeg-master-latest-win64-lgpl\bin");
            _speechConfig = SpeechConfig.FromEndpoint(new Uri(speechEndpoint), speechKey);
            _translationService = new TranslationService(
                Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY"),
                Environment.GetEnvironmentVariable("TRANSLATOR_ENDPOINT"),
                Environment.GetEnvironmentVariable("TRANSLATOR_REGION")
            );
        }

        public async Task<string> ConvertSpeechToTextAsync(string audioFilePath = null, string sourceLanguage = "en", string targetLanguage = "es")
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            // Set the source and target languages for the speech recognition
            var recognizedText = string.Empty;
            var audioInput = AudioConfig.FromDefaultMicrophoneInput();

            if (!string.IsNullOrEmpty(audioFilePath))
            {
                audioFilePath = audioFilePath.Replace('"', ' ').Trim();
                audioInput = AudioConfig.FromWavFileInput(audioFilePath);
            }


            using var recognizer = new SpeechRecognizer(_speechConfig, audioInput);
            await foreach (var text in GetSpeechStreamAsync(recognizer, sourceLanguage, targetLanguage))
            {
                recognizedText += text + " ";
            }

            return recognizedText.Trim();
        }

        public async Task<string> ConvertSpeechToTextFromVideoAsync(string videoFilePath, string sourceLanguage, string targetLanguage)
        {
            // Extract audio from video file (placeholder for actual implementation)
            string extractedAudioPath = await ExtractAudioFromVideo(videoFilePath);

            // Use the existing audio file method
            return await ConvertSpeechToTextAsync(extractedAudioPath, sourceLanguage, targetLanguage);
        }

        private async Task<string> ExtractAudioFromVideo(string videoFilePath)
        {
            if (!videoFilePath.StartsWith("\""))
            {
                videoFilePath = "\"" + videoFilePath;
            }

            if (!videoFilePath.EndsWith("\""))
            {
                videoFilePath += "\"";
            }

            string audioFilePath = Path.ChangeExtension(videoFilePath, ".wav");

            if (!audioFilePath.EndsWith("\""))
                audioFilePath += "\"";

            var conversion = await FFmpeg.Conversions.FromSnippet.ExtractAudio(videoFilePath, audioFilePath);
            await conversion.Start();

            // Ensure the audio file was created
            if (!File.Exists(audioFilePath.Trim('"')))
            {
                throw new FileNotFoundException("The audio file was not generated. Please check the FFmpeg command and input file.");
            }

            return audioFilePath;
        }

        public async IAsyncEnumerable<string> GetSpeechStreamAsync(SpeechRecognizer speechRecognizer, string sourceLanguage, string targetLanguage)
        {
            var speechSynthesizer = new SpeechSynthesizer(_speechConfig);

            var recognizedTexts = new Queue<string>();

            // Invoke the translation service for interim results
            speechRecognizer.Recognizing += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    Console.WriteLine($"Interim Recognized: {e.Result.Text}");

                    var translationStream = _translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, GetSingleTextStream(e.Result.Text));
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
                    var translationStream = _translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, GetSingleTextStream(e.Result.Text));
                    await foreach (var translatedText in translationStream)
                    {
                        Console.WriteLine($"Translated (Final): {translatedText}");
                        recognizedTexts.Enqueue(translatedText);

                        // Speak the translated text
                        await speechSynthesizer.SpeakTextAsync(translatedText);
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

            static async IAsyncEnumerable<string> GetSingleTextStream(string text)
            {
                yield return text;
                await Task.CompletedTask;
            }
        }
    }
}