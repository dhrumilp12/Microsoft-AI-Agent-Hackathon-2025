# AI Agent - Speech Translator

## Description
The AI Agent - Speech Translator is a real-time speech-to-text and translation application. It leverages Microsoft Cognitive Services for speech recognition and translation, enabling users to transcribe spoken words and translate them into a target language. Additionally, the agent provides real-time audio feedback by speaking the translated text.

## Features
- **Real-Time Speech Recognition**: Converts spoken words into text using Microsoft Cognitive Services.
- **Real-Time Translation**: Translates recognized text into a target language.
- **Multi-Language Support**: Supports multiple source and target languages.
- **Transcript Output**: Saves the translated transcript to a file (`translated_transcript.txt`) for further processing.
- **Integration with Other Agents**: Outputs the translated transcript to be used by other agents, such as the Vocabulary Bank & Flashcards Generator, Summary Agent, and Diagram Generator.
- **Centralized .env File**: The agent loads environment variables from a centralized `.env` file located outside the agent directory.
- **Dynamic Source Language Prompt**: If the source language is not provided as a command-line argument, the user is prompted to enter it during runtime.
- **Optional Text-to-Speech Playback**: The user is prompted to choose whether the translated text should be spoken back after translation.

## Setup Instructions

### Prerequisites
1. Install [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
2. Set up Azure Cognitive Services:
   - Create a Speech resource in the Azure portal.
   - Create a Translator resource in the Azure portal.

### Environment Variables
Create a `.env` file in the root directory of the project with the following variables:
```
SPEECH_API_KEY=<Your Azure Speech API Key>
SPEECH_REGION=<Your Azure Speech Region>
TRANSLATOR_API_KEY=<Your Azure Translator API Key>
TRANSLATOR_REGION=<Your Azure Translator Region>
```

### Installation
1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd AI-agent-SpeechTranslator
   ```
2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

## Updated Usage
1. Ensure the centralized `.env` file is located one level above the agent directory.
2. Run the application:
   ```bash
   dotnet run -- <target-language-code> [source-language-code]
   ```
   - `<target-language-code>`: The language code for the target language (e.g., `en` for English).
   - `[source-language-code]` (optional): The language code for the source language (e.g., `es` for Spanish). If not provided, the user will be prompted to enter it.
3. Follow the prompts:
   - Speak into your microphone.
   - Press `Enter` to stop.
   - Choose whether to hear the translated text spoken back.
4. The application will:
   - Display recognized text in real-time.
   - Translate the text into the target language.
   - Optionally speak the translated text back.
   - Save the translated transcript to `Output/translated_transcript.txt`.

## Project Structure
- **Program.cs**: Entry point of the application.
- **Services/**: Contains the core services:
  - `SpeechToTextService.cs`: Handles speech recognition.
  - `TranslationService.cs`: Handles text translation.
  - **bin/** and **obj/**: Build and output directories.

## Dependencies
- [Microsoft.CognitiveServices.Speech](https://www.nuget.org/packages/Microsoft.CognitiveServices.Speech/)
- [Azure.AI.Translation.Text](https://www.nuget.org/packages/Azure.AI.Translation.Text/)
- [dotenv.net](https://www.nuget.org/packages/dotenv.net/)