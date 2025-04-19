using System;
using System.Threading.Tasks;

namespace DiagramGenerator.Helpers
{
    /// <summary>
    /// Helper class for implementing retry logic for operations that might fail temporarily.
    /// Provides exponential backoff with jitter to handle transient failures in a resilient way.
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Executes an async function with retry logic using exponential backoff strategy.
        /// Each retry will double the delay time and add a random jitter to prevent concurrent retries.
        /// </summary>
        /// <typeparam name="T">Type of the result returned by the operation</typeparam>
        /// <param name="operation">The async operation to execute with retry logic</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
        /// <param name="initialDelayMs">Initial delay in milliseconds before first retry (default: 2000ms)</param>
        /// <param name="progress">Optional progress reporter for reporting retry progress</param>
        /// <returns>The result of the successful operation</returns>
        /// <exception cref="Exception">Throws the last encountered exception if all retries fail</exception>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation, 
            int maxRetries = 3, 
            int initialDelayMs = 2000,
            IProgress<int>? progress = null)
        {
            // Track retry attempts
            int retryCount = 0;
            int delayMs = initialDelayMs;
            
            while (true)
            {
                try
                {
                    // Attempt to execute the operation
                    return await operation();
                }
                catch (Exception ex)
                {
                    retryCount++;
                    
                    // If we've reached max retries, give up and rethrow
                    if (retryCount > maxRetries)
                    {
                        Console.WriteLine($"Operation failed after {maxRetries} retries.");
                        throw;
                    }
                    
                    // Log the error but continue with retry
                    Console.WriteLine($"Operation failed, retrying ({retryCount}/{maxRetries}): {ex.Message}");
                    
                    // Report progress if available - helps UI show something is happening
                    progress?.Report(50 + retryCount * 10);
                    
                    // Exponential backoff with jitter to prevent thundering herd problem
                    var jitter = new Random().Next(-500, 500);
                    await Task.Delay(delayMs + jitter);
                    delayMs *= 2; // Double the delay for next retry
                }
            }
        }
    }
}
