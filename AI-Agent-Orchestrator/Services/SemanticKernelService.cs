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
            
            _logger.LogInformation("Semantic Kernel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Semantic Kernel");
        }
    }
    
    public async Task<List<AgentInfo>> FindRelevantAgentsAsync(List<AgentInfo> allAgents, string userQuery)
    {
        if (_kernel == null || string.IsNullOrWhiteSpace(userQuery))
        {
            return allAgents;
        }
        
        try
        {
            // Without the memory system, we'll do a simple keyword matching
            var relevantAgents = new List<AgentInfo>();
            
            // Create a basic prompt function to identify relevant agents
            var promptText = $@"User query: ""{userQuery}""

Given the user's query above, which of the following AI agents would be most relevant? 
Return only the numbers of the most relevant agents (up to 3), separated by commas.

Available agents:
{string.Join("\n", allAgents.Select((a, i) => $"{i+1}. {a.Name}: {a.Description} Keywords: {string.Join(", ", a.Keywords ?? new[] {""})}"))}";

            // Create the function to call OpenAI
            //var functionParams = new OpenAIPromptExecutionSettings {
            //    MaxTokens = 100,
            //    Temperature = 0.0
            //};
            
            var function = _kernel.CreateFunctionFromPrompt(promptText);

            // Invoke the function
            var result = await _kernel.InvokeAsync(function);
            var response = result.GetValue<string>() ?? string.Empty;
            
            // Parse the response to get relevant agent numbers
            var agentNumbers = ParseAgentNumbers(response, allAgents.Count);
            
            // Get the relevant agents
            foreach (var num in agentNumbers)
            {
                if (num > 0 && num <= allAgents.Count)
                {
                    relevantAgents.Add(allAgents[num - 1]);
                }
            }
            
            return relevantAgents.Count > 0 ? relevantAgents : allAgents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding relevant agents with Semantic Kernel");
            return allAgents;
        }
    }
    
    private List<int> ParseAgentNumbers(string response, int maxAgentCount)
    {
        var result = new List<int>();
        
        // Extract numbers from the response
        var numberMatches = System.Text.RegularExpressions.Regex.Matches(response, @"\d+");
        
        foreach (System.Text.RegularExpressions.Match match in numberMatches)
        {
            if (int.TryParse(match.Value, out int agentNumber) && 
                agentNumber > 0 && 
                agentNumber <= maxAgentCount && 
                !result.Contains(agentNumber))
            {
                result.Add(agentNumber);
                
                // Limit to top 3
                if (result.Count >= 3)
                    break;
            }
        }
        
        return result;
    }
}
