using AI_Agent_Orchestrator.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;
using System.Text.Json;

namespace AI_Agent_Orchestrator.Services;

public class AgentExecutionService
{
    private readonly ILogger<AgentExecutionService> _logger;

    public AgentExecutionService(ILogger<AgentExecutionService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ExecuteAgentAsync(AgentInfo agent)
    {
        _logger.LogInformation($"Executing agent: {agent.Name}");

        try
        {
            // Verify the working directory exists
            if (!Directory.Exists(agent.WorkingDirectory))
            {
                _logger.LogError($"Working directory not found: {agent.WorkingDirectory}");
                string errorMsg = await TranslationHelper.TranslateAsync($"Error: Working directory not found: {agent.WorkingDirectory}");
                Console.WriteLine(errorMsg);
                return false;
            }

            // First approach: Use batch file to launch the agent with environment variables
            string batchFilePath = CreateTemporaryBatchFile(agent);

            // Create process info for the batch file, which can use shell execute
            var startInfo = new ProcessStartInfo
            {
                FileName = batchFilePath,
                WorkingDirectory = agent.WorkingDirectory,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            // Start the process
            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                _logger.LogError($"Failed to start agent: {agent.Name}");
                return false;
            }

            _logger.LogInformation($"Agent started with PID: {process.Id}");

            string agentName = await TranslationHelper.TranslateAsync(agent.Name);
            string startedMsg = await TranslationHelper.TranslateAsync($"Agent {agentName} started (PID: {process.Id})");
            string workingDirMsg = await TranslationHelper.TranslateAsync($"Working directory: {agent.WorkingDirectory}");
            string pressKeyMsg = await TranslationHelper.TranslateAsync("Press any key in this window to return when done...");

            Console.WriteLine(startedMsg);
            Console.WriteLine(workingDirMsg);
            Console.WriteLine(pressKeyMsg);

            // Wait for user to acknowledge before continuing
            await Task.Run(() => Console.ReadKey(true));

            // Check if process is still running
            if (!process.HasExited)
            {
                string stillRunningMsg = await TranslationHelper.TranslateAsync("Agent is still running. Wait for it to complete or press any key again to force return.");
                Console.WriteLine(stillRunningMsg);
                await Task.Run(() => Console.ReadKey(true));
            }

            // Check exit code if available
            if (process.HasExited)
            {
                _logger.LogInformation($"Agent {agent.Name} completed with exit code: {process.ExitCode}");

                // Display the summary content if this was the summarization agent
                if (agent.Name.Contains("Summarization Agent", StringComparison.OrdinalIgnoreCase))
                {
                    await DisplaySummaryContentAsync(agent.WorkingDirectory);
                }

                return process.ExitCode == 0;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing agent {agent.Name}");
            string errorMsg = await TranslationHelper.TranslateAsync($"Error executing agent: {ex.Message}");
            Console.WriteLine(errorMsg);
            return false;
        }
    }

    private async Task DisplaySummaryContentAsync(string workingDirectory)
    {
        try
        {
            // Find the most recent summary file in the output directory
            string outputDir = Path.Combine(workingDirectory, "../AgentData/Summary");
            if (!Directory.Exists(outputDir))
            {
                await TranslationHelper.MarkupLineAsync("[yellow]No summary output directory found.[/]");
                return;
            }

            var summaryFiles = Directory.GetFiles(outputDir, "summary_JSON.json")
                                       .OrderByDescending(f => new FileInfo(f).CreationTime)
                                       .ToArray();

            if (summaryFiles.Length == 0)
            {
                await TranslationHelper.MarkupLineAsync("[yellow]No summary files found in output directory.[/]");
                return;
            }

            var latestSummaryFile = summaryFiles[0];
            await TranslationHelper.MarkupLineAsync($"[green]Found summary file:[/] {latestSummaryFile}");

            // Read and parse the summary file
            string jsonContent = await File.ReadAllTextAsync(latestSummaryFile);
            using var document = JsonDocument.Parse(jsonContent);

            if (document.RootElement.TryGetProperty("Summary", out var summaryElement))
            {
                string summary = summaryElement.GetString() ?? "No summary content available";

                // Translate the summary
                string translatedSummary = await TranslationHelper.TranslateAsync(summary);
                string translatedHeader = await TranslationHelper.TranslateAsync("Summary Content");

                // Display the summary in a panel with formatting
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(translatedSummary)
                    .Header(translatedHeader)
                    .Expand()
                    .BorderColor(Color.Green)
                    .RoundedBorder());
                AnsiConsole.WriteLine();
            }
            else
            {
                await TranslationHelper.MarkupLineAsync("[yellow]Could not find Summary property in the JSON file.[/]");
            }
        }
        catch (Exception ex)
        {
            await TranslationHelper.MarkupLineAsync($"[red]Error displaying summary: {ex.Message}[/]");
            _logger.LogError(ex, "Error displaying summary content");
        }
    }

    public async Task<bool> ExecuteWorkflowAsync(AgentWorkflow workflow)
    {
        _logger.LogInformation($"Executing workflow: {workflow.Name}");

        try
        {
            // Dictionary to track generated files from each agent
            var generatedFiles = new Dictionary<string, List<string>>();

            for (int i = 0; i < workflow.Agents.Count; i++)
            {
                var agent = workflow.Agents[i];
                bool isLastAgent = (i == workflow.Agents.Count - 1);

                // Special case handling for summarization agent
                if (agent.Name.Contains("AI Summarization Agent") && i > 0)
                {
                    // Check for the existence of the required files
                    string translatedTranscriptPath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Recording", "translated_transcript.txt"));
                    string capturedImageDir = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Captures"));
                    string vocabularyDataPath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Vocabulary", "recognized_transcript_flashcards.json"));

                    // Verify the files exist before adding them to arguments
                    if (File.Exists(translatedTranscriptPath) && workflow.Name.Contains("Audio"))
                    {
                        AnsiConsole.MarkupLine($"[green]Found translated transcript at:[/] {translatedTranscriptPath}");
                        // Update the agent arguments to use the translated transcript
                        agent.Arguments.RemoveAll(arg => arg.Contains("translated_transcript.txt"));
                        agent.Arguments.Add(translatedTranscriptPath);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] Translated transcript not found at expected location: {translatedTranscriptPath}");
                    }

                    // If the whiteboard workflow is executed, use the latest captured image text
                    if (Directory.Exists(capturedImageDir) && workflow.Name.Contains("Whiteboard"))
                    {
                        AnsiConsole.MarkupLine($"[green]Using latest captured image text file:[/] {capturedImageDir}");

                        // Find the latest image text file
                        var imageTextFiles = Directory.GetFiles(capturedImageDir, "capture_*.txt")
                                                    .Where(f => !f.Contains(".analysis") && f.EndsWith(".txt"))
                                                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                                                    .ToArray();

                        if (imageTextFiles.Count() > 0)
                        {
                            string latestImageTextFile = imageTextFiles[0];

                            // Separate the original and translated texts into separate files
                            string translatedTextFile = Path.Combine(capturedImageDir, "translated_text.txt");

                            try
                            {
                                // Read the content of the latest image text file
                                string[] lines = await File.ReadAllLinesAsync(latestImageTextFile);

                                // Extract the translated text
                                var translatedTextLines = lines.SkipWhile(line => !line.StartsWith("TRANSLATED TEXT", StringComparison.OrdinalIgnoreCase))
                                                                .Skip(1) // Skip the "TRANSLATED TEXT" line
                                                                .ToList();

                                // Write the translated text to a file
                                await File.WriteAllLinesAsync(translatedTextFile, translatedTextLines);
                                AnsiConsole.MarkupLine($"[green]Translated text written to:[/] {translatedTextFile}");

                                agent.Arguments.RemoveAll(arg => arg.Contains("capture"));
                                agent.Arguments.Add(translatedTextFile);
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[red]Error separating texts:[/] {ex.Message}");
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]Warning:[/] No image text files found in {capturedImageDir}");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] Captured image directory not found: {capturedImageDir}");
                    }

                    if (File.Exists(vocabularyDataPath))
                    {
                        AnsiConsole.MarkupLine($"[green]Found vocabulary data at:[/] {vocabularyDataPath}");
                        // Update the agent arguments to use the vocabulary data
                        agent.Arguments.RemoveAll(arg => arg.Contains("flashcards.json"));
                        agent.Arguments.Add(vocabularyDataPath);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] Vocabulary data not found at expected location: {vocabularyDataPath}");
                    }
                    agent.Arguments.Add(workflow.TargetLanguage);
                    agent.Arguments.Add(string.IsNullOrEmpty(workflow.SourceLanguage) ? "en" : workflow.SourceLanguage);
                }

                // Special case handling for diagram generator agent
                if (agent.Name.Contains("Diagram Generator") && i > 0)
                {
                    // Find the latest summary file from the summarization agent
                    string summaryDir = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AI-Summarization-agent", "../AgentData/Summary"));
                    string translatedTranscriptPath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Recording", "translated_transcript.txt"));
                    string translatedImageTextPath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Captures", "translated_text.txt"));

                    if (Directory.Exists(summaryDir))
                    {
                        var summaryFiles = Directory.GetFiles(summaryDir, "summary_JSON.json")
                                                  .OrderByDescending(f => new FileInfo(f).CreationTime)
                                                  .ToArray();

                        if (summaryFiles.Length > 0)
                        {
                            string latestSummaryFile = summaryFiles[0];

                            // Create a symbolic link to the latest summary for consistency in naming
                            string latestSummaryLink = Path.Combine(summaryDir, "latest_summary.json");

                            try
                            {
                                // Remove existing link if it exists
                                if (File.Exists(latestSummaryLink))
                                {
                                    File.Delete(latestSummaryLink);
                                }

                                // On Windows, create a hard link as symbolic links require admin privileges
                                File.Copy(latestSummaryFile, latestSummaryLink, true);

                                AnsiConsole.MarkupLine($"[green]Created link to latest summary:[/] {latestSummaryLink}");

                                // Update the agent arguments to use the latest summary
                                agent.Arguments.RemoveAll(arg => arg.Contains("summary"));
                                agent.Arguments.Add(latestSummaryLink);
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not create link to latest summary: {ex.Message}");
                                // Add the direct file as fallback
                                agent.Arguments.RemoveAll(arg => arg.Contains("summary"));
                                agent.Arguments.Add(latestSummaryFile);
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]Warning:[/] No summary files found in {summaryDir}");
                        }
                    }

                    // Also add translated transcript
                    if (File.Exists(translatedTranscriptPath) || File.Exists(translatedImageTextPath))
                    {
                        // Check if the translated transcript file exists
                        var translationPath = workflow.Name.Contains("Audio") ? translatedTranscriptPath : translatedImageTextPath;
                        AnsiConsole.MarkupLine($"[green]Found translated transcript for diagram generation:[/] {translationPath}");

                        if (!agent.Arguments.Contains(translationPath))
                        {
                            agent.Arguments.RemoveAll(arg => arg.Contains("translate"));
                            agent.Arguments.Add(translationPath);
                        }
                    }
                }

                AnsiConsole.MarkupLine($"\n[bold cyan]Executing workflow step {i + 1}/{workflow.Agents.Count}:[/] [green]{agent.Name}[/]");

                // Execute the agent
                bool success = await ExecuteAgentAsync(agent);

                if (!success)
                {
                    _logger.LogError($"Workflow step failed: {agent.Name}");
                    AnsiConsole.MarkupLine($"[bold red]Workflow step failed:[/] {agent.Name}");

                    if (!PromptContinueWorkflow())
                    {
                        return false;
                    }
                }

                // Track generated files based on agent type
                CollectGeneratedFiles(agent, generatedFiles);

                // If not the last agent, pass output to the next agent if we have a mapping
                if (!isLastAgent && workflow.OutputMappings.ContainsKey(agent.Name))
                {
                    var outputPaths = workflow.OutputMappings[agent.Name];
                    var nextAgent = workflow.Agents[i + 1];

                    _logger.LogInformation($"Passing output from {agent.Name} to {nextAgent.Name}");
                    AnsiConsole.MarkupLine($"\n[bold blue]Passing output from {agent.Name} to {nextAgent.Name}[/]");

                    foreach (var outputPath in outputPaths)
                    {
                        // Resolve output path relative to agent's working directory
                        string fullOutputPath = Path.IsPathRooted(outputPath) ?
                            outputPath : Path.GetFullPath(Path.Combine(agent.WorkingDirectory, outputPath));

                        // Check if the output file exists
                        if (File.Exists(fullOutputPath))
                        {
                            // Add the file to the next agent's arguments
                            if (!nextAgent.Arguments.Contains(fullOutputPath))
                            {
                                nextAgent.Arguments.Add(fullOutputPath);
                                AnsiConsole.MarkupLine($"[dim]Added file to next agent: {fullOutputPath}[/]");
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Output file not found at {fullOutputPath}");
                        }
                    }
                }
            }

            AnsiConsole.MarkupLine($"\n[bold green]Workflow completed successfully:[/] {workflow.Name}");

            // Display summary of generated files for Complete Audio Learning Assistant workflow
            if (workflow.Name.Contains("Complete Audio Learning Assistant"))
            {
                await DisplayWorkflowOutputSummary(generatedFiles);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing workflow {workflow.Name}");
            AnsiConsole.MarkupLine($"[bold red]Error executing workflow:[/] {ex.Message}");
            return false;
        }
    }

    private void CollectGeneratedFiles(AgentInfo agent, Dictionary<string, List<string>> generatedFiles)
    {
        try
        {
            // Check the Speech Translator output directory for all generated files
            string speechTranslatorOutputDir = Path.Combine(
                GetSolutionRootDirectory(),
                "..",
                "AgentData",
                "Recording");

            _logger.LogInformation($"Checking for files in Speech Translator output directory: {speechTranslatorOutputDir}");

            if (agent.Name.Contains("Speech Translator"))
            {
                if (Directory.Exists(speechTranslatorOutputDir))
                {
                    var files = new List<string>();
                    var recognizedTranscript = Path.Combine(speechTranslatorOutputDir, "recognized_transcript.txt");
                    var translatedTranscript = Path.Combine(speechTranslatorOutputDir, "translated_transcript.txt");

                    if (File.Exists(recognizedTranscript)) files.Add(recognizedTranscript);
                    if (File.Exists(translatedTranscript)) files.Add(translatedTranscript);

                    if (files.Count > 0)
                    {
                        generatedFiles["Speech Translator"] = files;
                    }
                }
            }
            else if (agent.Name.Contains("Vocabulary Bank"))
            {
                // First check the Speech Translator output directory as noted by the user
                if (Directory.Exists(speechTranslatorOutputDir))
                {
                    var flashcardsFile = Path.Combine(speechTranslatorOutputDir, "..", "Vocabulary", "recognized_transcript_flashcards.json");
                    if (File.Exists(flashcardsFile))
                    {
                        generatedFiles["Vocabulary Bank"] = new List<string> { flashcardsFile };
                        _logger.LogInformation($"Found Vocabulary Bank output in Speech Translator directory: {flashcardsFile}");
                    }
                }

                // Also check in Vocabulary Bank's own output directory as a backup
                string vocabularyOutputDir = Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Vocabulary");
                if (Directory.Exists(vocabularyOutputDir))
                {
                    var files = Directory.GetFiles(vocabularyOutputDir, "*flashcards*.json")
                        .OrderByDescending(f => new FileInfo(f).CreationTime)
                        .ToList();

                    if (files.Count > 0)
                    {
                        if (!generatedFiles.ContainsKey("Vocabulary Bank"))
                        {
                            generatedFiles["Vocabulary Bank"] = files;
                        }
                        else
                        {
                            generatedFiles["Vocabulary Bank"].AddRange(files);
                        }
                        _logger.LogInformation($"Found Vocabulary Bank output in its own directory: {string.Join(", ", files)}");
                    }
                }
            }
            else if (agent.Name.Contains("Summarization Agent"))
            {
                string outputDir = Path.Combine(agent.WorkingDirectory, "../AgentData/Summary");
                if (Directory.Exists(outputDir))
                {
                    var files = Directory.GetFiles(outputDir, "summary_JSON.json")
                                        .OrderByDescending(f => new FileInfo(f).CreationTime)
                                        .Take(1)
                                        .ToList();

                    if (files.Count > 0)
                    {
                        generatedFiles["Summarization Agent"] = files;
                    }
                }
            }
            else if (agent.Name.Contains("Diagram Generator"))
            {
                // First check the Speech Translator output directory as noted by the user
                if (Directory.Exists(speechTranslatorOutputDir))
                {
                    var diagramFiles = new List<string>();

                    // Look for diagram files with various patterns in Speech Translator Output
                    var diagramMd = Path.Combine(speechTranslatorOutputDir, "translated_transcript_diagram.md");
                    var diagramJson = Path.Combine(speechTranslatorOutputDir, "translated_transcript_diagram.json");
                    var diagramMindmap = Path.Combine(speechTranslatorOutputDir, "*mindmap*.md");

                    if (File.Exists(diagramMd)) diagramFiles.Add(diagramMd);
                    if (File.Exists(diagramJson)) diagramFiles.Add(diagramJson);

                    // Find any files matching pattern
                    diagramFiles.AddRange(
                        Directory.GetFiles(speechTranslatorOutputDir, "*diagram*.md")
                        .Union(Directory.GetFiles(speechTranslatorOutputDir, "*diagram*.json"))
                        .Union(Directory.GetFiles(speechTranslatorOutputDir, "*mindmap*.md"))
                    );

                    if (diagramFiles.Count > 0)
                    {
                        generatedFiles["Diagram Generator"] = diagramFiles.Distinct().ToList();
                        _logger.LogInformation($"Found Diagram Generator output in Speech Translator directory: {string.Join(", ", diagramFiles)}");
                    }
                }

                // Also check in Diagram Generator's own directories
                string[] outputDirs = new[] {
                    Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Summary"),
                    Path.Combine(agent.WorkingDirectory, "Diagrams"),
                    Path.Combine(agent.WorkingDirectory, "..", "AgentData", "Summary"),
                    agent.WorkingDirectory
                };

                List<string> files = new List<string>();
                foreach (var dir in outputDirs.Where(Directory.Exists))
                {
                    files.AddRange(Directory.GetFiles(dir, "*.png"));
                    files.AddRange(Directory.GetFiles(dir, "*.svg"));
                    files.AddRange(Directory.GetFiles(dir, "*.pdf"));
                    files.AddRange(Directory.GetFiles(dir, "*diagram*.md"));
                    files.AddRange(Directory.GetFiles(dir, "*mindmap*.md"));
                }

                if (files.Count > 0)
                {
                    files = files.OrderByDescending(f => new FileInfo(f).CreationTime).Take(5).ToList();
                    if (!generatedFiles.ContainsKey("Diagram Generator"))
                    {
                        generatedFiles["Diagram Generator"] = files;
                    }
                    else
                    {
                        generatedFiles["Diagram Generator"].AddRange(files);
                        generatedFiles["Diagram Generator"] = generatedFiles["Diagram Generator"].Distinct().ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error collecting generated files for {agent.Name}");
        }
    }

    private async Task DisplayWorkflowOutputSummary(Dictionary<string, List<string>> generatedFiles)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule(await TranslationHelper.TranslateMarkupTextAsync("[bold blue]Complete Audio Learning Workflow - Generated Documents[/]"))
            .LeftJustified()
            .RuleStyle("blue dim"));
        AnsiConsole.WriteLine();

        // Always check for output files for all agents in the Speech Translator output dir
        EnsureAllAgentsHaveFiles(generatedFiles);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.BorderColor(Color.Blue);
        table.AddColumn(new TableColumn(await TranslationHelper.TranslateMarkupTextAsync("[green]Agent[/]")));
        table.AddColumn(new TableColumn(await TranslationHelper.TranslateMarkupTextAsync("[green]Generated Files[/]")).Centered());

        // Sort the keys to display in a consistent order
        var orderedAgents = new List<string> {
            "Speech Translator",
            "Vocabulary Bank",
            "Summarization Agent",
            "Diagram Generator"
        };

        // Translate agent names
        var translatedAgentNames = await TranslationHelper.TranslateListAsync(orderedAgents);
        var agentNameMap = orderedAgents.Zip(translatedAgentNames, (original, translated) => new { Original = original, Translated = translated })
                                      .ToDictionary(x => x.Original, x => x.Translated);

        bool isFirstAgent = true;
        foreach (var agentName in orderedAgents)
        {
            if (generatedFiles.ContainsKey(agentName) && generatedFiles[agentName].Count > 0)
            {
                // Add separator between agents (except before the first agent)
                if (!isFirstAgent)
                {
                    table.AddRow(
                        new Markup("[dim]───────────────────────[/]"),
                        new Markup("[dim]───────────────────────────────────────────────[/]")
                    );
                }

                isFirstAgent = false;

                string translatedName = agentNameMap.ContainsKey(agentName) ? agentNameMap[agentName] : agentName;

                // Format the file paths differently depending on the agent
                if (agentName == "Speech Translator")
                {
                    // Special handling for Speech Translator files
                    var fileRows = new List<string>();
                    foreach (var file in generatedFiles[agentName])
                    {
                        string labelKey = "";
                        if (file.Contains("recognized_transcript"))
                            labelKey = "Original Text";
                        else if (file.Contains("translated_transcript"))
                            labelKey = "Translated Text";

                        string translatedLabel = await TranslationHelper.TranslateAsync(labelKey);
                        fileRows.Add($"[bold blue]{translatedLabel}:[/] [cyan]{Path.GetFullPath(file)}[/]");
                    }

                    table.AddRow(new Markup($"[yellow]{translatedName}[/]"), new Markup(string.Join("\n", fileRows)));
                }
                else
                {
                    // Standard formatting for other agents
                    var filesList = string.Join("\n", generatedFiles[agentName].Select(f => $"[cyan]{Path.GetFullPath(f)}[/]"));
                    table.AddRow(new Markup($"[yellow]{translatedName}[/]"), new Markup(filesList));
                }
            }
        }

        if (table.Rows.Count == 0)
        {
            await TranslationHelper.MarkupLineAsync("[yellow]No output files were found for this workflow.[/]");
        }
        else
        {
            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
    }

    private void EnsureAllAgentsHaveFiles(Dictionary<string, List<string>> generatedFiles)
    {
        // Check Speech Translator output directory for all agent outputs
        string speechTranslatorOutputDir = Path.Combine(
            GetSolutionRootDirectory(),
            "AgentData",
            "Recording");

        if (Directory.Exists(speechTranslatorOutputDir))
        {
            // Look for Vocabulary Bank output
            if (!generatedFiles.ContainsKey("Vocabulary Bank"))
            {
                var flashcardsFile = Path.Combine(speechTranslatorOutputDir, "..", "Vocabulary", "recognized_transcript_flashcards.json");
                if (File.Exists(flashcardsFile))
                {
                    generatedFiles["Vocabulary Bank"] = new List<string> { flashcardsFile };
                    _logger.LogInformation($"Added missing Vocabulary Bank output: {flashcardsFile}");
                }
            }

            // Look for Diagram Generator output
            if (!generatedFiles.ContainsKey("Diagram Generator"))
            {
                var diagramFiles = new List<string>();
                var diagramMd = Path.Combine(speechTranslatorOutputDir, "..", "Diagram", "translated_transcript_diagram.md");

                if (File.Exists(diagramMd))
                {
                    diagramFiles.Add(diagramMd);
                }
                else
                {
                    // Try to find any diagram files
                    diagramFiles.AddRange(
                        Directory.GetFiles(speechTranslatorOutputDir, "*diagram*.md")
                        .Union(Directory.GetFiles(speechTranslatorOutputDir, "*diagram*.json"))
                        .Union(Directory.GetFiles(speechTranslatorOutputDir, "*mindmap*.md"))
                    );
                }

                if (diagramFiles.Count > 0)
                {
                    generatedFiles["Diagram Generator"] = diagramFiles;
                    _logger.LogInformation($"Added missing Diagram Generator output: {string.Join(", ", diagramFiles)}");
                }
            }
        }
    }

    private string GetSolutionRootDirectory()
    {
        // Start with the directory of the executing assembly
        string currentDir = AppDomain.CurrentDomain.BaseDirectory;

        // Navigate upwards to find the solution root
        // Usually 3 levels up from bin/Debug/netX.X
        string rootDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".."));

        // If we're already at the solution root, the parent directory is the repo root
        return Path.GetFullPath(Path.Combine(rootDir, ".."));
    }

    private bool PromptContinueWorkflow()
    {
        return AnsiConsole.Confirm("Do you want to continue with the next workflow step?", false);
    }

    private string CreateTemporaryBatchFile(AgentInfo agent)
    {
        // Create a temporary batch file to run the agent with correct environment variables
        string tempDir = Path.Combine(Path.GetTempPath(), "AI-Agent-Orchestrator");
        Directory.CreateDirectory(tempDir);

        string batchFilePath = Path.Combine(tempDir, $"run-agent-{Guid.NewGuid()}.bat");

        using (var writer = new StreamWriter(batchFilePath))
        {
            writer.WriteLine("@echo off");
            writer.WriteLine("setlocal");

            // Set all environment variables from the current process
            foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                writer.WriteLine($"set {env.Key}={env.Value}");
            }

            // Set custom environment variables for this agent
            foreach (var envVar in agent.EnvironmentVariables)
            {
                writer.WriteLine($"set {envVar.Key}={envVar.Value}");
            }

            // Build the command to execute with all arguments
            string command = agent.ExecutablePath;
            foreach (var arg in agent.Arguments)
            {
                if (arg.Contains(" "))
                    command += $" \"{arg}\"";
                else
                    command += $" {arg}";
            }

            writer.WriteLine($"cd /d {agent.WorkingDirectory}");
            writer.WriteLine(command);
            writer.WriteLine("if %ERRORLEVEL% NEQ 0 pause");
            writer.WriteLine("endlocal");
        }

        return batchFilePath;
    }
}
