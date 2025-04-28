# StudyBuddy

StudyBuddy is an AI-powered classroom assistant designed to enhance learning experiences by offering real-time transcription, translation, and personalized insights. It integrates multiple AI agents to provide a seamless and interactive educational environment.

---

## Authors

This project was developed by:
- **Pramod K. Singh**
- **Aashish Anand**
- **Dhrumil Patel**
- **Abhishiktha Valavala**

---

## Included Agents

1. **Speech Translator Agent**: Converts speech to text, translates it into a target language, and optionally speaks the translated text back.
2. **Summarization Agent**: Generates concise summaries from transcripts or text.
3. **Vocabulary Bank & Flashcards Generator Agent**: Extracts key vocabulary and creates flashcards for learning.
4. **Diagram Generator Agent**: Creates diagrams based on text or summaries.
5. **Board Capture Agent**: Captures and analyzes classroom board content.
6. **Chatbot**: Integrated within the orchestrator, the chatbot provides interactive conversations powered by Azure OpenAI.

---

## Tech Stack

- **Programming Language**: C#
- **Framework**: .NET 9.0
- **AI Services**: Azure Cognitive Services (Speech, Translator, Vision)
- **OCR**: Tesseract
- **Image Processing**: OpenCV
- **Dependency Injection**: .NET DI
- **Configuration Management**: .NET Configuration

---

## Dependencies

The project relies on the following dependencies:
- **Microsoft.Extensions.Configuration**: For configuration management.
- **Microsoft.Extensions.DependencyInjection**: For dependency injection.
- **Microsoft.Extensions.Logging**: For logging.
- **DotNetEnv**: For loading environment variables.
- **Azure Cognitive Services SDKs**: For speech, translation, and vision services.
- **Spectre.Console**: For building interactive console applications.
- **Tesseract**: For OCR functionality.
- **OpenCvSharp4**: For image processing.

---

## How to Run the Project

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

## Environment Variables

To run the project, ensure the following environment variables are set in a centralized `.env` file located one level above the orchestrator directory:

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

### Cosmos DB
- `COSMOSDB_CONNECTION_STRING`: Connection string for Cosmos DB.
- `COSMOSDB_DATABASENAME`: Name of the Cosmos DB database.
- `COSMOSDB_CONTAINERNAME`: Name of the Cosmos DB container.

---

## Additional Notes
- Ensure the `.env` file is located one level above the orchestrator directory.
- Use the orchestrator to coordinate workflows or interact with individual agents.

