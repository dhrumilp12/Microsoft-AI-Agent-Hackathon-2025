# Vocabulary Bank & Flashcards Generator

![Vocabulary Bank Banner](https://img.shields.io/badge/Microsoft-AI%20Agent%20Hackathon%202025-blue)
![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4)
![Azure OpenAI](https://img.shields.io/badge/Azure-OpenAI-0078D4)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

## Project Description

The Vocabulary Bank & Flashcards Generator is an AI-powered educational tool designed to enhance learning experiences by automatically extracting key terminology from educational content. This application scans lecture transcripts or educational texts, identifies important technical or domain-specific terms, and generates comprehensive flashcards with definitions, contextual examples, and usage information.

### Purpose

- **Identify Learning Opportunities**: Automatically scans transcripts for complex, technical, or domain-specific words that might be challenging for users.
- **Enhance Retention**: Creates flashcards with clear definitions and contextual examples to reinforce vocabulary retention.
- **Facilitate Review**: Provides a structured vocabulary bank that allows users to review key concepts in an accessible flashcard format.
- **Support Export and Integration**: Generated flashcard data can be easily integrated into Learning Management Systems (LMS) or study apps.
- **Automate Learning Assistance**: Takes the manual effort out of creating study materials—letting educators and students focus more on the lecture content itself.

### Key Features

- **Intelligent term extraction** using Azure OpenAI to identify important vocabulary
  - Employs frequency analysis and AI filtering to identify domain-specific terminology
  - Balances technical terms and contextually important concepts
  - Eliminates common words and focuses on educationally valuable vocabulary
- **Automatic definition generation** with examples and contextual information
  - Utilizes Azure OpenAI to create accurate, context-aware definitions
  - Generates authentic usage examples from original content
  - Provides subject-specific context for better understanding
- **Multi-language support** with translation capabilities
  - Supports 100+ languages through Azure Translator integration
  - Maintains terminology relationships across languages
  - Enables bilingual flashcard creation for language learning
- **Multiple export formats**: JSON, CSV, HTML
  - Structured JSON for integration with other applications
  - CSV format for easy import into spreadsheets and databases
  - Interactive HTML flashcards with built-in styling
- **Microsoft 365 integration** for direct export to cloud storage
  - Seamless OneDrive integration for cloud-based access
  - Automatic file sharing with configurable permissions
  - Direct access through Microsoft 365 ecosystem
- **Interactive console interface** with progress visualization
  - Real-time progress tracking with visual indicators
  - Clear workflow guidance through complex processes
  - Intuitive error handling and recovery options

## Technical Architecture

The application follows a modular, service-based architecture designed for extensibility and maintainability:

### Prerequisites

- .NET SDK 9.0 or later
- Azure OpenAI service account (for term extraction and definition generation)
- Azure Translator service account (optional, for translation features)
- Microsoft 365 account (optional, for M365 export features)

### Installation and Setup

1. **Clone the repository**

   ```bash
   git clone https://github.com/yourusername/vocabulary-bank.git
   cd vocabulary-bank
   ```

2. **Configure Azure services**

   - Copy `.env.example` to `.env`
   - Add your Azure OpenAI API credentials:
     ```
     AZURE_OPENAI_ENDPOINT=https://your-endpoint.openai.azure.com/
     AZURE_OPENAI_API_KEY=your-api-key
     AZURE_OPENAI_DEPLOYMENT_NAME=your-model-deployment-name
     ```
   - (Optional) Add Azure Translator credentials for translation features:
     ```
     TRANSLATOR_API_KEY=your-translator-key
     TRANSLATOR_ENDPOINT=https://api.cognitive.microsofttranslator.com/
     TRANSLATOR_REGION=your-region
     ```
   - (Optional) Add Microsoft 365 credentials for M365 export:
     ```
     M365_CLIENT_ID=your-client-id
     M365_TENANT_ID=your-tenant-id
     M365_CLIENT_SECRET=your-client-secret
     ```

3. **Build the application**

   ```bash
   dotnet build
   ```

4. **Run the application**

   ```bash
   dotnet run
   ```

## Usage

1. When prompted, select your input method:
   - Use the sample transcript (for testing)
   - Enter a path to your transcript file
   - Drag and drop a file into the console

2. Choose whether to translate the content (optional)

3. After processing, the application will:
   - Extract key vocabulary terms
   - Generate definitions and examples
   - Create flashcards
   
4. Select your preferred export format:
   - JSON
   - CSV
   - HTML
   - Microsoft 365 (if configured)

5. Specify the output location for the exported flashcards

## Configuration Options

All configuration options can be set either in the `appsettings.json` file or through environment variables:

```json
{
  "AzureOpenAI": {
    "UseEnvironmentVariables": true,
    "ApiVersion": "2024-12-01-preview",
    "Debug": false,
    "MaxRetries": 5,
    "InitialRetryDelayMs": 2000
  },
  "AzureTranslator": {
    "UseEnvironmentVariables": true,
    "Debug": false
  },
  "M365": {
    "UseEnvironmentVariables": true,
    "ClientId": "",
    "TenantId": "",
    "ClientSecret": ""
  }
}
```

## Contributor Guidelines

### Development Environment Setup

1. Ensure you have .NET SDK 9.0 or later installed
2. Install Visual Studio 2022 or Visual Studio Code with C# extensions
3. Configure Azure services as described in the Installation section

### Code Style and Standards

- Follow the existing code style and patterns
- Use meaningful variable and method names
- Add XML comments for public classes and methods
- Use asynchronous programming where appropriate (Task-based methods)
- Ensure proper error handling and logging

### Pull Request Process

1. Fork the repository
2. Create a feature branch from `main`
3. Make your changes, following the code style guidelines
4. Update documentation if necessary
5. Ensure all tests pass
6. Submit a pull request with a clear description of the changes

### Areas for Contribution

- Additional export formats
- UI improvements
- Performance optimizations
- Extended language support
- Integration with additional learning platforms
- Enhanced flashcard generation features
- Improved vocabulary term extraction algorithms

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Microsoft AI Agent Hackathon 2025
- Azure OpenAI service
- Azure Translator service
