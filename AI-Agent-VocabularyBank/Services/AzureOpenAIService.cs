using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace VocabularyBank.Services
{
    /// <summary>
    /// Service responsible for communicating with Azure OpenAI API.
    /// This service handles API requests for text completions and chat completions,
    /// providing abstraction over the raw HTTP requests.
    /// </summary>
    public class AzureOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _deploymentName;
        private readonly string _apiVersion;
        private readonly bool _debug;
        
        /// <summary>
        /// Initializes a new instance of AzureOpenAIService with configuration settings.
        /// </summary>
        /// <param name="configuration">Application configuration containing Azure OpenAI settings</param>
        public AzureOpenAIService(IConfiguration configuration)
        {
            string endpoint;
            string apiKey;
            
            // Determine whether to use environment variables or appsettings.json
            bool useEnvVars = configuration.GetSection("AzureOpenAI").GetValue<bool>("UseEnvironmentVariables", false);
            if (useEnvVars)
            {
                // Get credentials from environment variables
                endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                _deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
            }
            else
            {
                // Fall back to appsettings.json
                endpoint = configuration["AzureOpenAI:Endpoint"];
                apiKey = configuration["AzureOpenAI:ApiKey"];
                _deploymentName = configuration["AzureOpenAI:DeploymentName"];
            }
            
            _apiVersion = configuration["AzureOpenAI:ApiVersion"] ?? "2024-12-01-preview";
            _debug = configuration.GetSection("AzureOpenAI").GetValue<bool>("Debug", false);
            
            // Remove trailing slash if it exists for proper URL construction
            if (endpoint?.EndsWith('/') == true)
            {
                endpoint = endpoint.TrimEnd('/');
            }
            
            // Log connection details if in debug mode
            if (_debug)
            {
                Console.WriteLine($"Debug: Using endpoint: {endpoint}");
                Console.WriteLine($"Debug: Using deployment: {_deploymentName}");
                Console.WriteLine($"Debug: Using API version: {_apiVersion}");
            }
            
            // Initialize HTTP client for API communication
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(endpoint)
            };
            
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        }
        
        /// <summary>
        /// Gets a completion from the Azure OpenAI service based on the provided prompt.
        /// </summary>
        /// <param name="prompt">The text prompt to send to the model</param>
        /// <param name="temperature">Controls randomness. Lower is more deterministic.</param>
        /// <param name="maxTokens">Maximum number of tokens to generate</param>
        /// <returns>Generated text from the model</returns>
        public async Task<string> GetCompletionAsync(string prompt, double temperature = 0.3, int maxTokens = 500)
        {
            try
            {
                // Use chat completions API instead of completions API for o3-mini
                return await GetChatCompletionAsync(prompt, temperature, maxTokens);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON parsing error: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets a chat completion from the Azure OpenAI service based on the provided prompt.
        /// This method uses the chat completions API which is required for certain models.
        /// </summary>
        /// <param name="prompt">The text prompt to send to the model</param>
        /// <param name="temperature">Controls randomness. Lower is more deterministic.</param>
        /// <param name="maxTokens">Maximum number of tokens to generate</param>
        /// <returns>Generated text from the model</returns>
        public async Task<string> GetChatCompletionAsync(string prompt, double temperature = 0.3, int maxTokens = 1000)
        {
            try
            {
                // Construct the API URL with deployment name and API version
                var requestUrl = $"openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}";
                
                if (_debug)
                {
                    Console.WriteLine($"Debug: Full request URL: {_httpClient.BaseAddress}{requestUrl}");
                }
                
                // Create the request body with only the supported parameters for the model
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = "You are an AI assistant helping with vocabulary analysis. Provide concise responses focusing only on the key terms." },
                        new { role = "user", content = prompt }
                    },
                    max_completion_tokens = maxTokens  // Model-specific parameter
                };
                
                // Serialize the request body to JSON
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var stringContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                if (_debug)
                {
                    Console.WriteLine($"Debug: Request payload: {jsonContent}");
                }
                
                // Send the request to the API
                var response = await _httpClient.PostAsync(requestUrl, stringContent);
                
                // Read the response content
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Handle API errors
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API Error: {(int)response.StatusCode} {response.StatusCode}");
                    Console.WriteLine($"Response details: {responseContent}");
                    throw new HttpRequestException($"Azure OpenAI API returned {(int)response.StatusCode}: {responseContent}");
                }
                
                if (_debug)
                {
                    Console.WriteLine($"Debug: API Response: {responseContent}");
                }
                
                // Parse the response and extract the content
                var responseJson = JsonSerializer.Deserialize<JsonDocument>(responseContent);
                string resultContent = responseJson.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                
                // Handle empty responses with appropriate messages
                if (string.IsNullOrWhiteSpace(resultContent))
                {
                    // If content is empty but we got a successful response, check the finish reason
                    string finishReason = responseJson.RootElement.GetProperty("choices")[0].TryGetProperty("finish_reason", out var finishReasonElement) ? 
                        finishReasonElement.GetString() : "unknown";
                        
                    Console.WriteLine($"Warning: Received empty content with finish reason: {finishReason}");
                    
                    if (finishReason == "length")
                    {
                        return "Error: Response was cut off due to token limit. Please try again with a shorter input or request more tokens.";
                    }
                    
                    return "Error: The model returned an empty response.";
                }
                
                return resultContent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chat completion error: {ex.Message}");
                throw;
            }
        }
    }
}
