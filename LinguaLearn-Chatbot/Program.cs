using System;
using System.Collections.Generic;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Services;
using System.Threading.Tasks;
using System.IO;
using LinguaLearnChatbot.Services;
using ClassroomBoardCapture.Services;
using DiagramGenerator.Services;
using VocabularyBank.Services;
using System.Windows.Forms;

namespace LinguaLearnChatbot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Welcome to LinguaLearn Chatbot!");

            Kernel kernel;

            // Initialize the kernel with Azure OpenAI credentials
            try
            {
                kernel = Kernel.CreateBuilder()
                    .AddAzureOpenAIChatCompletion(
                        Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "your-deployment-name",
                        Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? "your-endpoint",
                        Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? "your-api-key"
                    )
                    .Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing kernel: {ex.Message}");
                return;
            }

            // Load skills from the Skills directory
            var skillsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Skills"));
            if (!Directory.Exists(skillsDirectory))
            {
                Console.WriteLine("Skills directory not found. Please ensure the 'Skills' folder exists.");
                return;
            }

            // Load skills dynamically
            string[] skillNames = {
                "ImageAnalysis", "ImageCapture", "OCR", "Translation", "DiagramRendering",
                "SpeechToText", "VocabularyExtraction", "FlashcardGeneration", "DefinitionGeneration", "Summarization"
            };
            var skills = new Dictionary<string, IDictionary<string, Func<string, Task<string>>>>();

            foreach (var skillName in skillNames)
            {
                try
                {
                    var skillFiles = Directory.GetFiles(Path.Combine(skillsDirectory, skillName), "*.txt");
                    var skill = new Dictionary<string, Func<string, Task<string>>>();

                    foreach (var file in skillFiles)
                    {
                        var functionName = Path.GetFileNameWithoutExtension(file);
                        var functionCode = File.ReadAllText(file);

                        // Example: Create a simple function that echoes the input
                        Func<string, Task<string>> function = async (input) =>
                        {
                            await Task.Delay(100); // Simulate async work
                            return $"Executed {functionName} with input: {input}";
                        };

                        skill[functionName] = function;
                    }

                    skills[skillName] = skill;

                    Console.WriteLine($"Loaded skill: {skillName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading skill '{skillName}': {ex.Message}");
                }
            }

            // Initialize CLU Service
            var cluService = new CLUService(
                Environment.GetEnvironmentVariable("AZURE_CLU_ENDPOINT") ?? "your-clu-endpoint",
                Environment.GetEnvironmentVariable("AZURE_CLU_API_KEY") ?? "your-clu-api-key"
            );

            // Initialize services for each agent
            var imageAnalysisService = new ImageAnalysisService();
            var imageCaptureService = new ImageCaptureService();
            var ocrService = new OcrService();
            var translationService = new TranslationService();
            var diagramRendererService = new AI_agent_DiagramGenerator.Services.DiagramRenderer();
            var speechToTextService = new AI_agent_SpeechTranslator.Services.SpeechToTextService();
            var summarizationService = new AI_Summarization_agent.Services.SummarizationService();

            // Example: Handle drag-and-drop file uploads
            Console.WriteLine("Drag and drop a file or type your input:");

            while (true)
            {
                Console.Write("You: ");
                string userInput = Console.ReadLine();

                if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                // Check if the input contains a file path
                string filePath = null;
                foreach (var part in userInput.Split(' '))
                {
                    if (File.Exists(part))
                    {
                        filePath = part;
                        break;
                    }
                }

                if (filePath != null)
                {
                    string fileExtension = Path.GetExtension(filePath).ToLower();

                    switch (fileExtension)
                    {
                        case ".txt":
                            string textContent = File.ReadAllText(filePath);
                            Console.WriteLine($"Extracted text: {textContent}");
                            userInput = userInput.Replace(filePath, textContent); // Replace file path with content
                            break;

                        case ".jpg":
                        case ".png":
                            var ocrResult = await ocrService.ExtractTextAsync(filePath);
                            Console.WriteLine($"Extracted text from image: {ocrResult}");
                            userInput = userInput.Replace(filePath, ocrResult); // Replace file path with OCR result
                            break;

                        case ".mp3":
                        case ".wav":
                            var speechResult = await speechToTextService.ConvertSpeechToTextAsync(filePath);
                            Console.WriteLine($"Extracted text from audio: {speechResult}");
                            userInput = userInput.Replace(filePath, speechResult); // Replace file path with speech-to-text result
                            break;

                        default:
                            Console.WriteLine("Unsupported file type. Please upload a .txt, .jpg, .png, .mp3, or .wav file.");
                            continue;
                    }
                }

                try
                {
                    // Use CLU to determine intent
                    var intent = await cluService.GetIntentAsync(userInput);

                    Console.WriteLine($"Detected intent: {intent}");

                    // Map intent to service function and invoke
                    switch (intent.ToLower())
                    {
                        case "analyzeimage":
                            var analysisResult = await imageAnalysisService.AnalyzeImageAsync(userInput);
                            Console.WriteLine($"Chatbot: {analysisResult}");
                            break;

                        case "translate":
                            var translationResult = await translationService.TranslateTextAsync(userInput);
                            Console.WriteLine($"Chatbot: {translationResult}");
                            break;

                        default:
                            Console.WriteLine("Chatbot: Sorry, I didn't understand that.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing input: {ex.Message}");
                }
            }
        }
    }
}
