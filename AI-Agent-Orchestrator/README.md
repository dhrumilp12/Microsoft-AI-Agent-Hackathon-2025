# AI Agent Orchestrator

The AI Agent Orchestrator provides a unified interface for accessing and managing multiple AI agents from a single location. It leverages Microsoft Semantic Kernel to intelligently match user queries with the most relevant AI agents.

## Features

- **Centralized Management**: Access all available AI agents from a single interface
- **Intelligent Agent Recommendation**: Uses Azure OpenAI to suggest relevant agents based on natural language queries
- **User-Friendly Interface**: Interactive console-based UI built with Spectre.Console
- **Categorized Agent Organization**: Agents are organized by category (Education, Content, Language, etc.)
- **Environment Variable Propagation**: Automatically passes environment variables to child processes
- **Adaptive Path Resolution**: Automatically locates agent directories in repository structure

## Supported Agents

The orchestrator currently supports the following agents:

1. **Vocabulary Bank & Flashcards Generator** - Creates flashcards from educational content with definitions and examples
2. **AI Summarization Agent** - Summarizes text content automatically
3. **Speech Translator** - Translates spoken language in real-time
4. **Diagram Generator** - Generates visual diagrams from text content
5. **Classroom Board Capture** - Captures, analyzes, and translates whiteboard content

## Prerequisites

- .NET 9.0 SDK or later
- Azure OpenAI API access (for intelligent agent matching)

## Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/Microsoft-AI-Agent-Hackathon-2025.git
   cd Microsoft-AI-Agent-Hackathon-2025
   ```

2. Navigate to the AI-Agent-Orchestrator directory:
   ```bash
   cd AI-Agent-Orchestrator
   ```

3. Configure your Azure OpenAI settings in the `.env` file:
   ```
   AZURE_OPENAI_ENDPOINT=your-endpoint
   AZURE_OPENAI_API_KEY=your-api-key
   AZURE_OPENAI_DEPLOYMENT_NAME=your-deployment-name
   ```

   Additional optional environment variables can be found in the `.env` file.

4. Build and run the application:
   ```bash
   dotnet run
   ```

## Usage

1. Start the orchestrator:
   ```bash
   dotnet run
   ```

2. The main menu will appear showing all available agents.

3. You can:
   - Type what you want to do in natural language to get relevant agent recommendations
   - Browse through the categorized list of agents
   - Select an agent to launch it

4. After selecting an agent, it will be launched in a new window. Return to the orchestrator window after you're done using the agent.

## Troubleshooting

### Agent Not Found
- Ensure all agent projects exist in the repository structure
- Check the paths in the AgentDiscoveryService.cs file
- If the path is incorrect, you'll be prompted to provide an alternative path

### Agent Execution Error
- Check that the agent project can be built and run independently
- Ensure all dependencies for the agent are installed
- Verify that environment variables needed by the agent are properly set in the `.env` file

### Semantic Search Not Working
- Verify your Azure OpenAI configuration in the `.env` file
- Ensure you have a valid deployment for text embeddings if you want to use semantic search
- Check that your Azure OpenAI key has access to the required models

## Architecture

The AI Agent Orchestrator is built with a modular architecture:

- **AgentDiscoveryService**: Discovers available AI agents in the repository structure
- **AgentExecutionService**: Handles launching and managing agent processes
- **SemanticKernelService**: Provides intelligent agent matching using Azure OpenAI

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
