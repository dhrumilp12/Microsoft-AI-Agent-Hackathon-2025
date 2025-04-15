## 🛠️ Setup Guide for Local Development

Follow these steps to set up and run the refactored application on your local machine:

---

### 1. 📁 Create Project Structure

# Create the project
dotnet new console -n ClassroomBoardCapture -f net9.0

# Navigate to project directory
cd ClassroomBoardCapture

# Create required directories
mkdir Services
mkdir Models
mkdir Utils
mkdir Captures
mkdir tessdata


### 2. 📦 Install Required Packages

# Core dependencies
dotnet add package OpenCvSharp4
dotnet add package OpenCvSharp4.Extensions
dotnet add package Tesseract

# Configuration and DI packages
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Http

# Environment variable loading
dotnet add package DotNetEnv


### 3. 📂 Copy Code Files
Copy the provided code files into the appropriate folders inside your project structure.

### 4. 🔐 Setup Environment Variables
Create a .env file in the project root (copy from .env.example if available), and add your actual API keys:

# Azure Computer Vision API
VISION_API_KEY=your_actual_vision_key_here

# Azure Translator API
TRANSLATOR_API_KEY=your_actual_translator_key_here
TRANSLATOR_REGION=eastus

## ⚠️ Ensure your .env file is listed in .gitignore to avoid accidental commits of sensitive information.

### 5. 🧠 Setup Tesseract (Optional for OCR Fallback)
If you want to use Tesseract OCR as a fallback:

Download and install Tesseract OCR for your platform.

Download required language data files (e.g., eng.traineddata).

Place them in the tessdata directory.

### 6. ▶️ Build and Run

# Build the application
dotnet build

# Run the application
dotnet run

### Key Improvements Made
🔧 Modular Architecture
Clean separation of responsibilities: capture, OCR, translation, analysis.

Services split with well-defined interfaces.

💉 Dependency Injection
Uses .NET built-in DI for better testability and flexibility.

Mockable services for testing.

⚙️ Configuration Management
Moved hardcoded values to config files.

Secrets stored securely via environment variables.

🧯 Better Error Handling
Comprehensive logging and exception handling.

Fallback support (e.g., Tesseract if Azure Vision fails).

🧼 Best Practices
XML doc comments for public methods.

Proper async/await usage.

Supports cancellation tokens.

Follows SOLID principles.

🔐 Security Improvements
API keys not hardcoded in source.

Uses .env file for secrets.

.env is excluded from version control.

