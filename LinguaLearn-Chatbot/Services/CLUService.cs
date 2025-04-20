using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LinguaLearnChatbot.Services
{
    public class CLUService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;

        public CLUService(string endpoint, string apiKey)
        {
            _endpoint = endpoint;
            _apiKey = apiKey;
        }

        public async Task<string> GetIntentAsync(string userInput)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);

            var requestBody = new
            {
                query = userInput
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(_endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CLUResponse>(responseContent);

            return result?.TopIntent ?? "Unknown";
        }

        private class CLUResponse
        {
            public string TopIntent { get; set; }
        }
    }
}