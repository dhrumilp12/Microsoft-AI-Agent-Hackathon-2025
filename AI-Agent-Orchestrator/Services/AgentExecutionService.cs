using AI_Agent_Orchestrator.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;

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
    
    public async Task<bool> ExecuteWorkflowAsync(AgentWorkflow workflow)
    {
        _logger.LogInformation($"Executing workflow: {workflow.Name}");
        
        try
        {
            for (int i = 0; i < workflow.Agents.Count; i++)
            {
                var agent = workflow.Agents[i];
                bool isLastAgent = (i == workflow.Agents.Count - 1);
                
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
                    var outputPath = workflow.OutputMappings[agent.Name];
                    var nextAgent = workflow.Agents[i + 1];
                    
                    _logger.LogInformation($"Passing output from {agent.Name} to {nextAgent.Name}");
                    AnsiConsole.MarkupLine($"\n[bold blue]Passing output from {agent.Name} to {nextAgent.Name}[/]");
                    
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
