using Microsoft.Azure.Cosmos;
using Azure;
using Azure.AI.OpenAI;
using Azure.AI.Inference;
using System.Threading;

namespace AI_Agent_Orchestrator.Services;

public class VectorSearchService
{
    private readonly Container _container;

    public VectorSearchService(CosmosClient cosmosClient, string databaseName, string containerName)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    /// <summary>
    /// Stores the embedding and metadata in Cosmos DB.
    /// /// </summary>
    /// <param name="id">The unique identifier for the embedding.</param>
    /// <param name="embedding">The embedding vector.</param>
    /// <param name="metadata">The metadata associated with the embedding.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the embedding is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when the metadata is null or empty.</exception>
    /// <exception cref="CosmosException">Thrown when there is an error storing the embedding in Cosmos DB.</exception>
    /// <remarks>
    /// This method stores the embedding and metadata in Cosmos DB. It creates a new item in the specified container with the provided id, embedding, and metadata.
    /// 
    /// The embedding is expected to be a float array, and the metadata is expected to be a string.
    /// The id is generated using a GUID to ensure uniqueness.
    /// Note: Ensure that the Cosmos DB container is set up to accept the structure of the document being stored.
    /// </remarks>
    public async Task StoreEmbeddingAsync(float[] embedding, string name, string description)
    {
        var document = new
        {
            id = Guid.NewGuid().ToString(),
            embedding = embedding,
            name = name,
            description = description
        };

        await _container.CreateItemAsync(document);
    }

    /// <summary>
    /// Retrieves the embedding for a given name from Cosmos DB.
    /// /// </summary>
    /// <param name="name">The name associated with the embedding.</param>
    /// <returns>The embedding vector as a float array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the name is null or empty.</exception>
    /// <exception cref="CosmosException">Thrown when there is an error retrieving the embedding from Cosmos DB.</exception>
    /// <remarks>
    /// This method retrieves the embedding for a given name from Cosmos DB.
    /// It queries the container for the item with the specified name and returns the embedding vector as a float array.
    /// 
    /// The name is expected to be a string, and the embedding is expected to be a float array.
    /// The method returns null if no embedding is found for the given name.
    /// 
    /// Note: Ensure that the Cosmos DB container is set up to accept the structure of the document being stored.
    /// </remarks>
    public async Task<float[]> RetrieveEmbeddingsAsync(string name)
    {
        var query = new QueryDefinition("SELECT c.embedding FROM c WHERE c.name = @name")
            .WithParameter("@name", name);

        var iterator = _container.GetItemQueryIterator<float[]>(query);
        var results = new List<float[]>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        // Check if the results list is empty after the while loop
        if (!results.Any())
        {
            return null; // Return null if no embeddings are found
        }

        return results[0];
    }

    /// <summary>
    /// Performs a vector search in Cosmos DB using the provided query and returns the top K results.
    /// </summary>
    /// <param name="query">The query string to search for.</param>
    /// <param name="topK">The number of top results to return.</param>
    /// <returns>A list of metadata strings representing the top K results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the query is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when the topK is less than or equal to 0.</exception>
    /// <remarks>
    /// This method performs a vector search in Cosmos DB using the provided query and returns the top K results.
    /// It converts the query to an embedding using the Azure OpenAI embedding model and then performs a vector search in the Cosmos DB container.
    /// 
    /// The query is expected to be a string, and the topK is expected to be an integer greater than 0.
    /// The method returns a list of metadata strings representing the top K results.
    /// 
    /// Note: Ensure that the Cosmos DB container is set up to accept the structure of the document being stored.
    /// </remarks>
    public async Task<List<string>> PerformVectorSearchAsync(string query, int topK = 3)
    {
        // Assuming you have a method to convert the query to an embedding
        var queryEmbedding = await ConvertToEmbeddingAsync(query);
        return await PerformVectorSearchAsync(queryEmbedding, topK);
    }

    /// <summary>
    /// Converts the provided query string to an embedding using the Azure OpenAI embedding model.
    /// </summary>
    /// <param name="query">The query string to convert to an embedding.</param>
    /// <returns>The embedding vector as a float array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the query is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no embedding is returned from the model.</exception>
    /// <remarks>
    /// This method converts the provided query string to an embedding using the Azure OpenAI embedding model.
    /// It calls the Azure OpenAI embedding model and returns the embedding as a float array.
    /// 
    /// The query is expected to be a string, and the embedding is expected to be a float array.
    /// The method returns null if no embedding is found for the given query.
    /// 
    /// Note: Ensure that the Azure OpenAI embedding model is set up correctly and accessible.
    /// </remarks>
    public async Task<float[]> ConvertToEmbeddingAsync(string query)
    {
        // Calls the Azure OpenAI embedding model (hard-coded below) and return the embedding as a float array
        var openAIClient = new AzureOpenAIClient(
            new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
            new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"))
        );
        
        var embeddingClient = openAIClient.GetEmbeddingClient("text-embedding-3-small");

        var response = await embeddingClient.GenerateEmbeddingAsync(query, cancellationToken: CancellationToken.None);

        if (response == null || response.Value == null || response.Value.ToFloats().ToArray().Length == 0)
        {
            throw new InvalidOperationException("No embedding returned from the model.");
        }

        return response.Value.ToFloats().ToArray();
    }

    public async Task<List<string>> PerformVectorSearchAsync(float[] queryEmbedding, int topK = 3)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var iterator = _container.GetItemQueryIterator<dynamic>(query);

        var results = new List<(string metadata, double similarity)>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                var storedEmbedding = item.embedding.ToObject<float[]>();
                var similarity = ComputeCosineSimilarity(queryEmbedding, storedEmbedding);
                results.Add((item.metadata, similarity));
            }
        }

        return results.OrderByDescending(r => r.similarity).Take(topK).Select(r => r.metadata).ToList();
    }

    public double ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            throw new ArgumentException("Vectors must be of the same length.");
        }

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0; // Avoid division by zero
        }

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}