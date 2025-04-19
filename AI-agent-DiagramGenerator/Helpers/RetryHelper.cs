using System;
using System.Threading.Tasks;

namespace DiagramGenerator.Helpers
{
    /// <summary>
    /// Helper class for implementing retry logic for operations that might fail temporarily
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Executes an async function with retry logic
        /// </summary>
        /// <typeparam name="T">Type of the result</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="initialDelayMs">Initial delay in milliseconds</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <returns>Result of the operation</returns>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation, 
            int maxRetries = 3, 
            int initialDelayMs = 2000,
            IProgress<int>? progress = null)
        {
            int retryCount = 0;
            int delayMs = initialDelayMs;
            
            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    retryCount++;
                    
                    if (retryCount > maxRetries)
                    {
                        Console.WriteLine($"Operation failed after {maxRetries} retries.");
                        throw;
                    }
                    
                    // Log the error but continue with retry
                    Console.WriteLine($"Operation failed, retrying ({retryCount}/{maxRetries}): {ex.Message}");
                    
                    // Report progress if available
                    progress?.Report(50 + retryCount * 10); // Arbitrary progress reporting
                    
                    // Exponential backoff with jitter
                    var jitter = new Random().Next(-500, 500);
                    await Task.Delay(delayMs + jitter);
                    delayMs *= 2; // Double the delay for next retry
                }
            }
        }
    }
}
