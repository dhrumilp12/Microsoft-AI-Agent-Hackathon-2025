using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using AI_Agent_Orchestrator.Models;

namespace AI_Agent_Orchestrator.Services;

public class CosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;

    public CosmosDbService(string connectionString, string databaseName, string containerName)
    {
        _cosmosClient = new CosmosClient(connectionString);
        _container = _cosmosClient.GetContainer(databaseName, containerName);
    }

    public async Task AddConversationAsync(string userId, string userQuery, string botResponse, List<LLMConversation> prevConversations)
    {
        var conversationRecord = new
        {
            id = Guid.NewGuid().ToString(),
            conversationId = prevConversations.Count + 1,
            userId = userId,
            timestamp = DateTime.UtcNow,
            userQuery = userQuery,
            botResponse = botResponse
        };

        await _container.CreateItemAsync(conversationRecord);
    }

    public async Task<List<LLMConversation>> GetConversationsAsync(string userId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);

        var iterator = _container.GetItemQueryIterator<LLMConversation>(query);
        var conversations = new List<LLMConversation>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            conversations.AddRange(response);
        }

        return conversations;
    }

    public string ConvertConversationsToString(List<LLMConversation> conversations)
    {
        return string.Join(Environment.NewLine, conversations.Select(c => $"User: {c.UserQuery} Bot: {c.BotResponse}"));
    }
}