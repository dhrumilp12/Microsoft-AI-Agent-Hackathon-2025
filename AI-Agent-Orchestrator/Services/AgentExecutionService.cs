using AI_Agent_Orchestrator.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

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
            Console.ReadKey(true);
            
            // Check if process is still running
            if (!process.HasExited)
            {
                Console.WriteLine("Agent is still running. Wait for it to complete or press any key again to force return.");
                Console.ReadKey(true);
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
