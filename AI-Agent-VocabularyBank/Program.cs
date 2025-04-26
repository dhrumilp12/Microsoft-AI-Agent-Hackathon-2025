using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using VocabularyBank.Models;
using VocabularyBank.Services;
using VocabularyBank.Helpers;
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
            DotNetEnv.Env.Load(@"../.env");

            // Set up console display
            Console.Title = "Vocabulary Bank & Flashcards Generator";
            Console.ForegroundColor = ConsoleColor.White;
            
            // Try to clear the console, but don't fail if there's no valid console
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
                // No valid console handle, possibly running as a child process with redirected output
                // Just continue without clearing
            }
            
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
                
                // Check if we have a second argument which is likely the translated file
                string translatedPath = null;
                if (args.Length > 1 && File.Exists(args[1]))
                {
                    translatedPath = args[1];
                }
                
                // Offer translation option
                TranslatedContent translatedContent = await PromptForTranslationAsync(transcript, transcriptPath, serviceProvider, translatedPath);
                if (translatedContent != null)
                {
                    // Process both original and translated text for vocabulary extraction
                    DisplayProcessingStage("Processing both original and translated text");
                }
                
                // Extract vocabulary terms with progress indicator
                DisplayProcessingStage("Extracting key vocabulary terms");
                var vocabularyExtractor = serviceProvider.GetService<IVocabularyExtractorService>();
                
                List<string> vocabularyTerms;
                using (var extractionProgressBar = new ConsoleProgressBar("Analyzing text for key terms..."))
                {
                    vocabularyTerms = await vocabularyExtractor.ExtractVocabularyAsync(
                        transcript,
                        (percent, message) => extractionProgressBar.Report(percent, message));
                        
                    extractionProgressBar.Complete();
                }
                
                Console.WriteLine($"✓ Successfully extracted {vocabularyTerms.Count} key terms from original text.");
                
                // Display extracted terms
                if (vocabularyTerms.Count > 0)
                {
                    DisplayExtractedTerms(vocabularyTerms);
                }
                
                // If we have translated content, extract vocabulary from it too
                List<string> translatedVocabularyTerms = null;
                if (translatedContent != null)
                {
                    DisplayProcessingStage($"Extracting key vocabulary terms from translated text ({translatedContent.TargetLanguageDisplayName})");
                    
                    using (var translatedExtractionProgressBar = new ConsoleProgressBar($"Analyzing {translatedContent.TargetLanguageDisplayName} text for key terms..."))
                    {
                        translatedVocabularyTerms = await vocabularyExtractor.ExtractVocabularyAsync(
                            translatedContent.TranslatedText,
                            (percent, message) => translatedExtractionProgressBar.Report(percent, message));
                            
                        translatedExtractionProgressBar.Complete();
                    }
                    
                    Console.WriteLine($"✓ Successfully extracted {translatedVocabularyTerms.Count} key terms from translated text.");
                    
                    if (translatedVocabularyTerms.Count > 0)
                    {
                        Console.WriteLine($"\nExtracted key terms from {translatedContent.TargetLanguageDisplayName} text:");
                        DisplayExtractedTerms(translatedVocabularyTerms);
                    }
                }
                
                // Generate definitions and examples with progress indicator
                DisplayProcessingStage("Generating definitions and examples for original text");
                var definitionGenerator = serviceProvider.GetService<IDefinitionGeneratorService>();
                
                Console.WriteLine("Fetching definitions from AI service (this may take a moment)...");
                
                // Create and initialize progress bar
                using (var progressBar = new ConsoleProgressBar("Initializing..."))
                {
                    // Generate definitions with progress bar
                    var termsWithDefinitions = await definitionGenerator.GenerateDefinitionsAsync(
                        vocabularyTerms, 
                        transcript,
                        (percent, message) => progressBar.Report(percent, message));
                    
                    // Mark the progress as complete
                    progressBar.Complete();
                    
                    Console.WriteLine($"✓ Successfully generated {termsWithDefinitions.Count} term definitions for original text.");
                
                    // Generate definitions for translated terms if available
                    List<VocabularyTerm> translatedTermsWithDefinitions = null;
                    if (translatedContent != null && translatedVocabularyTerms != null && translatedVocabularyTerms.Count > 0)
                    {
                        DisplayProcessingStage($"Generating definitions and examples for {translatedContent.TargetLanguageDisplayName} text");
                        Console.WriteLine("Fetching definitions from AI service for translated terms...");
                        
                        using (var translatedProgressBar = new ConsoleProgressBar("Initializing..."))
                        {
                            translatedTermsWithDefinitions = await definitionGenerator.GenerateDefinitionsAsync(
                                translatedVocabularyTerms, 
                                translatedContent.TranslatedText,
                                (percent, message) => translatedProgressBar.Report(percent, message));
                                
                            translatedProgressBar.Complete();
                        }
                        
                        Console.WriteLine($"✓ Successfully generated {translatedTermsWithDefinitions.Count} term definitions for translated text.");
                    }
                    
                    // Create flashcards with progress indicator
                    DisplayProcessingStage("Creating flashcards for original text");
                    var flashcardGenerator = serviceProvider.GetService<IFlashcardGeneratorService>();
                    
                    List<Flashcard> flashcards;
                    using (var flashcardProgressBar = new ConsoleProgressBar("Creating flashcards..."))
                    {
                        flashcards = flashcardGenerator.CreateFlashcards(
                            termsWithDefinitions,
                            (percent, message) => flashcardProgressBar.Report(percent, message));
                            
                        flashcardProgressBar.Complete();
                    }
                    
                    Console.WriteLine($"✓ Created {flashcards.Count} flashcards for original text.");
                    
                    // Create flashcards for translated terms if available
                    List<Flashcard> translatedFlashcards = null;
                    if (translatedTermsWithDefinitions != null)
                    {
                        DisplayProcessingStage($"Creating flashcards for {translatedContent.TargetLanguageDisplayName} text");
                        
                        using (var translatedFlashcardProgressBar = new ConsoleProgressBar("Creating translated flashcards..."))
                        {
                            translatedFlashcards = flashcardGenerator.CreateFlashcards(
                                translatedTermsWithDefinitions,
                                (percent, message) => translatedFlashcardProgressBar.Report(percent, message));
                                
                            translatedFlashcardProgressBar.Complete();
                        }
                        
                        Console.WriteLine($"✓ Created {translatedFlashcards.Count} flashcards for translated text.");
                    }
                    
                    // Export flashcards with options
                    string outputPath = await PromptForExportOptions(
                        transcriptPath, 
                        serviceProvider, 
                        flashcards,
                        translatedFlashcards,
                        translatedContent?.TargetLanguageDisplayName);
                    
                    DisplayProcessingStage("Exporting flashcards", outputPath);
                    
                    var exportService = serviceProvider.GetService<IExportService>();
                    if (translatedFlashcards != null && translatedFlashcards.Count > 0)
                    {
                        // Export both original and translated flashcards
                        await exportService.ExportCombinedFlashcardsAsync(
                            flashcards, 
                            translatedFlashcards, 
                            translatedContent.TargetLanguageDisplayName, 
                            outputPath);
                    }
                    else
                    {
                        // Export only original flashcards
                        await exportService.ExportFlashcardsAsync(flashcards, outputPath);
                    }
                    
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
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("╔════════════════════════════════════════════╗");
                Console.WriteLine("║  Vocabulary Bank & Flashcards Generator    ║");
                Console.WriteLine("╚════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine("\nAutomatically extract and define key terms from educational content");
            }
            catch (IOException)
            {
                // Fallback for redirected console
                Console.WriteLine("== Vocabulary Bank & Flashcards Generator ==");
                Console.WriteLine("Automatically extract and define key terms from educational content");
            }
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
        /// <param name="translatedFlashcards">The list of translated flashcards (optional)</param>
        /// <param name="targetLanguageName">Name of the target language (if translation was performed)</param>
        /// <returns>The chosen output file path</returns>
        private static async Task<string> PromptForExportOptions(
            string transcriptPath, 
            ServiceProvider serviceProvider, 
            List<Flashcard> flashcards,
            List<Flashcard> translatedFlashcards = null,
            string targetLanguageName = null)
        {
            bool hasTranslatedCards = translatedFlashcards != null && translatedFlashcards.Count > 0;
            
            // Default output path
            string defaultPath = Path.Combine(
                Path.GetDirectoryName(transcriptPath),
                "..","Vocabulary", 
                Path.GetFileNameWithoutExtension(transcriptPath) + "_flashcards.json"
            );
            
            Console.WriteLine("\nSelect export format:");
            Console.WriteLine("  1. JSON format (default)");
            Console.WriteLine("  2. CSV format");
            Console.WriteLine("  3. HTML format");
            Console.WriteLine("  4. Export to Microsoft 365");
            
            Console.Write("Enter your choice (1-4): ");
            string formatChoice = Console.ReadLine().Trim();
            
            // If we have translated flashcards, ask about export options
            if (hasTranslatedCards)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nYou have flashcards in both original language and {targetLanguageName}.");
                Console.WriteLine("How would you like to export them?");
                Console.ResetColor();
                Console.WriteLine("  1. Combined in one file (default)");
                Console.WriteLine("  2. As separate files");
                
                Console.Write("Enter your choice (1-2): ");
                string exportChoice = Console.ReadLine().Trim();
                
                if (exportChoice == "2")
                {
                    // Handle separate files
                    string outputPathOriginal = defaultPath;
                    string outputPathTranslated = Path.Combine(
                        Path.GetDirectoryName(defaultPath),
                        $"{Path.GetFileNameWithoutExtension(defaultPath)}_{targetLanguageName.ToLower()}{Path.GetExtension(defaultPath)}"
                    );
                    
                    Console.WriteLine($"\nExporting original flashcards to: {outputPathOriginal}");
                    Console.WriteLine($"Exporting {targetLanguageName} flashcards to: {outputPathTranslated}");
                    
                    // Allow customizing paths
                    Console.Write($"\nOutput file path for original flashcards [{outputPathOriginal}]: ");
                    string customPathOriginal = Console.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(customPathOriginal))
                    {
                        outputPathOriginal = customPathOriginal;
                    }
                    
                    Console.Write($"Output file path for {targetLanguageName} flashcards [{outputPathTranslated}]: ");
                    string customPathTranslated = Console.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(customPathTranslated))
                    {
                        outputPathTranslated = customPathTranslated;
                    }
                    
                    // Apply correct extensions
                    string fileExtension = formatChoice == "2" ? ".csv" : formatChoice == "3" ? ".html" : ".json";
                    if (!Path.HasExtension(outputPathOriginal))
                    {
                        outputPathOriginal = Path.ChangeExtension(outputPathOriginal, fileExtension);
                    }
                    if (!Path.HasExtension(outputPathTranslated))
                    {
                        outputPathTranslated = Path.ChangeExtension(outputPathTranslated, fileExtension);
                    }
                    
                    // Ensure directories exist
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPathOriginal));
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPathTranslated));
                    
                    // Export both sets of flashcards separately
                    var exportService = serviceProvider.GetService<IExportService>();
                    await exportService.ExportFlashcardsAsync(flashcards, outputPathOriginal);
                    await exportService.ExportFlashcardsAsync(translatedFlashcards, outputPathTranslated);
                    
                    return outputPathOriginal; // Return the original path for the success message
                }
            }
            
            // Handle M365 export option
            if (formatChoice == "4")
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
            
            string extension;
            switch (formatChoice)
            {
                case "2":
                    extension = ".csv";
                    break;
                case "3":
                    extension = ".html";
                    break;
                default:
                    extension = ".json";
                    break;
            }
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
            services.AddTransient<ITranslationService, AzureTranslationService>(); // Add translation service
            
            return services;
        }

        /// <summary>
        /// Prompts the user if they want to translate the transcript and handles the translation process.
        /// </summary>
        /// <param name="transcript">The original transcript text</param>
        /// <param name="transcriptPath">The path to the original transcript file</param>
        /// <param name="serviceProvider">The service provider for dependency injection</param>
        /// <param name="translatedPath">Optional path to an existing translated file</param>
        /// <returns>TranslatedContent if translation was performed, null otherwise</returns>
        private static async Task<TranslatedContent> PromptForTranslationAsync(
            string transcript, 
            string transcriptPath, 
            ServiceProvider serviceProvider, 
            string translatedPath = null)
        {
            // If we already have a translated path provided via arguments (from the workflow),
            // automatically use it without prompting
            if (!string.IsNullOrEmpty(translatedPath) && File.Exists(translatedPath))
            {
                Console.WriteLine($"Using provided translated file: {translatedPath}");
                
                // Get the translation service
                var translationSvc = serviceProvider.GetService<ITranslationService>();
                
                // Load the translated content
                string translatedFileContent = await File.ReadAllTextAsync(translatedPath);
                
                if (string.IsNullOrWhiteSpace(translatedFileContent))
                {
                    Console.WriteLine("Warning: Translated file is empty. Proceeding with original text only.");
                    return null;
                }
                
                // Detect the languages
                DisplayProcessingStage("Detecting source language");
                string sourceLang = await translationSvc.DetectLanguageAsync(transcript);
                string translatedLang = await translationSvc.DetectLanguageAsync(translatedFileContent);
                
                // Get available languages
                var availableLanguages = await translationSvc.GetAvailableLanguagesAsync();
                
                // Determine language name
                string translatedLanguageName = availableLanguages.ContainsKey(translatedLang) 
                    ? availableLanguages[translatedLang]
                    : translatedLang;
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Using original text in {sourceLang} and translated text in {translatedLanguageName}!");
                Console.ResetColor();
                
                // Return the translated content
                return new TranslatedContent
                {
                    OriginalText = transcript,
                    OriginalLanguage = sourceLang,
                    TranslatedText = translatedFileContent,
                    TargetLanguage = translatedLang,
                    TargetLanguageDisplayName = translatedLanguageName
                };
            }
            
            // Otherwise proceed with the normal prompt flow
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nHow would you like to handle translation?");
            Console.ResetColor();
            Console.WriteLine("  1. Translate the text automatically");
            Console.WriteLine("  2. Upload an existing translated file");
            Console.WriteLine("  3. No translation, keep original language only");
            
            Console.Write("\nEnter your choice (1-3): ");
            string choice = Console.ReadLine().Trim();
            
            // User chose not to translate
            if (choice == "3")
            {
                return null;
            }
            
            // Get the translation service
            var translationService = serviceProvider.GetService<ITranslationService>();
            
            // Detect the source language of the original text
            DisplayProcessingStage("Detecting source language");
            string detectedLanguage = await translationService.DetectLanguageAsync(transcript);
            
            // Get available languages
            var languages = await translationService.GetAvailableLanguagesAsync();
            
            // Handle option to upload an existing translation
            if (choice == "2")
            {
                return await LoadExistingTranslationAsync(transcript, detectedLanguage, languages);
            }
            
            // Proceed with automatic translation
            DisplayProcessingStage("Retrieving available languages");
            
            // Display language options in columns
            Console.WriteLine("\nAvailable target languages:");
            DisplayLanguageOptions(languages, detectedLanguage);
            
            // Prompt for target language
            Console.Write("\nEnter the language code to translate to: ");
            string targetLanguage = Console.ReadLine().Trim();
            
            // Validate the language code
            if (!languages.ContainsKey(targetLanguage))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Language code '{targetLanguage}' not recognized. Using 'en' (English) as default.");
                Console.ResetColor();
                targetLanguage = "en";
            }
            
            // Perform the translation
            DisplayProcessingStage($"Translating text to {languages[targetLanguage]}");
            Console.WriteLine("This may take a moment for larger texts...");
            
            string translatedText = await translationService.TranslateTextAsync(transcript, targetLanguage);
            
            if (string.IsNullOrEmpty(translatedText))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Translation failed. Continuing with original text only.");
                Console.ResetColor();
                return null;
            }
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Text successfully translated to {languages[targetLanguage]}!");
            Console.ResetColor();
            
            // Get the directory of the transcript file to save the translated file in the same location
            string outputDirectory = Path.Combine(Path.GetDirectoryName(transcriptPath), "..", "Vocabulary");
            Console.WriteLine($"Output directory path {outputDirectory}");
            // Save the translated text to a file in the same location as the transcript
            string translatedFilePath = await SaveTranslatedTextAsync(translatedText, targetLanguage, languages[targetLanguage], outputDirectory);
            Console.WriteLine($"Translated text saved to: {translatedFilePath}");
            
            // Return the translated content
            return new TranslatedContent
            {
                OriginalText = transcript,
                OriginalLanguage = detectedLanguage,
                TranslatedText = translatedText,
                TargetLanguage = targetLanguage,
                TargetLanguageDisplayName = languages[targetLanguage]
            };
        }
        
        /// <summary>
        /// Handles loading an existing translation file provided by the user
        /// </summary>
        /// <param name="originalText">The original untranslated text</param>
        /// <param name="originalLanguage">The detected language of the original text</param>
        /// <param name="availableLanguages">Dictionary of available languages</param>
        /// <returns>TranslatedContent if successful, null otherwise</returns>
        private static async Task<TranslatedContent> LoadExistingTranslationAsync(
            string originalText, 
            string originalLanguage, 
            Dictionary<string, string> availableLanguages)
        {
            Console.WriteLine("\nPlease provide the path to your translated text file:");
            Console.Write("> ");
            string translatedFilePath = Console.ReadLine().Trim('"', ' ');
            
            if (string.IsNullOrEmpty(translatedFilePath) || !File.Exists(translatedFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: File not found at '{translatedFilePath}'");
                Console.WriteLine("Translation process cancelled. Continuing with original text only.");
                Console.ResetColor();
                return null;
            }
            
            try
            {
                // Load the translated text
                string translatedText = await File.ReadAllTextAsync(translatedFilePath);
                
                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: The translated file is empty.");
                    Console.WriteLine("Translation process cancelled. Continuing with original text only.");
                    Console.ResetColor();
                    return null;
                }
                
                // Ask user to specify the language of the translation
                Console.WriteLine("\nWhat language is the translated text in?");
                
                // Show the full list of available languages using the same display method as for automatic translation
                Console.WriteLine("Available target languages:");
                DisplayLanguageOptions(availableLanguages, originalLanguage);
                
                Console.Write("\nEnter the language code: ");
                string targetLanguage = Console.ReadLine().Trim();
                
                // Validate the language code
                if (!availableLanguages.ContainsKey(targetLanguage))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Language code '{targetLanguage}' not recognized. Using 'en' (English) as default.");
                    Console.ResetColor();
                    targetLanguage = "en";
                }
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Successfully loaded translated text in {availableLanguages[targetLanguage]}!");
                Console.ResetColor();
                
                return new TranslatedContent
                {
                    OriginalText = originalText,
                    OriginalLanguage = originalLanguage,
                    TranslatedText = translatedText,
                    TargetLanguage = targetLanguage,
                    TargetLanguageDisplayName = availableLanguages[targetLanguage]
                };
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error loading translated file: {ex.Message}");
                Console.WriteLine("Translation process cancelled. Continuing with original text only.");
                Console.ResetColor();
                return null;
            }
        }
        
        /// <summary>
        /// Displays available language options in a formatted multi-column layout.
        /// </summary>
        /// <param name="languages">Dictionary of language codes and names</param>
        /// <param name="detectedLanguage">The detected source language to highlight</param>
        private static void DisplayLanguageOptions(Dictionary<string, string> languages, string detectedLanguage)
        {
            const int columns = 3;
            var sortedLanguages = languages.OrderBy(l => l.Value).ToList();
            
            for (int i = 0; i < sortedLanguages.Count; i += columns)
            {
                for (int j = 0; j < columns && i + j < sortedLanguages.Count; j++)
                {
                    var language = sortedLanguages[i + j];
                    string display = $"{language.Key}: {language.Value}";
                    
                    // Highlight detected source language
                    if (language.Key == detectedLanguage)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"{display.PadRight(30)} (detected) ");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.Write(display.PadRight(30));
                    }
                }
                Console.WriteLine();
            }
        }
        
        /// <summary>
        /// Saves translated text to a file.
        /// </summary>
        /// <param name="translatedText">The translated text</param>
        /// <param name="languageCode">The language code</param>
        /// <param name="languageName">The display name of the language</param>
        /// <param name="outputDirectory">The directory where the file should be saved</param>
        /// <returns>Path to the saved file</returns>
        private static async Task<string> SaveTranslatedTextAsync(
            string translatedText, 
            string languageCode, 
            string languageName, 
            string outputDirectory = null)
        {
            string fileName = $"translated_{languageCode}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            
            // If no output directory is specified, use the current directory
            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = Directory.GetCurrentDirectory();
            }
            
            // Ensure the directory exists
            Directory.CreateDirectory(outputDirectory);
            
            string filePath = Path.Combine(outputDirectory, fileName);
            await File.WriteAllTextAsync(filePath, translatedText);
            return filePath;
        }
    }
}
