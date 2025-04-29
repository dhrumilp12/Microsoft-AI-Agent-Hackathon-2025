# StudyBuddy

**StudyBuddy** is an AI-powered classroom assistant that transforms learning with real-time transcription, translation, and personalized study tools. Powered by multiple AI agents, it creates an interactive and inclusive educational experience for students and educators.

## Authors

Developed by:

- Pramod K. Singh (Lead)
- Aashish Anand (Speech & Translation Specialist)
- Dhrumil Patel (Diagram, Image Processing & OCR Expert)
- Abhishiktha Valavala (AI Chatbot & .NET Developer)

## Features

StudyBuddy integrates the following AI agents:

- **Speech Translator**: Transcribes speech, translates it into a target language, and optionally reads it aloud.
- **Summarizer**: Creates concise summaries from transcripts or text.
- **Vocabulary & Flashcards**: Extracts key terms and generates flashcards for study.
- **Diagram Generator**: Produces visual diagrams from text or summaries.
- **Board Capture**: Analyzes and digitizes classroom board content.
- **Chatbot**: Engages users with interactive, Azure OpenAI-powered conversations.

## Tech Stack

| Component            | Technology                                            |
| -------------------- | ----------------------------------------------------- |
| **Language**         | C#                                                    |
| **Framework**        | .NET 9.0                                              |
| **AI Services**      | Azure Cognitive Services (Speech, Translator, Vision) |
| **OCR**              | Tesseract                                             |
| **Image Processing** | OpenCV (OpenCvSharp4)                                 |
| **Dependencies**     | Microsoft.Extensions.\*, DotNetEnv, Spectre.Console   |

## Prerequisites

- .NET 9.0 SDK
- Azure Cognitive Services account
- (Optional) Microsoft 365 account for flashcard exports
- (For using the chatbot) Cosmos DB for data storage

## How to Run

1. Clone the repository:
   ```bash
   git clone https://github.com/dhrumilp12/Microsoft-AI-Agent-Hackathon-2025.git
   ```
2. Navigate to the orchestrator directory:
   ```bash
   cd Microsoft-AI-Agent-Hackathon-2025/AI-Agent-Orchestrator
   ```
3. Set up environment variables in a `.env` file (see below).
4. Run the application:
   ```bash
   dotnet run
   ```
5. Choose a workflow (Whiteboard Capture, Audio Learning or both, which will run the two workflows in parallel) or interact with the chatbot via the console.

## Environment Variables

Create a `.env` file one level above the orchestrator directory with the following:

### Azure Cognitive Services

- `AZURE_OPENAI_ENDPOINT`: Your Azure OpenAI endpoint.
- `AZURE_OPENAI_API_KEY`: Your Azure OpenAI API key.
- `AZURE_OPENAI_DEPLOYMENT_NAME`: Your Azure OpenAI deployment name.
- `SPEECH_API_KEY`: Your Azure Speech API key.
- `SPEECH_REGION`: Your Azure Speech region.
- `TRANSLATOR_API_KEY`: Your Azure Translator API key.
- `TRANSLATOR_REGION`: Your Azure Translator region.
- `TRANSLATOR_ENDPOINT`: Your Azure Translator endpoint.
- `VISION_API_KEY`: Your Azure Vision API key.

### Microsoft 365 Configuration (if exporting vocab flashcards to Microsoft 365)

- `M365_CLIENT_ID`: Client ID for Microsoft 365
- `M365_TENANT_ID`: Tenant ID for Microsoft 365
- `M365_CLIENT_SECRET`: Client Secret for Microsoft 365

### Cosmos DB (For chatbot use)

- `COSMOSDB_CONNECTION_STRING`: Connection string for Cosmos DB.
- `COSMOSDB_DATABASENAME`: Name of the Cosmos DB database.
- `COSMOSDB_CONTAINERNAME`: Name of the Cosmos DB container.

### Diagram Configurations

- `MERMAID_OUTPUT_DIRECTORY`: The output directory of the diagrams
- `MAX_TOKENS`: The maximum number of tokens for the OpenAI service
- `TEMPERATURE`: The temperature parameter for the OpenAI service

### Diagram Configurations

- `MERMAID_OUTPUT_DIRECTORY`: Diagram output directory
- `MAX_TOKENS`: Max tokens for OpenAI
- `TEMPERATURE`: Temperature for OpenAI

## Project Structure

- `/AI-Agent-Orchestrator`: Main application coordinating all agents
- `/AI-Agent-BoardCapture`: AI agent that uses your camera to capture text from a board, analyze it, and translate it
- `/AI-agent-SpeechTranslator`: AI agent that converts speech to text and translates them. Both the original and translated texts are saved as text files.
- `/AI-Agent-VocabularyBank`: AI agent that handles extraction of key vocabulary terms from both original and translated texts
- `/AI-Summarization-agent`: AI agent that generates concise summaries from audio transcripts or whiteboard text
- `/AI-agent-DiagramGenerator`: AI agent that produces visual diagrams from summaries or text.
- `/AgentData`: Contains translation, summary, images, diagram & vocabulary files created by individual agents

## License

This project is licensed under the MIT License.

## Demo

Check out a live demo or screenshots at [link-to-demo-or-screenshots].
