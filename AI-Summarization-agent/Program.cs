using AI_Summarization_agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DotNetEnv;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using AI_Summarization_agent.Models;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Safe console setup
            try
            {
                Console.Title = "AI Summarization Agent";
                Console.Clear();
            }
            catch (IOException)
            {
                // Ignore console errors when running with redirected output
            }

            Console.WriteLine("=== AI Summarization Agent ===\n");

            // Load environment variables from the .env file
            Env.Load(@"../.env");

            // Set up configuration to pull from environment variables
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            // Set up dependency injection for services
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<CustomOpenAIClient>();
            services.AddSingleton<SummarizationService>();

            var serviceProvider = services.BuildServiceProvider();
            var summarizationService = serviceProvider.GetRequiredService<SummarizationService>();

            // Default values
            string capturePath = null;
            string speechTranscriptPath = "../AgentData/Recording/translated_transcript.txt";

            // Parse capture file if provided
            if (args.Length > 0 && File.Exists(args[0]))
            {
                capturePath = args[0];
                Console.WriteLine($"Using provided capture file: {Path.GetFullPath(capturePath)}");
            }
            else
            {
                Console.WriteLine("No capture file provided or file missing. Proceeding without capture text.");
            }

            // Parse speech translated file
            if (args.Length > 1 && File.Exists(args[1]))
            {
                speechTranscriptPath = args[1];
                Console.WriteLine($"Using provided speech translated file: {Path.GetFullPath(speechTranscriptPath)}");
            }
            else
            {
                Console.WriteLine($"Using default speech translated file: {Path.GetFullPath(speechTranscriptPath)}");
            }

            // Validate that at least speech translated file exists
            if (!File.Exists(speechTranscriptPath))
            {
                Console.WriteLine("Error: Speech translated file is missing. Cannot continue.");
                return;
            }

            // Parse language codes
            string targetLanguage = "en"; // Default
            string sourceLanguage = "en"; // Default

            if (args.Length > 2)
            {
                targetLanguage = args[2];
                Console.WriteLine($"Target Language set to: {targetLanguage}");
            }
            else
            {
                Console.WriteLine("No target language provided. Defaulting to 'en' (English).");
            }

            if (args.Length > 3)
            {
                sourceLanguage = args[3];
                Console.WriteLine($"Source Language set to: {sourceLanguage}");
            }
            else
            {
                Console.WriteLine("No source language provided. Defaulting to 'en' (English).");
            }

            // Read file contents
            string captureText = string.Empty;
            if (!string.IsNullOrEmpty(capturePath) && File.Exists(capturePath))
            {
                captureText = await File.ReadAllTextAsync(capturePath);
            }

            string speechText = await File.ReadAllTextAsync(speechTranscriptPath);

            string vocabularyData = string.Empty;
            if (args.Length > 4 && File.Exists(args[4]) && args[4].EndsWith(".json"))
            {
                try
                {
                    vocabularyData = await File.ReadAllTextAsync(args[4]);
                    Console.WriteLine($"Loaded vocabulary data from: {Path.GetFullPath(args[4])}");

                    // Validate JSON
                    var jsonDoc = JsonDocument.Parse(vocabularyData);
                    Console.WriteLine("Vocabulary data successfully validated as JSON.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not read or validate vocabulary file: {ex.Message}");
                }
            }

            // Read summarization prompt
            var promptPath = "data/prompt.txt";
            if (!File.Exists(promptPath))
            {
                Console.WriteLine("Prompt file is missing in 'data/prompt.txt'.");
                return;
            }
            string prompt = await File.ReadAllTextAsync(promptPath);
            Console.WriteLine($"Using prompt file: {Path.GetFullPath(promptPath)}");

            Console.WriteLine("\nProcessing input files...");

            // Build the full input text
            string fullInput = $"{prompt}\n\n" +
                               $"Target Language: {targetLanguage}\n" +
                               $"Source Language: {sourceLanguage}\n\n";

            if (!string.IsNullOrWhiteSpace(captureText))
            {
                fullInput += $"Capture Text:\n{captureText}\n\n";
            }

            fullInput += $"Speech Transcript:\n{speechText}";

            if (!string.IsNullOrEmpty(vocabularyData) && vocabularyData != "[]")
            {
                fullInput += $"\n\nVocabulary Data:\n{vocabularyData}";
            }

            if (!string.IsNullOrWhiteSpace(fullInput))
            {
                Console.WriteLine("Generating summary. Please wait...");

                var summary = await summarizationService.SummarizeTextAsync(fullInput);

                // Output directory
                string outputDir = "../AgentData/Summary";

                if (!Directory.Exists(outputDir))
                {
                    Console.WriteLine($"Creating output directory: {outputDir}");
                    Directory.CreateDirectory(outputDir);
                }

                // Timestamped output filename
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string outputPath = Path.Combine(outputDir, $"summary_JSON.json");

                // Wrap into structured object
                var summaryResult = new SummaryResult
                {
                    Timestamp = timestamp,
                    Summary = summary
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonOutput = JsonSerializer.Serialize(summaryResult, jsonOptions);
                string textOutputPath = Path.Combine(outputDir, $"summary_text.txt");

                await File.WriteAllTextAsync(outputPath, jsonOutput);
                await File.WriteAllTextAsync(textOutputPath, summary);

                Console.WriteLine("\n===================================");
                Console.WriteLine($"✅ Summary successfully generated!");
                Console.WriteLine($"📄 Output file: {Path.GetFullPath(outputPath)}");
                Console.WriteLine($"📝 Text summary: {Path.GetFullPath(textOutputPath)}");
                Console.WriteLine("===================================\n");
                Console.WriteLine($"Summary content:\n{summary}");
            }
            else
            {
                Console.WriteLine("Input from files is empty.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}