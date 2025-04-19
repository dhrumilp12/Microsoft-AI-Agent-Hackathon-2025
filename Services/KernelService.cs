using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace AI_Agent_Orchestrator.Services
{
    public class KernelService
    {
        private readonly Kernel _kernel;
        private readonly IConfiguration _configuration;

        public KernelService(IConfiguration configuration)
        {
            _configuration = configuration;
            _kernel = Kernel.CreateBuilder().Build();
        }

        public Kernel GetKernel() => _kernel;

        public string GetSpeechKey() => _configuration["Azure:SpeechKey"];

        public string GetSpeechEndpoint() => _configuration["Azure:SpeechEndpoint"];

        public string GetTranslatorKey() => _configuration["Azure:TranslatorKey"];

        public string GetTranslatorEndpoint() => _configuration["Azure:TranslatorEndpoint"];

        public string GetTranslatorRegion() => _configuration["Azure:TranslatorRegion"];

        public string GetOpenAIAPIKey() => _configuration["OpenAI:ApiKey"];

        public string GetOpenAIEndpoint() => _configuration["OpenAI:Endpoint"];
    }
}