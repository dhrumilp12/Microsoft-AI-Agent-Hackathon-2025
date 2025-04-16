# AI Agent - Speech Translator

## Description
The AI Agent - Speech Translator is a real-time speech-to-text and translation application. It leverages Microsoft Cognitive Services for speech recognition and translation, enabling users to transcribe spoken words and translate them into a target language. Additionally, the agent provides real-time audio feedback by speaking the translated text.

## Features
- **Real-Time Speech Recognition**: Converts spoken words into text using Microsoft Cognitive Services.
- **Real-Time Translation**: Translates recognized text into a target language.
- **Audio Feedback**: Speaks the translated text back to the user in real-time.
- **Multi-Language Support**: Supports multiple source and target languages.

## Setup Instructions

### Prerequisites
1. Install [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
2. Install [FFmpeg](https://ffmpeg.org/) (required for audio extraction from video files).
3. Set up Azure Cognitive Services:
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

## Usage
1. Run the application:
   ```bash
   dotnet run
   ```
2. Follow the prompts:
   - Enter the source language (e.g., `en` for English).
   - Enter the target language (e.g., `es` for Spanish).
   - Start speaking into your microphone.
   - Press `Enter` to stop.
3. The application will:
   - Display recognized text in real-time.
   - Translate the text into the target language.
   - Speak the translated text back to you.

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