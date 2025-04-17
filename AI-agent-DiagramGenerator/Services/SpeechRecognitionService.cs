using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text; // Add this missing using directive for StringBuilder

namespace DiagramGenerator.Services
{
    public class SpeechRecognitionService : ISpeechRecognitionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SpeechRecognitionService> _logger;
        private SpeechRecognizer? _recognizer;
        private readonly StringBuilder _transcriptBuilder = new();
        private TaskCompletionSource<int> _stopRecognition = new();

        public SpeechRecognitionService(IConfiguration configuration, ILogger<SpeechRecognitionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartContinuousRecognitionAsync()
        {
            // Get API keys from environment variables (loaded from .env)
            var speechKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            var speechRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");

            if (string.IsNullOrEmpty(speechKey) || string.IsNullOrEmpty(speechRegion))
            {
                _logger.LogWarning("Environment variables not found. Falling back to configuration values.");
                
                // Fallback to configuration if environment variables are not set
                speechKey = _configuration["Azure:SpeechService:Key"];
                speechRegion = _configuration["Azure:SpeechService:Region"];

                if (string.IsNullOrEmpty(speechKey) || string.IsNullOrEmpty(speechRegion))
                {
                    throw new InvalidOperationException("Speech service configuration is missing. Please check your .env file or appsettings.json.");
                }
            }

            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();

            _recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            _transcriptBuilder.Clear();
            _stopRecognition = new TaskCompletionSource<int>();

            _recognizer.Recognized += (s, e) => {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    _transcriptBuilder.AppendLine(e.Result.Text);
                    Console.WriteLine($"RECOGNIZED: {e.Result.Text}");
                }
            };

            _recognizer.SessionStopped += (s, e) => {
                _stopRecognition.TrySetResult(0);
            };

            await _recognizer.StartContinuousRecognitionAsync();
        }

        public async Task<string> StopContinuousRecognitionAsync()
        {
            if (_recognizer == null)
            {
                return string.Empty;
            }

            await _recognizer.StopContinuousRecognitionAsync();
            await _stopRecognition.Task;
            
            var transcript = _transcriptBuilder.ToString();
            _logger.LogInformation($"Recognition stopped. Transcript length: {transcript.Length} characters");
            
            return transcript;
        }
    }
}
