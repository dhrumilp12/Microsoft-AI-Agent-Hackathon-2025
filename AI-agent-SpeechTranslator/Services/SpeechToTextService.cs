using Microsoft.CognitiveServices.Speech;
using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Collections.Generic;

namespace SpeechTranslator.Services
{
    public class SpeechToTextService
    {
        private readonly SpeechConfig _speechConfig;

        public SpeechToTextService(string speechKey, string speechRegion)
        {
            _speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        }

        public async Task<string> ConvertSpeechToTextAsync()
        {
            using var recognizer = new SpeechRecognizer(_speechConfig);

            Console.WriteLine("Speak into your microphone.");
            var result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                return result.Text;
            }

            throw new Exception("Speech could not be recognized.");
        }

        public async Task<string> ConvertSpeechToTextAsync(string audioFilePath)
        {
            using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);
            using var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);

            Console.WriteLine("Processing audio file...");
            var result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                return result.Text;
            }

            throw new Exception("Speech could not be recognized from the audio file.");
        }

        public async Task<string> ConvertSpeechToTextFromVideoAsync(string videoFilePath)
        {
            // Extract audio from video file (placeholder for actual implementation)
            string extractedAudioPath = ExtractAudioFromVideo(videoFilePath);

            // Use the existing audio file method
            return await ConvertSpeechToTextAsync(extractedAudioPath);
        }

        private string ExtractAudioFromVideo(string videoFilePath)
        {
            string audioFilePath = Path.ChangeExtension(videoFilePath, ".wav");

            // Construct the FFmpeg command
            string ffmpegCommand = $"ffmpeg -i \"{videoFilePath}\" -q:a 0 -map a \"{audioFilePath}\" -y";

            // Execute the command
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {ffmpegCommand}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception("Failed to extract audio from video. Ensure FFmpeg is installed and accessible from the command line.");
            }

            return audioFilePath;
        }

        public async IAsyncEnumerable<string> GetSpeechStreamAsync(string sourceLanguage, string targetLanguage)
        {
            var speechRecognizer = new SpeechRecognizer(_speechConfig);

            var recognizedTexts = new Queue<string>();

            // Invoke the translation service for interim results
            var translationService = new TranslationService(
                Environment.GetEnvironmentVariable("TRANSLATOR_API_KEY"),
                "https://api.cognitive.microsofttranslator.com/",
                Environment.GetEnvironmentVariable("TRANSLATOR_REGION")
            );

            speechRecognizer.Recognizing += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    Console.WriteLine($"Interim Recognized: {e.Result.Text}");

                    var translationStream = translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, GetSingleTextStream(e.Result.Text));
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
                    var translationStream = translationService.TranslateTextStreamAsync(sourceLanguage, targetLanguage, GetSingleTextStream(e.Result.Text));
                    await foreach (var translatedText in translationStream)
                    {
                        Console.WriteLine($"Translated (Final): {translatedText}");
                        recognizedTexts.Enqueue(translatedText);
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