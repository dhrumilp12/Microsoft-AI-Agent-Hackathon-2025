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
            Env.Load();

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

            // Define file paths for transcript and prompt input
            var transcriptPath = "data/transcript2.txt";
            var promptPath = "data/prompt.txt";

            // Check if input files are present
            if (!File.Exists(transcriptPath) || !File.Exists(promptPath))
            {
                Console.WriteLine("One or both input files are missing in the 'data' folder.");
                return;
            }

            // Read the contents of both input files
            string transcript = await File.ReadAllTextAsync(transcriptPath);
            string prompt = await File.ReadAllTextAsync(promptPath);

            // Combine the prompt and transcript into a single input string
            string fullInput = $"{prompt}\n\n{transcript}";

            if (!string.IsNullOrWhiteSpace(fullInput))
            {
                // Send the full input to the summarization service
                var summary = await summarizationService.SummarizeTextAsync(fullInput);

                // Define output directory and make sure it exists
                string outputDir = "data/outputs";

                // Create a timestamp for the output filename
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

                // Generate a path for the JSON output file
                string outputPath = Path.Combine(outputDir, $"summary_{timestamp}.json");

                // Wrap the summary and timestamp into a strongly typed object
                var summaryResult = new SummaryResult
                {
                    Timestamp = timestamp,
                    Summary = summary
                };

                // Set JSON formatting options for readability
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                // Convert the result object to a formatted JSON string
                string jsonOutput = JsonSerializer.Serialize(summaryResult, jsonOptions);

                // Save the JSON summary to file
                await File.WriteAllTextAsync(outputPath, jsonOutput);

                Console.WriteLine($"\nSummary saved to JSON:\n{summary}");
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
