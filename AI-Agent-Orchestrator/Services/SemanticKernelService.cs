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

    public async Task<List<AgentWorkflow>> FindRelevantWorkflowsAsync(List<AgentWorkflow> allWorkflows, string userQuery)
    {
        if (_kernel == null || string.IsNullOrWhiteSpace(userQuery) || allWorkflows.Count == 0)
        {
            return allWorkflows;
        }
        
        try
        {
            // First do a direct keyword match
            var keywordMatches = new List<AgentWorkflow>();
            foreach (var workflow in allWorkflows)
            {
                // Check if the query contains workflow keywords
                if (workflow.Keywords.Any(k => userQuery.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    keywordMatches.Add(workflow);
                }
                
                // Special case for speech translation to vocabulary
                if ((userQuery.Contains("speech", StringComparison.OrdinalIgnoreCase) || 
                     userQuery.Contains("translate", StringComparison.OrdinalIgnoreCase)) && 
                    (userQuery.Contains("vocabulary", StringComparison.OrdinalIgnoreCase) || 
                     userQuery.Contains("flashcard", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!keywordMatches.Contains(workflow) && 
                        workflow.Name.Contains("Audio Translation to Vocabulary", StringComparison.OrdinalIgnoreCase))
                    {
                        keywordMatches.Add(workflow);
                    }
                }
            }
            
            // If we found direct matches, return those
            if (keywordMatches.Count > 0)
            {
                _logger.LogInformation($"Found {keywordMatches.Count} direct keyword matches for workflow query");
                return keywordMatches;
            }
            
            // If no direct matches, use the LLM
            var promptText = $@"User query: ""{userQuery}""

Given the user's query above, which of the following workflows would be most relevant? 
Return only the numbers of the most relevant workflows (up to 2), separated by commas.

Available workflows:
{string.Join("\n", allWorkflows.Select((w, i) => $"{i+1}. {w.Name}: {w.Description} Keywords: {string.Join(", ", w.Keywords ?? new[] {""})}"))}";

            // Create the function to call OpenAI
            var function = _kernel.CreateFunctionFromPrompt(promptText);

            // Invoke the function
            var result = await _kernel.InvokeAsync(function);
            var response = result.GetValue<string>() ?? string.Empty;
            
            _logger.LogInformation($"LLM response for workflow matching: {response}");
            
            // Parse the response to get relevant workflow numbers
            var workflowNumbers = ParseResponseNumbers(response, allWorkflows.Count);
            
            // Get the relevant workflows
            var relevantWorkflows = new List<AgentWorkflow>();
            foreach (var num in workflowNumbers)
            {
                if (num > 0 && num <= allWorkflows.Count)
                {
                    relevantWorkflows.Add(allWorkflows[num - 1]);
                }
            }
            
            return relevantWorkflows.Count > 0 ? relevantWorkflows : allWorkflows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding relevant workflows with Semantic Kernel");
            return allWorkflows;
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

    private List<int> ParseResponseNumbers(string response, int maxCount)
    {
        var result = new List<int>();
        
        // Extract numbers from the response
        var numberMatches = System.Text.RegularExpressions.Regex.Matches(response, @"\d+");
        
        foreach (System.Text.RegularExpressions.Match match in numberMatches)
        {
            if (int.TryParse(match.Value, out int number) && 
                number > 0 && 
                number <= maxCount && 
                !result.Contains(number))
            {
                result.Add(number);
                
                // Limit to top 2
                if (result.Count >= 2)
                    break;
            }
        }
        
        return result;
    }

    public async Task<string> ChatWithLLMAsync(string userQuery)
    {
        if (_kernel == null)
        {
            _logger.LogWarning("Semantic Kernel is not initialized. Cannot engage in chat.");
            return "Sorry, the chat service is currently unavailable.";
        }

        try
        {
            // Create a prompt for the LLM
            var promptText = $"The user has asked: \"{userQuery}\". Please provide a helpful and concise response.";

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
