using DiagramGenerator.Models;
using DiagramGenerator.Services;
using Microsoft.Extensions.Logging;

namespace DiagramGenerator
{
    public class DiagramManager
    {
        private readonly ISpeechRecognitionService _speechRecognition;
        private readonly IConceptExtractorService _conceptExtractor;
        private readonly IDiagramGeneratorService _diagramGenerator;
        private readonly IWhiteboardIntegrationService _whiteboardIntegration;
        private readonly IDiagramInteractionService _diagramInteraction;
        private readonly ILogger<DiagramManager> _logger;
        private Diagram? _currentDiagram;

        public DiagramManager(
            ISpeechRecognitionService speechRecognition,
            IConceptExtractorService conceptExtractor,
            IDiagramGeneratorService diagramGenerator,
            IWhiteboardIntegrationService whiteboardIntegration,
            IDiagramInteractionService diagramInteraction,
            ILogger<DiagramManager> logger)
        {
            _speechRecognition = speechRecognition;
            _conceptExtractor = conceptExtractor;
            _diagramGenerator = diagramGenerator;
            _whiteboardIntegration = whiteboardIntegration;
            _diagramInteraction = diagramInteraction;
            _logger = logger;
        }

        public async Task StartListeningAndGenerateDiagramAsync()
        {
            _logger.LogInformation("Starting speech recognition...");
            Console.WriteLine("Listening to lecture... Press Enter to stop.");
            
            var speechTask = _speechRecognition.StartContinuousRecognitionAsync();
            Console.ReadLine();
            var transcript = await _speechRecognition.StopContinuousRecognitionAsync();
            
            await ProcessTranscriptAsync(transcript);
        }

        public async Task GenerateDiagramFromFileAsync(string filePath)
        {
            try
            {
                var transcript = await File.ReadAllTextAsync(filePath);
                await ProcessTranscriptAsync(transcript);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading file at {filePath}");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task ProcessTranscriptAsync(string transcript)
        {
            _logger.LogInformation("Processing transcript...");
            Console.WriteLine("Extracting key concepts from transcript...");
            
            var concepts = await _conceptExtractor.ExtractConceptsAsync(transcript);
            
            Console.WriteLine($"Found {concepts.Count} key concepts.");
            Console.WriteLine("Generating diagram...");
            
            _currentDiagram = await _diagramGenerator.GenerateDiagramAsync(concepts, transcript);
            
            Console.WriteLine("Diagram generated successfully!");
            Console.WriteLine($"Diagram type: {_currentDiagram.Type}");
            
            await _whiteboardIntegration.ShareDiagramAsync(_currentDiagram);
            
            Console.WriteLine("Diagram is now available on Microsoft Whiteboard.");
            Console.WriteLine("You can now interact with the diagram.");
        }

        public async Task InteractWithDiagramAsync()
        {
            if (_currentDiagram == null)
            {
                Console.WriteLine("No diagram available. Please generate a diagram first.");
                return;
            }

            await _diagramInteraction.StartInteractionAsync(_currentDiagram);
        }
    }
}
