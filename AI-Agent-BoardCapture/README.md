# Classroom Board Capture

An application that captures whiteboard content from a camera, performs OCR to extract text, translates the text, and analyzes the image content.

## Features

- Automatic webcam capture at configurable intervals
- Text extraction using Azure Computer Vision API with Tesseract fallback
- Translation of extracted text using Azure Translator API
- Image content analysis with object detection
- Saves results alongside captured images

## Requirements

- .NET 9.0 SDK
- Webcam or camera device
- Azure Computer Vision API key
- Azure Translator API key
- Tesseract OCR (optional for fallback)

## Setup Instructions

1. Clone the repository
2. Create a `.env` file by copying `.env.example` and filling in your API keys
3. Install Tesseract OCR (optional for fallback):
   - Windows: Download from [GitHub](https://github.com/UB-Mannheim/tesseract/wiki)
   - Linux: `sudo apt-get install tesseract-ocr`
   - Mac: `brew install tesseract`
4. Create a `tessdata` folder and download language data files from [GitHub](https://github.com/tesseract-ocr/tessdata)
5. Build the application:
   ```bash
   dotnet build
   ```
6. Run the application:
   ```bash
   dotnet run
   ```

## Configuration Options

Edit `appsettings.json` to customize:
- Capture interval
- Source and target languages
- Camera device ID
- API endpoints
- Tesseract settings

## Usage

1. Start the application
2. The camera will automatically capture images at the defined interval
3. Press ESC to stop capturing and exit
4. Check the Captures folder for:
   - Image files (.jpg)
   - Text extraction files (.txt)
   - Analysis results (.analysis.txt)