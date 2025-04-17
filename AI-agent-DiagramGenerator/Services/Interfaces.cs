using DiagramGenerator.Models;

namespace DiagramGenerator.Services
{
    public interface ISpeechRecognitionService
    {
        Task StartContinuousRecognitionAsync();
        Task<string> StopContinuousRecognitionAsync();
    }

    public interface IConceptExtractorService
    {
        Task<List<string>> ExtractConceptsAsync(string transcript);
    }

    public interface IDiagramGeneratorService
    {
        Task<Diagram> GenerateDiagramAsync(List<string> concepts, string transcript);
        Task<string> GenerateDiagramMarkupAsync(Diagram diagram);
    }

    public interface IWhiteboardIntegrationService
    {
        Task<string> ShareDiagramAsync(Diagram diagram);
        Task<bool> UpdateDiagramAsync(Diagram diagram);
    }

    public interface IDiagramInteractionService
    {
        Task StartInteractionAsync(Diagram diagram);
        Task<Diagram> BreakdownElementAsync(Diagram diagram, string elementId);
        Task<Diagram> ModifyDiagramAsync(Diagram diagram, string command);
    }
}
