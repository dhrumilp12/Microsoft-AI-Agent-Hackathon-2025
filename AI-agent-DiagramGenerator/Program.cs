using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using DiagramGenerator.Services;
using DiagramGenerator.Helpers;

namespace DiagramGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load environment variables
            DotNetEnv.Env.Load();
            
            // Setup configuration
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();
            
            // Setup DI container
            var services = new ServiceCollection();
            ConfigureServices(services, configuration);
            var serviceProvider = services.BuildServiceProvider();
            
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var openAIService = serviceProvider.GetRequiredService<AzureOpenAIService>();
            
            try
            {
                logger.LogInformation("Starting Visual Diagram Generator");
                
                // Validate and process the manifest file and additional arguments
                var filePaths = new List<string>();

                if (args.Length > 0)
                {
                    foreach (var arg in args)
                    {
                        if (File.Exists(arg))
                        {
                            if (!arg.EndsWith(".json"))
                            {
                                filePaths.Add(arg);
                            }
                            else
                            {
                                if (arg.Contains("manifest")) {
                                    string manifestContent = await File.ReadAllTextAsync(arg);
                                    var manifestEntries = JsonSerializer.Deserialize<List<dynamic>>(manifestContent);

                                    if (manifestEntries != null)
                                    {
                                        filePaths.AddRange(manifestEntries.Select(entry => entry.FileName));
                                    }
                                }
                            }
                        }
                    }
                }

                if (filePaths.Count == 0)
                {
                    logger.LogError("No valid files or manifest entries provided.");
                    return;
                }

                foreach (var filePath in filePaths.Distinct())
                {
                    if (!File.Exists(filePath))
                    {
                        logger.LogError($"File not found: {filePath}");
                        continue;
                    }

                    logger.LogInformation($"Processing file: {filePath}");

                    // Process transcript and generate diagram
                    string transcript = await File.ReadAllTextAsync(filePath);
                    logger.LogInformation($"Processing transcript from {filePath}");

                    var conceptExtractionProgress = new Progress<int>(percent => 
                    {
                        Console.Write($"\rExtracting concepts: {percent}% complete");
                        if (percent >= 100) Console.WriteLine();
                    });

                    var concepts = await openAIService.ExtractConcepts(transcript, conceptExtractionProgress);
                    logger.LogInformation($"Extracted {concepts.Count} key concepts");

                    string diagramType = GetDiagramTypeFromUser();

                    var diagramProgress = new Progress<int>(percent => 
                    {
                        Console.Write($"\rGenerating {diagramType} diagram: {percent}% complete");
                        if (percent >= 100) Console.WriteLine();
                    });

                    var diagram = await openAIService.GenerateDiagram(concepts, diagramType, diagramProgress);

                    // Save and display the diagram
                    string outputPath = await SaveDiagramToFile(diagram, filePath, configuration);
                    logger.LogInformation($"Diagram saved to {outputPath}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred");
                Console.WriteLine($"Error: {ex.Message}");
                
                // Show recovery options
                Console.WriteLine("\nHow would you like to proceed?");
                Console.WriteLine("1. Try again with a smaller input");
                Console.WriteLine("2. Exit application");
                
                if (Console.ReadLine() == "1")
                {
                    // Recursively try again with a smaller input
                    string filePath = args.Length > 0 ? args[0] : PromptForFilePath();
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        string transcript = await File.ReadAllTextAsync(filePath);
                        // Truncate the transcript if it's too long
                        if (transcript.Length > 3000)
                        {
                            string truncatedFile = Path.GetTempFileName();
                            await File.WriteAllTextAsync(truncatedFile, transcript.Substring(0, 3000));
                            Console.WriteLine($"Trying again with truncated input...");
                            await Main(new[] { truncatedFile });
                            return;
                        }
                    }
                }
            }
        }
        
        private static void ConfigureServices(ServiceCollection services, IConfiguration configuration)
        {
            // Configure Serilog for file logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "logs", "application.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            services.AddLogging(configure => configure.AddSerilog())
                .Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
                
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<AzureOpenAIService>();
            services.AddSingleton<DiagramRenderer>();  // Add the new renderer service
        }
        
        private static string GetDiagramTypeFromUser()
        {
            Console.WriteLine("\nSelect diagram type:");
            Console.WriteLine("1. Mind Map (default)");
            Console.WriteLine("2. Flowchart");
            Console.WriteLine("3. Sequence Diagram");
            Console.Write("Enter your choice (1-3): ");
            
            string? input = Console.ReadLine();
            return input switch
            {
                "2" => "flowchart",
                "3" => "sequence",
                _ => "mindmap"
            };
        }
        
        private static async Task<string> SaveDiagramToFile(string diagram, string transcriptPath, IConfiguration configuration)
        {
            // Get the directory where the transcript file is located
            string outputDir = Path.GetDirectoryName(transcriptPath) ?? "./";
            
            // Create directory if it doesn't exist (should already exist, but just in case)
            Directory.CreateDirectory(outputDir);
            
            string fileName = Path.GetFileNameWithoutExtension(transcriptPath);
            string outputPath = Path.Combine(outputDir, $"{fileName}_diagram.md");
            
            await File.WriteAllTextAsync(outputPath, diagram);
            return outputPath;
        }
        
        private static async Task HandleUserInteraction(
            string diagram, 
            List<ConceptNode> concepts,
            AzureOpenAIService openAIService, 
            string outputPath,
            string inputFilePath)  // Added inputFilePath parameter
        {
            bool exit = false;
            string currentDiagramType = "mindmap";
            
            // Get the directory where the input file is located
            string workingDirectory = Path.GetDirectoryName(inputFilePath) ?? "./";
            
            while (!exit)
            {
                Console.WriteLine("\nOptions:");
                Console.WriteLine("1. View diagram");
                Console.WriteLine("2. Expand a specific concept");
                Console.WriteLine("3. Generate a different diagram type");
                Console.WriteLine("4. Export diagram to HTML");
                Console.WriteLine("5. Exit");
                Console.Write("Enter your choice (1-5): ");
                
                string? choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        Console.WriteLine("\nDiagram (Mermaid syntax):");
                        Console.WriteLine(diagram);
                        Console.WriteLine("\nOpen the saved file in a Mermaid-compatible viewer to see the rendered diagram.");
                        break;
                        
                    case "2":
                        await ExpandConcept(concepts, openAIService, currentDiagramType, workingDirectory);
                        break;
                        
                    case "3":
                        string diagramType = GetDiagramTypeFromUser();
                        currentDiagramType = diagramType;
                        
                        var progress = new Progress<int>(percent => 
                        {
                            Console.Write($"\rGenerating {diagramType} diagram: {percent}% complete");
                            if (percent >= 100) Console.WriteLine();
                        });
                        
                        var newDiagram = await openAIService.GenerateDiagram(concepts, diagramType, progress);
                        await File.WriteAllTextAsync(outputPath, newDiagram);
                        diagram = newDiagram;
                        Console.WriteLine($"New diagram generated and saved to {outputPath}");
                        break;
                        
                    case "4":
                        await ExportDiagram(diagram, openAIService, outputPath);
                        break;
                        
                    case "5":
                        exit = true;
                        break;
                        
                    default:
                        Console.WriteLine("Invalid choice, please try again.");
                        break;
                }
            }
        }
        
        private static async Task ExpandConcept(
            List<ConceptNode> concepts, 
            AzureOpenAIService openAIService,
            string diagramType,
            string workingDirectory = null)  // Added workingDirectory parameter
        {
            // List all available concepts for selection
            Console.WriteLine("\nAvailable concepts:");
            for (int i = 0; i < concepts.Count; i++)
            {
                Console.WriteLine($"{i+1}. {concepts[i].Name}");
            }
            
            Console.Write("Enter the concept number or name you want to expand: ");
            string? input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input)) return;
            
            ConceptNode? targetConcept = null;
            // Try parsing as number first
            if (int.TryParse(input, out int conceptNumber) && conceptNumber > 0 && conceptNumber <= concepts.Count)
            {
                targetConcept = concepts[conceptNumber - 1];
            }
            else
            {
                // Try finding by name
                targetConcept = concepts.FirstOrDefault(c => 
                    c.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
            }
            
            if (targetConcept == null)
            {
                Console.WriteLine($"Concept '{input}' not found");
                return;
            }
            
            string conceptName = targetConcept.Name;
            
            try
            {
                Console.WriteLine($"\nExpanding concept: {conceptName}");
                
                var progress = new Progress<int>(percent => 
                {
                    Console.Write($"\rExpanding concept: {percent}% complete");
                    if (percent >= 100) Console.WriteLine();
                });
                
                string expandedDiagram = await openAIService.ExpandConcept(conceptName, targetConcept, diagramType, progress);
                Console.WriteLine("\nExpanded Diagram:");
                Console.WriteLine(expandedDiagram);
                
                // Save the expanded diagram to the working directory if provided, otherwise use default
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string fileName = FileNameHelper.SanitizeFileName(conceptName);
                string outputDir = !string.IsNullOrEmpty(workingDirectory) ? workingDirectory : "./diagrams";
                Directory.CreateDirectory(outputDir);
                
                string outputPath = Path.Combine(outputDir, $"expanded_{fileName}_{timestamp}.md");
                await File.WriteAllTextAsync(outputPath, expandedDiagram);
                Console.WriteLine($"Expanded diagram saved to {outputPath}");
                
                // Ask if user wants to export the expanded diagram
                Console.Write("Would you like to export this as HTML? (y/n): ");
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    string htmlPath = Path.Combine(outputDir, $"expanded_{fileName}_{timestamp}.html");
                    
                    // Get the service provider to access the renderer
                    var serviceProvider = new ServiceCollection()
                        .AddLogging(configure => configure.AddConsole())
                        .AddSingleton<DiagramRenderer>()
                        .BuildServiceProvider();
                        
                    var renderer = serviceProvider.GetRequiredService<DiagramRenderer>();
                    
                    // Use the renderer for better compatibility
                    string htmlDiagram = await renderer.RenderDiagramAsHtml(expandedDiagram, $"Expanded: {conceptName}");
                    
                    await File.WriteAllTextAsync(htmlPath, htmlDiagram);
                    Console.WriteLine($"HTML diagram saved to {htmlPath}");
                    
                    // Try to open the HTML file in the default browser
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = htmlPath,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        Console.WriteLine($"Couldn't automatically open the file. Please open {htmlPath} in your browser.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error expanding concept: {ex.Message}");
            }
        }
        
        private static async Task ExportDiagram(
            string diagram, 
            AzureOpenAIService openAIService,
            string outputPath)
        {
            // Get the directory where the diagram file is located
            string outputDir = Path.GetDirectoryName(outputPath) ?? "./";
            
            Console.WriteLine("\nSelect export format:");
            Console.WriteLine("1. HTML (better compatibility)");
            Console.WriteLine("2. Return to previous menu");
            Console.Write("Enter your choice: ");
            
            string? choice = Console.ReadLine();
            
            if (choice == "1")
            {
                Console.WriteLine("Exporting diagram to HTML...");
                
                string htmlPath = Path.Combine(
                    outputDir,
                    Path.GetFileNameWithoutExtension(outputPath) + "_simple.html"
                );
                
                // Get the service provider to access the renderer
                var serviceProvider = new ServiceCollection()
                    .AddLogging(configure => configure.AddConsole())
                    .AddSingleton<DiagramRenderer>()
                    .BuildServiceProvider();
                    
                var renderer = serviceProvider.GetRequiredService<DiagramRenderer>();
                
                // Use the simpler renderer for better compatibility
                string htmlDiagram = await renderer.RenderDiagramAsHtml(diagram, "Lecture Diagram");
                
                await File.WriteAllTextAsync(htmlPath, htmlDiagram);
                Console.WriteLine($"HTML diagram saved to {htmlPath}");
                
                // Try to open the HTML file in the default browser
                try
                {
                    Console.WriteLine("Attempting to open diagram in your browser...");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = htmlPath,
                        UseShellExecute = true
                    });
                    Console.WriteLine("Browser opened successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Couldn't automatically open the browser: {ex.Message}");
                    Console.WriteLine($"Please open {htmlPath} manually in your browser.");
                }
            }
        }
        
        private static string PromptForFilePath()
        {
            Console.WriteLine("Please enter the path to a transcript file:");
            string? path = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("No file specified. Exiting.");
                return string.Empty;
            }
            
            // Remove quotes if they were added by drag-and-drop or copy-paste
            path = path.Trim('"');
            
            // Check if file exists
            if (!File.Exists(path))
            {
                Console.WriteLine($"File not found: {path}");
                return PromptForFilePath(); // Recursively ask again
            }
            
            return path;
        }
    }
    
    public class ConceptNode
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<ConceptRelationship> Relationships { get; set; } = new();
        public int Importance { get; set; }
        public string Style { get; set; } = "";  // Added styling property
    }
    
    public class ConceptRelationship
    {
        public string Type { get; set; } = "";
        public string Target { get; set; } = "";
    }
}
