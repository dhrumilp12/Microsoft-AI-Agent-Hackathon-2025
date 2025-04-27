using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using Azure.AI.OpenAI;
using AI_Agent_Orchestrator.Models;

namespace AI_Agent_Orchestrator.Services;

public class CosmosDbService
{
    private readonly Container _container;
    private readonly AzureOpenAIClient _openAIClient;

    public CosmosDbService(CosmosClient cosmosClient, string databaseName, string containerName, AzureOpenAIClient openAIClient)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
        _openAIClient = openAIClient;
    }

    /// <summary>
    /// Adds a conversation record to the Cosmos DB container.
    /// This method creates a new item in the container with the provided user ID, user query, bot response, and previous conversations.
    /// </summary>
    /// <param name="userId">The ID that represents the user</param>
    /// <param name="userQuery">The query that user entered into the chatbot</param>
    /// <param name="botResponse">The generated response from the chatbot given the user query</param>
    /// <param name="prevConversations">A list of all the previous conversations between the user and chatbot</param>
    /// <returns>
    /// Asynchronous task that represents the operation of adding a conversation record to the Cosmos DB container.
    /// The task does not return any value.
    /// </returns>
    /// <remarks>
    /// This method is designed to be called when a new conversation is initiated or continued.
    /// It generates a unique ID for the conversation record and sets the conversation ID based on the count of previous conversations.
    /// The timestamp is set to the current UTC time.
    /// </remarks>
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

    /// <summary>
    /// Retrieves all conversations for a specific user from the Cosmos DB container.
    /// This method queries the container for items that match the provided user ID and returns a list of conversation records.
    /// </summary>
    /// <param name="userId">The ID that represents the user</param>
    /// <returns>
    /// Asynchronous task that returns a list of conversation records for the specified user.
    /// The list contains all conversations associated with the user ID.
    /// </returns>
    /// <remarks>
    /// This method is designed to be called when retrieving conversation history for a user.
    /// It uses a SQL-like query to filter items based on the user ID.
    /// </remarks>
    /// <exception cref="CosmosException">Thrown when there is an error while querying the Cosmos DB container.</exception>
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

    /// <summary>
    /// Converts a list of LLMConversation objects to a string representation.
    /// This method formats each conversation in the list as "User: {userQuery} Bot: {botResponse}"
    /// and joins them with a newline character.
    /// </summary>
    /// <param name="conversations">The list of LLMConversation objects to convert.</param>
    /// <returns>
    /// A string representation of the conversations, formatted as "User: {userQuery} Bot: {botResponse}"
    /// joined with newline characters.
    /// </returns>
    /// <remarks>
    /// This method is useful for displaying or logging the conversation history in a readable format.
    /// It iterates through each conversation in the list and formats it accordingly.
    /// </remarks>
    public string ConvertConversationsToString(List<LLMConversation> conversations)
    {
        return string.Join(Environment.NewLine, conversations.Select(c => $"User: {c.UserQuery} Bot: {c.BotResponse}"));
    }
}