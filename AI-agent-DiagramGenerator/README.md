# Visual Diagram Generation

## Role
Creates visual diagrams or illustrations based on the concepts being discussed in class.

## Features
- AI generates flowcharts, mind maps, or diagrams based on the spoken lecture.
- Integrates with Microsoft Whiteboard API or custom tools for visual aid.
- Students can interact with diagrams to break them down further.

## Implementation
This project is built as a .NET console application that:
1. Uses Azure Speech Services to convert spoken lectures to text
2. Analyzes text with Azure OpenAI to extract key concepts
3. Generates diagram structures using Azure OpenAI
4. Renders diagrams using Mermaid.js or similar library
5. Integrates with Microsoft Whiteboard API for sharing and collaboration
6. Provides an interactive interface for students to modify diagrams

## Requirements
- .NET 8.0 SDK or later
- Azure subscription with the following services:
  - Azure Speech Service
  - Azure OpenAI Service
  - Azure App Service (for hosting web interface)
  - Azure Storage (for storing diagrams)

## Setup
1. Clone the repository
2. Update the appsettings.json with your Azure credentials
3. Run `dotnet restore` to install dependencies
4. Run `dotnet run` to start the application