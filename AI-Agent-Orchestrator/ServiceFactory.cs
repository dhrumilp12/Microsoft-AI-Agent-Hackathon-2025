using Microsoft.Extensions.Configuration;
using VocabularyBank.Services;

namespace AI_Agent_Orchestrator
{
    public static class ServiceFactory
    {
        public static VocabularyExtractorService CreateVocabularyExtractorService(IConfiguration configuration)
        {
            // Assuming AzureOpenAIService is properly initialized elsewhere
            var openAIService = new AzureOpenAIService(configuration);
            return new VocabularyExtractorService(configuration, openAIService);
        }
    }
}
