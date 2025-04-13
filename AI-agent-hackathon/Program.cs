using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using VocabularyBank.Models;
using VocabularyBank.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace VocabularyBank
{
    /// <summary>
    /// Main entry point for the Vocabulary Bank & Flashcards Generator application.
    /// This application extracts key terms from a transcript, generates definitions and examples,
    /// and exports them as flashcards for educational purposes.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load environment variables from .env file (ensure this is at the top)
            DotNetEnv.Env.Load();

            // Set up console display
            Console.Title = "Vocabulary Bank & Flashcards Generator";
            Console.ForegroundColor = ConsoleColor.White;
            Console.Clear();
            
            DisplayBanner();
            
            // Setup configuration and services
            var services = ConfigureServices();
            var serviceProvider = services.BuildServiceProvider();
            
            try
            {
                // Get file path from arguments or prompt user
                string transcriptPath = await GetTranscriptPathAsync(args);
                
                // Process the transcript
                DisplayProcessingStage("Processing transcript", transcriptPath);
                var transcriptProcessor = serviceProvider.GetService<ITranscriptProcessorService>();
                string transcript = await transcriptProcessor.LoadTranscriptAsync(transcriptPath);
                
                // Extract vocabulary terms with progress indicator
                DisplayProcessingStage("Extracting key vocabulary terms");
                var vocabularyExtractor = serviceProvider.GetService<IVocabularyExtractorService>();
                var vocabularyTerms = await vocabularyExtractor.ExtractVocabularyAsync(transcript);
                Console.WriteLine($"✓ Successfully extracted {vocabularyTerms.Count} key terms.");
                
                // Display extracted terms
                if (vocabularyTerms.Count > 0)
                {
                    DisplayExtractedTerms(vocabularyTerms);
                }
                
                // Generate definitions and examples with progress indicator
                DisplayProcessingStage("Generating definitions and examples");
                var definitionGenerator = serviceProvider.GetService<IDefinitionGeneratorService>();
                
                Console.WriteLine("Fetching definitions from AI service (this may take a moment)...");
                var termsWithDefinitions = await definitionGenerator.GenerateDefinitionsAsync(vocabularyTerms, transcript);
                Console.WriteLine($"✓ Successfully generated {termsWithDefinitions.Count} term definitions.");
                
                // Create flashcards with progress indicator
                DisplayProcessingStage("Creating flashcards");
                var flashcardGenerator = serviceProvider.GetService<IFlashcardGeneratorService>();
                var flashcards = flashcardGenerator.CreateFlashcards(termsWithDefinitions);
                Console.WriteLine($"✓ Created {flashcards.Count} flashcards.");
                
                // Export flashcards with options
                string outputPath = await PromptForExportOptions(transcriptPath, serviceProvider, flashcards);
                DisplayProcessingStage("Exporting flashcards", outputPath);
                
                var exportService = serviceProvider.GetService<IExportService>();
                await exportService.ExportFlashcardsAsync(flashcards, outputPath);
                
                // Display success message with highlighted output path
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Flashcards successfully exported to: {outputPath}");
                Console.ResetColor();
                
                // Offer to open the output file
                if (File.Exists(outputPath))
                {
                    Console.Write("\nWould you like to open the exported file? (Y/N): ");
                    var key = Console.ReadKey().Key;
                    if (key == ConsoleKey.Y)
                    {
                        Console.WriteLine();
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                // Display error message with red color
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError: {ex.Message}");
                Console.ResetColor();
            }
            
            // Exit prompt
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("\nPress any key to exit...");
            Console.ResetColor();
            Console.ReadKey();
        }
        
        /// <summary>
        /// Displays the application banner with formatting
        /// </summary>
        private static void DisplayBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║  Vocabulary Bank & Flashcards Generator    ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("\nAutomatically extract and define key terms from educational content");
        }
        
        /// <summary>
        /// Displays a processing stage header with consistent formatting
        /// </summary>
        /// <param name="stage">The name of the processing stage</param>
        /// <param name="details">Optional details about the stage</param>
        private static void DisplayProcessingStage(string stage, string details = null)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"► {stage}...");
            Console.ResetColor();
            
            if (!string.IsNullOrEmpty(details))
            {
                Console.WriteLine($"  {details}");
            }
        }
        
        /// <summary>
        /// Displays the extracted terms in a formatted list
        /// </summary>
        /// <param name="terms">The list of extracted vocabulary terms</param>
        private static void DisplayExtractedTerms(List<string> terms)
        {
            Console.WriteLine("\nExtracted key terms:");
            
            // Display terms in columns
            const int columns = 3;
            for (int i = 0; i < terms.Count; i += columns)
            {
                for (int j = 0; j < columns && i + j < terms.Count; j++)
                {
                    Console.Write($"{(i + j + 1).ToString().PadLeft(2)}: {terms[i + j].PadRight(20)} ");
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }
        
        /// <summary>
        /// Prompts the user to select a transcript file from various sources
        /// </summary>
        /// <param name="args">Command-line arguments that may contain a file path</param>
        /// <returns>The path to the selected transcript file</returns>
        private static async Task<string> GetTranscriptPathAsync(string[] args)
        {
            if (args.Length > 0 && File.Exists(args[0]))
            {
                return args[0];
            }
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nPlease select an option:");
            Console.ResetColor();
            
            Console.WriteLine("  1. Use sample transcript");
            Console.WriteLine("  2. Enter path to transcript file");
            Console.WriteLine("  3. Drag and drop a file");
            
            Console.Write("\nEnter your choice (1-3): ");
            string choice = Console.ReadLine().Trim();
            
            switch (choice)
            {
                case "1":
                    return await CreateSampleFileAsync();
                    
                case "2":
                    Console.Write("\nEnter the full path to the transcript file: ");
                    string filePath = Console.ReadLine().Trim('"', ' ');
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException($"Transcript file not found: {filePath}");
                    }
                    return filePath;
                    
                case "3":
                    Console.WriteLine("\nPlease drag and drop your file into this window now, then press Enter:");
                    string dragDropPath = Console.ReadLine().Trim('"', ' ');
                    if (string.IsNullOrEmpty(dragDropPath))
                    {
                        throw new FileNotFoundException("No file was provided.");
                    }
                    if (!File.Exists(dragDropPath))
                    {
                        throw new FileNotFoundException($"Transcript file not found: {dragDropPath}");
                    }
                    return dragDropPath;
                    
                default:
                    // Try to interpret the input as a direct file path
                    if (File.Exists(choice))
                    {
                        return choice;
                    }
                    else if (choice.ToLower() == "sample" || choice.ToLower() == "example")
                    {
                        return await CreateSampleFileAsync();
                    }
                    else
                    {
                        throw new FileNotFoundException("Invalid choice or file path not found.");
                    }
            }
        }
        
        /// <summary>
        /// Prompts the user for export options including format and file location
        /// </summary>
        /// <param name="transcriptPath">The path to the original transcript file</param>
        /// <param name="serviceProvider">The service provider for dependency injection</param>
        /// <param name="flashcards">The list of flashcards to export</param>
        /// <returns>The chosen output file path</returns>
        private static async Task<string> PromptForExportOptions(string transcriptPath, ServiceProvider serviceProvider, List<Flashcard> flashcards)
        {
            // Default output path
            string defaultPath = Path.Combine(
                Path.GetDirectoryName(transcriptPath), 
                Path.GetFileNameWithoutExtension(transcriptPath) + "_flashcards.json"
            );
            
            Console.WriteLine("\nSelect export format:");
            Console.WriteLine("  1. JSON format (default)");
            Console.WriteLine("  2. CSV format");
            Console.WriteLine("  3. Export to Microsoft 365");
            
            Console.Write("Enter your choice (1-3): ");
            string formatChoice = Console.ReadLine().Trim();
            
            // Handle M365 export option
            if (formatChoice == "3")
            {
                var exportService = serviceProvider.GetService<IExportService>();
                
                if (exportService.IsM365ExportAvailable())
                {
                    Console.Write("\nEnter your email address for M365 sharing: ");
                    string userEmail = Console.ReadLine().Trim();
                    
                    if (!string.IsNullOrEmpty(userEmail) && userEmail.Contains("@"))
                    {
                        try
                        {
                            string m365Url = await exportService.ExportToM365Async(flashcards, userEmail);
                            
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\n✓ Flashcards successfully exported to Microsoft 365!");
                            Console.WriteLine($"Access your flashcards at: {m365Url}");
                            Console.ResetColor();
                            
                            // Also save locally as a backup
                            string localPath = Path.ChangeExtension(defaultPath, ".json");
                            await exportService.ExportFlashcardsAsync(flashcards, localPath);
                            Console.WriteLine($"\nA local backup copy has been saved to: {localPath}");
                            
                            return localPath; // Return the local file path
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\nError exporting to Microsoft 365: {ex.Message}");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Falling back to local export...");
                            Console.ResetColor();
                            
                            // Fall back to JSON format
                            formatChoice = "1";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid email address. Falling back to local export.");
                        formatChoice = "1";
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nMicrosoft 365 export is not available. Please check your configuration.");
                    Console.WriteLine("To enable Microsoft 365 export, add the following to your appsettings.json or environment variables:");
                    Console.WriteLine("  - M365:ClientId");
                    Console.WriteLine("  - M365:TenantId");
                    Console.WriteLine("  - M365:ClientSecret");
                    Console.WriteLine("\nFalling back to local export...");
                    Console.ResetColor();
                    
                    formatChoice = "1";
                }
            }
            
            string extension = formatChoice == "2" ? ".csv" : ".json";
            string outputPath = Path.ChangeExtension(defaultPath, extension);
            
            Console.Write($"\nOutput file path [{outputPath}]: ");
            string customPath = Console.ReadLine().Trim();
            
            if (!string.IsNullOrEmpty(customPath))
            {
                outputPath = customPath;
                // Ensure the correct extension
                if (!Path.HasExtension(outputPath))
                {
                    outputPath = Path.ChangeExtension(outputPath, extension);
                }
            }
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            
            return outputPath;
        }
        
        /// <summary>
        /// Creates a sample transcript file for demonstration purposes
        /// </summary>
        /// <returns>Path to the created sample file</returns>
        private static async Task<string> CreateSampleFileAsync()
        {
            string samplePath = Path.Combine(Directory.GetCurrentDirectory(), "sample_transcript.txt");
            
            // Create a sample transcript file with educational content
            string sampleText = @"Introduction to Machine Learning

Machine learning is a field of artificial intelligence that uses statistical techniques to give computer systems the ability to 'learn' from data, without being explicitly programmed. The name machine learning was coined in 1959 by Arthur Samuel.

Neural networks are a set of algorithms, modeled loosely after the human brain, that are designed to recognize patterns. They interpret sensory data through a kind of machine perception, labeling or clustering raw input.

Supervised learning is the machine learning task of learning a function that maps an input to an output based on example input-output pairs. It infers a function from labeled training data.

Unsupervised learning is a type of machine learning algorithm used to draw inferences from datasets consisting of input data without labeled responses. The most common unsupervised learning method is cluster analysis.

Reinforcement learning is an area of machine learning concerned with how software agents ought to take actions in an environment so as to maximize some notion of cumulative reward.

The bias-variance tradeoff is a central problem in supervised learning. Ideally, one wants to choose a model that both accurately captures the regularities in its training data, but also generalizes well to unseen data.

Deep learning is part of a broader family of machine learning methods based on artificial neural networks with representation learning.";
            
            await File.WriteAllTextAsync(samplePath, sampleText);
            Console.WriteLine($"Created sample transcript file at: {samplePath}");
            
            return samplePath;
        }
        
        /// <summary>
        /// Configures the dependency injection services for the application
        /// </summary>
        /// <returns>Configured ServiceCollection</returns>
        private static IServiceCollection ConfigureServices()
        {
            IServiceCollection services = new ServiceCollection();
            
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables() // Add environment variables as configuration source
                .Build();
            
            // Register configuration
            services.AddSingleton<IConfiguration>(configuration);
            
            // Register services
            services.AddSingleton<AzureOpenAIService>();
            services.AddTransient<ITranscriptProcessorService, TranscriptProcessorService>();
            services.AddTransient<IVocabularyExtractorService, VocabularyExtractorService>();
            services.AddTransient<IDefinitionGeneratorService, DefinitionGeneratorService>();
            services.AddTransient<IFlashcardGeneratorService, FlashcardGeneratorService>();
            services.AddTransient<M365ExportService>();
            services.AddTransient<IExportService, ExportService>();
            
            return services;
        }
    }
}
