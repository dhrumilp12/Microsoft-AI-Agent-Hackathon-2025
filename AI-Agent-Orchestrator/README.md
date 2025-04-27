# AI Agent Orchestrator

The AI Agent Orchestrator is a centralized application designed to coordinate and execute multiple AI agents. It provides a user-friendly interface for selecting workflows, interacting with agents, and managing tasks such as speech translation, summarization, vocabulary generation, and diagram creation.

---

## Purpose of the Application
The orchestrator simplifies the process of managing and executing AI agents by providing a unified interface. It ensures seamless communication between agents and allows users to:
- Execute predefined workflows.
- Interact with an AI chatbot powered by Azure OpenAI.
- Dynamically match user queries to relevant agents or workflows using vector embeddings.

---

## How to Run the Application
1. Navigate to the orchestrator directory:
   ```bash
   cd AI-Agent-Orchestrator
   ```
2. Run the application:
   ```bash
   dotnet run
   ```
3. Follow the on-screen prompts to select workflows or interact with the chatbot.

---

## Workflow Options

When running the orchestrator, you will be prompted to choose one of the following workflows:

1. **Complete Audio Learning**: Executes the workflow for translating and summarizing audio content.
2. **Whiteboard**: Executes the workflow for capturing and analyzing classroom board content.
3. **Both**: Executes both the "Complete Audio Learning" and "Whiteboard" workflows in parallel.
4. **Chat with a Bot**: Activates the LLM chat for interactive conversations.
5. **Exit**: Exits the program.

Follow the on-screen prompts to select and execute the desired workflow.

---

## Key Features

1. **Interactive Workflow Selection**:
   - Users can choose from predefined workflows or interact with the chatbot.
   - Workflows include "Complete Audio Learning," "Whiteboard," and "Both." (executes both the audio and whiteboard workflows in parallel)

2. **LLM Chat Integration**:
   - The user can also choose to start a conversation with an AI chatbot powered by Azure OpenAI by selecting the option "Chat with a Bot".
   - Users can chat with the bot until they type "exit."

3. **Dynamic Embedding Management**:
   - The orchestrator uses vector embeddings to semantically match user queries with workflows or agents.
   - Missing embeddings are dynamically generated and stored in Cosmos DB for future use.

4. **Centralized Environment Variable Management**:
   - The orchestrator passes a centralized `.env` file to all agents, ensuring consistent configuration across the system.

5. **Error Handling and Logging**:
   - Comprehensive logging ensures that errors are captured and can be debugged effectively.

---

## Dependencies

The application relies on the following dependencies:
- **Microsoft.Extensions.Configuration**: For configuration management.
- **Microsoft.Extensions.DependencyInjection**: For dependency injection.
- **Microsoft.Extensions.Logging**: For logging.
- **DotNetEnv**: For loading environment variables.
- **Azure Cognitive Services SDKs**: For speech, translation, and vision services.
- **Spectre.Console**: For building interactive console applications.

---

## Tech Stack

- **Programming Language**: C#
- **Framework**: .NET 9.0
- **AI Services**: Azure Cognitive Services (Speech, Translator, Vision)
- **Dependency Injection**: .NET DI
- **Configuration Management**: .NET Configuration

---

## Environment Variables

To run the application, ensure the following environment variables are set in a centralized `.env` file located one level above the orchestrator directory:

### Azure OpenAI Services
- `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI endpoint.
- `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key.
- `AZURE_OPENAI_DEPLOYMENT_NAME`: Your Azure OpenAI deployment name.

### Cosmos DB
- `COSMOSDB_CONNECTION_STRING`: Connection string for Cosmos DB.
- `COSMOSDB_DATABASENAME`: Name of the Cosmos DB database.
- `COSMOSDB_CONTAINERNAME`: Name of the Cosmos DB container.

---

## Troubleshooting

### Missing Embeddings
- If embeddings are not found for a workflow or agent, the orchestrator dynamically generates and stores them in Cosmos DB.
- Ensure the Cosmos DB connection string and database/container names are correctly set in the `.env` file.

### Environment Variable Issues
- Verify that the centralized `.env` file is located one level above the orchestrator directory.
- Ensure all required environment variables are defined in the `.env` file.

### Agent Execution Errors
- Check that the agent project can be built and run independently.
- Verify that all dependencies for the agent are installed.
- Ensure the agent's environment variables are properly set.

---

## Additional Notes
- Ensure the `.env` file is located one level above the orchestrator directory.
- Use the orchestrator to coordinate workflows or interact with individual agents.
