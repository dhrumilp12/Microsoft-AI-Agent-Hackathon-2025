using AI_Agent_Orchestrator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AI_Agent_Orchestrator.Services;

public class SemanticKernelService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticKernelService> _logger;
    private Kernel? _kernel;

    public SemanticKernelService(IConfiguration configuration, ILogger<SemanticKernelService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Semantic Kernel");
            
            var endpoint = string.IsNullOrEmpty(_configuration["AzureOpenAI:Endpoint"]) 
                ? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") : _configuration["AzureOpenAI:Endpoint"];
            var apiKey = string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]) 
                ? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") : _configuration["AzureOpenAI:ApiKey"];
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"];
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || 
                string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Semantic Kernel configuration incomplete. Using basic agent selection.");
                return;
            }
            
            // Initialize kernel builder with the updated API
            var builder = Kernel.CreateBuilder();
            
            // Add Azure OpenAI chat completion
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                endpoint: endpoint,
                apiKey: apiKey);
            
            _kernel = builder.Build();
            
            // Add a simple await to make this properly async
            await Task.Delay(1); // Minimal delay to make this truly async
            
            _logger.LogInformation("Semantic Kernel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Semantic Kernel");
        }
    }

    public async Task<string> ChatWithLLMAsync(string userQuery, string conversationHistory)
    {
        if (_kernel == null)
        {
            _logger.LogWarning("Semantic Kernel is not initialized. Cannot engage in chat.");
            return "Sorry, the chat service is currently unavailable.";
        }

        try
        {
            // Create a prompt for the LLM with the conversation history
            var promptText = string.IsNullOrWhiteSpace(conversationHistory)
                ? $"The following is a conversation between a user and an AI assistant. The user just started the conversation:\nUser: {userQuery}\n"
                : $"The following is a conversation between a user and an AI assistant:\n{conversationHistory}\n The user has just entered {userQuery}. Please provide a clear and concise response to the query.\n";

            // Create the function to call OpenAI
            var function = _kernel.CreateFunctionFromPrompt(promptText);

            // Invoke the function
            var result = await _kernel.InvokeAsync(function);
            return result.GetValue<string>() ?? "No response from the LLM.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chat with LLM");
            return "An error occurred while trying to chat with the LLM.";
        }
    }
}
