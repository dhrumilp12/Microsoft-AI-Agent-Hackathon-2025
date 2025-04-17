using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiagramGenerator.Services
{
    public class AzureOpenAIClientService
    {
        private readonly HttpClient _httpClient;
        private readonly string _deploymentName;
        private readonly string _apiVersion;
        private readonly ILogger<AzureOpenAIClientService> _logger;
        private readonly int _maxRetries = 3;
        private readonly int _initialRetryDelay = 2000;

        public AzureOpenAIClientService(IConfiguration configuration, ILogger<AzureOpenAIClientService> logger)
        {
            _logger = logger;
            
            // Get API keys from environment variables
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
            _deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(_deploymentName))
            {
                _logger.LogWarning("Environment variables not found. Falling back to configuration values.");
                
                // Fallback to configuration if environment variables are not set
                endpoint = configuration["Azure:OpenAI:Endpoint"];
                apiKey = configuration["Azure:OpenAI:Key"];
                _deploymentName = configuration["Azure:OpenAI:DeploymentName"];
            }

            // Set API version to required value for newer models
            _apiVersion = "2024-12-01-preview";
            
            _logger.LogInformation($"Using Azure OpenAI endpoint: {endpoint}");
            _logger.LogInformation($"Using deployment name: {_deploymentName}");
            _logger.LogInformation($"Using API version: {_apiVersion}");
            
            // Remove trailing slash if it exists
            if (endpoint?.EndsWith('/') == true)
            {
                endpoint = endpoint.TrimEnd('/');
            }
            
            // Initialize HTTP client
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(endpoint)
            };
            
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        public async Task<string> GetChatCompletionAsync(string systemPrompt, string userPrompt, double temperature = 0.3, int maxTokens = 2000)
        {
            return await ExecuteWithRetryAsync(async () => 
            {
                return await ExecuteChatCompletionRequestAsync(systemPrompt, userPrompt, temperature, maxTokens);
            }, _maxRetries, _initialRetryDelay);
        }

        private async Task<string> ExecuteChatCompletionRequestAsync(string systemPrompt, string userPrompt, double temperature, int maxTokens)
        {
            try
            {
                // Construct the API URL with deployment name and API version
                var requestUrl = $"openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}";
                
                _logger.LogInformation($"Sending request to: {_httpClient.BaseAddress}{requestUrl}");
                
                // Create the request body - removed temperature parameter as it's not supported
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_completion_tokens = maxTokens
                };
                
                // Serialize the request body to JSON
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var stringContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug($"Request payload: {jsonContent}");
                
                // Send the request to the API
                var response = await _httpClient.PostAsync(requestUrl, stringContent);
                
                // Read the response content
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Handle API errors
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API Error: {(int)response.StatusCode} {response.StatusCode}");
                    _logger.LogError($"Response details: {responseContent}");
                    throw new HttpRequestException($"Azure OpenAI API returned {(int)response.StatusCode}: {responseContent}");
                }
                
                // Try to extract content even if JSON is malformed
                try {
                    // Parse the response and extract the content
                    var responseJson = JsonSerializer.Deserialize<JsonDocument>(responseContent);
                    string resultContent = responseJson.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    
                    // Handle empty responses
                    if (string.IsNullOrWhiteSpace(resultContent))
                    {
                        // Check the finish reason
                        string finishReason = responseJson.RootElement.GetProperty("choices")[0].TryGetProperty("finish_reason", out var finishReasonElement) ? 
                            finishReasonElement.GetString() : "unknown";
                        
                        _logger.LogWarning($"Received empty content with finish reason: {finishReason}");
                        
                        if (finishReason == "length")
                        {
                            return "Error: Response was cut off due to token limit.";
                        }
                        
                        return "Error: The model returned an empty response.";
                    }
                    
                    return resultContent;
                }
                catch (JsonException ex)
                {
                    _logger.LogError($"JSON parsing error: {ex.Message}");
                    
                    // Try to fix common JSON truncation issues
                    string fixedJson = TryRepairTruncatedJson(responseContent);
                    
                    if (fixedJson != responseContent)
                    {
                        _logger.LogInformation("Attempted to repair truncated JSON");
                        try
                        {
                            // Try parsing the repaired JSON
                            var responseJson = JsonSerializer.Deserialize<JsonDocument>(fixedJson);
                            string resultContent = responseJson.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                            return resultContent;
                        }
                        catch (Exception repairEx)
                        {
                            _logger.LogError($"Failed to parse repaired JSON: {repairEx.Message}");
                        }
                    }
                    
                    // Return content directly if we can extract it using string manipulation
                    string directContent = ExtractContentDirectly(responseContent);
                    if (!string.IsNullOrEmpty(directContent))
                    {
                        _logger.LogInformation("Extracted content directly from response");
                        return directContent;
                    }
                    
                    // If all else fails, return the raw JSON response for client-side handling
                    return responseContent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in API request: {ex.Message}");
                throw;
            }
        }
        
        private string TryRepairTruncatedJson(string json)
        {
            // Quick and simple JSON repair for common truncation issues
            try
            {
                int openBraces = json.Count(c => c == '{');
                int closeBraces = json.Count(c => c == '}');
                int openBrackets = json.Count(c => c == '[');
                int closeBrackets = json.Count(c => c == ']');
                
                // Add missing closing braces and brackets
                if (openBraces > closeBraces)
                {
                    json += new string('}', openBraces - closeBraces);
                }
                
                if (openBrackets > closeBrackets)
                {
                    json += new string(']', openBrackets - closeBrackets);
                }
                
                return json;
            }
            catch
            {
                return json; // Return original if repair fails
            }
        }
        
        private string ExtractContentDirectly(string response)
        {
            try
            {
                // Try to extract content directly using string operations
                const string contentMarker = "\"content\":\"";
                int contentStart = response.IndexOf(contentMarker);
                
                if (contentStart >= 0)
                {
                    contentStart += contentMarker.Length;
                    int contentEnd = response.IndexOf("\"", contentStart);
                    
                    if (contentEnd > contentStart)
                    {
                        return response.Substring(contentStart, contentEnd - contentStart);
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries, int initialDelayMs)
        {
            int retryCount = 0;
            int delay = initialDelayMs;
            
            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    retryCount++;
                    
                    if (retryCount > maxRetries)
                    {
                        _logger.LogError($"Operation failed after {maxRetries} retries: {ex.Message}");
                        throw;
                    }
                    
                    _logger.LogWarning($"Attempt {retryCount} failed: {ex.Message}. Retrying in {delay}ms...");
                    await Task.Delay(delay);
                    
                    // Exponential backoff
                    delay *= 2;
                }
            }
        }
    }
}
