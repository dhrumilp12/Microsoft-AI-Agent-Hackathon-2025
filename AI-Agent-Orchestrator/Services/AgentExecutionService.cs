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
                Console.WriteLine($"Error: Working directory not found: {agent.WorkingDirectory}");
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
            Console.WriteLine($"Agent {agent.Name} started (PID: {process.Id})");
            Console.WriteLine($"Working directory: {agent.WorkingDirectory}");
            Console.WriteLine("Press any key in this window to return when done...");
            
            // Wait for user to acknowledge before continuing
            await Task.Run(() => Console.ReadKey(true));
            
            // Check if process is still running
            if (!process.HasExited)
            {
                Console.WriteLine("Agent is still running. Wait for it to complete or press any key again to force return.");
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
            Console.WriteLine($"Error executing agent: {ex.Message}");
            return false;
        }
    }
    
    private async Task DisplaySummaryContentAsync(string workingDirectory)
    {
        try
        {
            // Find the most recent summary file in the output directory
            string outputDir = Path.Combine(workingDirectory, "data", "outputs");
            if (!Directory.Exists(outputDir))
            {
                AnsiConsole.MarkupLine("[yellow]No summary output directory found.[/]");
                return;
            }
            
            var summaryFiles = Directory.GetFiles(outputDir, "summary_*.json")
                                       .OrderByDescending(f => new FileInfo(f).CreationTime)
                                       .ToArray();
            
            if (summaryFiles.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No summary files found in output directory.[/]");
                return;
            }
            
            var latestSummaryFile = summaryFiles[0];
            AnsiConsole.MarkupLine($"[green]Found summary file:[/] {latestSummaryFile}");
            
            // Read and parse the summary file
            string jsonContent = await File.ReadAllTextAsync(latestSummaryFile);
            using var document = JsonDocument.Parse(jsonContent);
            
            if (document.RootElement.TryGetProperty("Summary", out var summaryElement))
            {
                string summary = summaryElement.GetString() ?? "No summary content available";
                
                // Display the summary in a panel with formatting
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(summary)
                    .Header("Summary Content")
                    .Expand()
                    .BorderColor(Color.Green)
                    .RoundedBorder());
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Could not find Summary property in the JSON file.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error displaying summary: {ex.Message}[/]");
            _logger.LogError(ex, "Error displaying summary content");
        }
    }
    
    public async Task<bool> ExecuteWorkflowAsync(AgentWorkflow workflow)
    {
        _logger.LogInformation($"Executing workflow: {workflow.Name}");
        
        try
        {
            for (int i = 0; i < workflow.Agents.Count; i++)
            {
                var agent = workflow.Agents[i];
                bool isLastAgent = (i == workflow.Agents.Count - 1);

                if (agent.Name.Contains("Vocabulary") && workflow.Name.Contains("Whiteboard"))
                {
                    string capturedImageDir = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AI-Agent-BoardCapture", "Captures"));
                    
                    // If the whiteboard workflow is executed, use the latest captured image text
                    if (Directory.Exists(capturedImageDir) && workflow.Name.Contains("Whiteboard"))
                    {
                        AnsiConsole.MarkupLine($"[green]Using latest captured image text file:[/] {capturedImageDir}");
                        
                        // Find the latest image text file
                        var imageTextFiles = Directory.GetFiles(capturedImageDir, "capture_*.txt")
                                                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                                                    .ToArray();

                        if (imageTextFiles.Count() > 0)
                        {
                            string latestImageTextFile = imageTextFiles[0];
                            
                            // Create a symbolic link to the latest image text file for consistency in naming
                            string latestImageTextLink = Path.Combine(capturedImageDir, "latest_captured_image_text.txt");
                            
                            try
                            {
                                // Remove existing link if it exists
                                if (File.Exists(latestImageTextLink))
                                {
                                    File.Delete(latestImageTextLink);
                                }
                                
                                // On Windows, create a hard link as symbolic links require admin privileges
                                File.Copy(latestImageTextFile, latestImageTextLink, true);

                                AnsiConsole.MarkupLine($"[green]Created link to latest image text:[/] {latestImageTextLink}");

                                agent.Arguments.RemoveAll(arg => arg.Contains("capture"));
                                agent.Arguments.Add(latestImageTextLink);
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not create link to latest image text: {ex.Message}");
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
                }
                
                // Special case handling for summarization agent
                if (agent.Name.Contains("AI Summarization Agent") && i > 0)
                {
                    // Check for the existence of the required files
                    string translatedTranscriptPath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AI-agent-SpeechTranslator", "Output", "translated_transcript.txt"));
                    string capturedImagePath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AI-Agent-BoardCapture", "Captures", "latest_captured_image_text.txt"));
                    string vocabularyDataPath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AI-agent-SpeechTranslator", "Output", "recognized_transcript_flashcards.json"));
                    
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
                    if (File.Exists(capturedImagePath) && workflow.Name.Contains("Whiteboard"))
                    {
                        AnsiConsole.MarkupLine($"[green]Using latest captured image text file:[/] {capturedImageDir}");
                        
                        agent.Arguments.RemoveAll(arg => arg.Contains("capture"));
                        agent.Arguments.Add(capturedImagePath);
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
                }
                
                // Special case handling for diagram generator agent
                if (agent.Name.Contains("Diagram Generator") && i > 0)
                {
                    // Find the latest summary file from the summarization agent
                    string summaryDir = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AI-Summarization-agent", "data", "outputs"));
                    string translatedTranscriptPath = Path.GetFullPath(Path.Combine(agent.WorkingDirectory, "..", "AI-agent-SpeechTranslator", "Output", "translated_transcript.txt"));
                    
                    if (Directory.Exists(summaryDir))
                    {
                        var summaryFiles = Directory.GetFiles(summaryDir, "summary_*.json")
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
                    if (File.Exists(translatedTranscriptPath))
                    {
                        AnsiConsole.MarkupLine($"[green]Found translated transcript for diagram generation:[/] {translatedTranscriptPath}");
                        
                        if (!agent.Arguments.Contains(translatedTranscriptPath))
                        {
                            agent.Arguments.RemoveAll(arg => arg.Contains("transcript"));
                            agent.Arguments.Add(translatedTranscriptPath);
                        }
                    }
                }
                
                AnsiConsole.MarkupLine($"\n[bold cyan]Executing workflow step {i+1}/{workflow.Agents.Count}:[/] [green]{agent.Name}[/]");
                
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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing workflow {workflow.Name}");
            AnsiConsole.MarkupLine($"[bold red]Error executing workflow:[/] {ex.Message}");
            return false;
        }
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
