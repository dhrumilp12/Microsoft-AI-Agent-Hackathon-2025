using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VocabularyBank.Helpers
{
    /// <summary>
    /// Helper class for implementing retry logic with exponential backoff
    /// </summary>
    public static class RetryHelper
    {
        private static readonly Random _jitterer = new Random();

        /// <summary>
        /// Executes an async function with retry logic using exponential backoff
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="operation">The async operation to execute</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <param name="initialDelay">Initial delay in milliseconds</param>
        /// <param name="shouldRetryPredicate">Optional predicate to determine if retry should occur based on the exception</param>
        /// <returns>The result of the operation</returns>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation, 
            int maxRetries = 3, 
            int initialDelay = 1000,
            Func<Exception, bool> shouldRetryPredicate = null)
        {
            int retryCount = 0;
            int delay = initialDelay;

            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (HttpRequestException ex) when (
                    (ex.Message.Contains("429") || IsRetryableError(ex)) &&
                    (shouldRetryPredicate == null || shouldRetryPredicate(ex)))
                {
                    // Check if we've exceeded max retries
                    if (retryCount >= maxRetries)
                    {
                        Console.WriteLine($"Maximum retries ({maxRetries}) exceeded for rate limited operation.");
                        throw;
                    }

                    retryCount++;
                    
                    // Parse retry-after header if available, otherwise use exponential backoff
                    int retryDelay = GetRetryDelay(ex, delay);
                    
                    Console.WriteLine($"API limit encountered. Retrying in {retryDelay/1000.0:F1} seconds... (Attempt {retryCount} of {maxRetries})");
                    await Task.Delay(retryDelay);
                    
                    // Exponential backoff: double delay each time plus some jitter
                    delay = delay * 2 + _jitterer.Next(100, 500);
                }
                catch (Exception ex)
                {
                    // For other exceptions that aren't rate limits
                    if (shouldRetryPredicate != null && shouldRetryPredicate(ex))
                    {
                        if (retryCount >= maxRetries)
                        {
                            Console.WriteLine($"Maximum retries ({maxRetries}) exceeded for operation.");
                            throw;
                        }
                        
                        retryCount++;
                        Console.WriteLine($"Retrying operation after error: {ex.Message} (Attempt {retryCount} of {maxRetries})");
                        await Task.Delay(delay);
                        
                        // Exponential backoff: double delay each time plus some jitter
                        delay = delay * 2 + _jitterer.Next(100, 500);
                    }
                    else
                    {
                        // Non-retryable exception
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the appropriate retry delay from exception details or default value
        /// </summary>
        private static int GetRetryDelay(HttpRequestException ex, int defaultDelay)
        {
            // Try to parse retry-after from error message
            string errorMessage = ex.Message;
            if (errorMessage.Contains("retry after"))
            {
                // Extract seconds value from "retry after X seconds"
                int startIndex = errorMessage.IndexOf("retry after ") + 12;
                int endIndex = errorMessage.IndexOf(" second", startIndex);
                
                if (endIndex > startIndex && int.TryParse(errorMessage.Substring(startIndex, endIndex - startIndex), out int seconds))
                {
                    return seconds * 1000; // Convert seconds to milliseconds
                }
            }
            
            // Default exponential backoff
            return defaultDelay;
        }
        
        /// <summary>
        /// Determines if an error is retryable based on the error code or message
        /// </summary>
        private static bool IsRetryableError(HttpRequestException ex)
        {
            string message = ex.Message.ToLowerInvariant();
            
            // Don't retry invalid_prompt errors - these won't succeed with retries
            if (message.Contains("invalid_prompt") || message.Contains("violating")) 
                return false;
                
            // Retry on these specific error types
            return message.Contains("429") ||   // Rate limit
                   message.Contains("500") ||   // Internal server error
                   message.Contains("502") ||   // Bad gateway
                   message.Contains("503") ||   // Service unavailable
                   message.Contains("504") ||   // Gateway timeout
                   message.Contains("timeout");
        }
    }
}
