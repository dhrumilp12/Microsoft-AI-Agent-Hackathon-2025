using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DiagramGenerator.Services
{
    public class ConceptExtractorService : IConceptExtractorService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConceptExtractorService> _logger;
        private readonly AzureOpenAIClientService _azureOpenAIClient;

        public ConceptExtractorService(
            IConfiguration configuration, 
            ILogger<ConceptExtractorService> logger,
            AzureOpenAIClientService azureOpenAIClient)
        {
            _configuration = configuration;
            _logger = logger;
            _azureOpenAIClient = azureOpenAIClient;
        }

        public async Task<List<string>> ExtractConceptsAsync(string transcript)
        {
            _logger.LogInformation("Extracting concepts using direct HTTP approach...");
            
            try
            {
                // System prompt that instructs the model on its role
                string systemPrompt = "You are an expert at extracting key concepts from educational lectures. Extract the main concepts that would be useful for creating a visual diagram.";
                
                // User prompt that contains the actual content to process
                string userPrompt = $"Extract the key concepts from this lecture transcript. Return them as a JSON array of strings. Transcript: {transcript}";
                
                // Get response from Azure OpenAI
                var conceptsJson = await _azureOpenAIClient.GetChatCompletionAsync(systemPrompt, userPrompt, 0.0, 800);
                
                // If response starts with error, log it and return empty list
                if (conceptsJson.StartsWith("Error:"))
                {
                    _logger.LogError(conceptsJson);
                    return new List<string>();
                }
                
                // Parse the JSON response to extract the array of concepts
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(conceptsJson) ?? new List<string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"Failed to parse concepts JSON: {conceptsJson}");
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting concepts from transcript");
                return new List<string>();
            }
        }
    }
}
