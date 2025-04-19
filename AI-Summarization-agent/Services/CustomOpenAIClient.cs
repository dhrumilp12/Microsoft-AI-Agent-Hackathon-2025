using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AI_Summarization_agent.Services
{
    public class CustomOpenAIClient
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _deploymentName;
        private readonly HttpClient _httpClient;

        // Initializes the client with configuration values and sets up HttpClient
        public CustomOpenAIClient(IConfiguration configuration)
        {
            _endpoint = configuration["AZURE_OPENAI_ENDPOINT"] ?? throw new ArgumentNullException("AZURE_OPENAI_ENDPOINT is not configured.");
            _apiKey = configuration["AZURE_OPENAI_API_KEY"] ?? throw new ArgumentNullException("AZURE_OPENAI_API_KEY is not configured.");
            _deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? throw new ArgumentNullException("AZURE_OPENAI_DEPLOYMENT_NAME is not configured.");
            _httpClient = new HttpClient();
        }

        // Sends a request to Azure OpenAI to summarize the given input
        public async Task<string> GetSummaryAsync(string input, double temperature = 0.5, int maxTokens = 1000)
        {
            var requestUri = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-12-01-preview";

            var requestBody = new
            {
                messages = new[]
                {
                    new {
                        role = "system",
                        content = "You are an expert summarization assistant. Summarize the user's input clearly, highlighting key points. Also, provide 2-3 helpful and credible website links where students can learn more about the topic."
                    },
                    new { role = "user", content = input }
                },
                max_completion_tokens = maxTokens
            };

            var requestJson = JsonSerializer.Serialize(requestBody);

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            try
            {
                var response = await _httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"OpenAI API request failed. Status Code: {response.StatusCode}, Response: {responseString}");
                }

                // Parses and returns the summarized content from the response
                using var jsonDoc = JsonDocument.Parse(responseString);
                if (jsonDoc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message").GetProperty("content").GetString();
                    return !string.IsNullOrEmpty(message) ? message : "No summary content returned.";
                }

                return "No summary generated, missing 'choices' in response.";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while making OpenAI API request: {ex.Message}", ex);
            }
        }
    }
}
