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
| Component              | Technology                          |
|-----------------------|-------------------------------------|
| **Language**          | C#                                  |
| **Framework**         | .NET 9.0                            |
| **AI Services**       | Azure Cognitive Services (Speech, Translator, Vision) |
| **OCR**               | Tesseract                           |
| **Image Processing**  | OpenCV (OpenCvSharp4)               |
| **Dependencies**      | Microsoft.Extensions.*, DotNetEnv, Spectre.Console |

## Prerequisites
- .NET 9.0 SDK
- Azure Cognitive Services account
- (Optional) Microsoft 365 account for flashcard exports
- (Optional) Cosmos DB for data storage

## How to Run
1. Clone the repository:
   ```bash
   git clone https://github.com/<your-repo>/StudyBuddy.git
   ```
2. Navigate to the orchestrator directory:
   ```bash
   cd StudyBuddy/AI-Agent-Orchestrator
   ```
3. Set up environment variables in a `.env` file (see below).
4. Run the application:
   ```bash
   dotnet run
   ```
5. Choose a workflow (e.g., transcription, summarization) or interact with the chatbot via the console.

## Environment Variables
Create a `.env` file one level above the orchestrator directory with the following:

### Azure Cognitive Services
- `AZURE_OPENAI_ENDPOINT`: Azure OpenAI endpoint
- `AZURE_OPENAI_API_KEY`: Azure OpenAI API key
- `AZURE_OPENAI_DEPLOYMENT_NAME`: Azure OpenAI deployment name
- `SPEECH_API_KEY`: Azure Speech API key
- `SPEECH_REGION`: Azure Speech region
- `TRANSLATOR_API_KEY`: Azure Translator API key
- `TRANSLATOR_REGION`: Azure Translator region
- `TRANSLATOR_ENDPOINT`: Azure Translator endpoint
- `VISION_API_KEY`: Azure Vision API key

### Microsoft 365 (Optional)
- `M365_CLIENT_ID`: Microsoft 365 client ID
- `M365_TENANT_ID`: Microsoft 365 tenant ID
- `M365_CLIENT_SECRET`: Microsoft 365 client secret

### Cosmos DB (Optional)
- `COSMOSDB_CONNECTION_STRING`: Cosmos DB connection string
- `COSMOSDB_DATABASENAME`: Cosmos DB database name
- `COSMOSDB_CONTAINERNAME`: Cosmos DB container name

### Diagram Configurations
- `MERMAID_OUTPUT_DIRECTORY`: Diagram output directory
- `MAX_TOKENS`: Max tokens for OpenAI
- `TEMPERATURE`: Temperature for OpenAI

## Project Structure
- `/AI-Agent-Orchestrator`: Main application coordinating all agents
- `/AI-Agents-*`: Individual AI agent implementations
- `/Configs`: Configuration files and utilities
- `AgentData`: Contains translation, summary, images, diagram & vocabulary files created by induvidual agnets


## License
This project is licensed under the MIT License. 

## Demo
Check out a live demo or screenshots at [link-to-demo-or-screenshots].
