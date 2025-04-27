using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DiagramGenerator.Helpers
{
    /// <summary>
    /// Utility class that provides helper methods for working with file names.
    /// Ensures file names are valid for the operating system and don't contain illegal characters.
    /// </summary>
    public static class FileNameHelper
    {
        /// <summary>
        /// Sanitizes a string to make it safe for use as a file name.
        /// Removes invalid characters, replaces them with underscores, and truncates if too long.
        /// </summary>
        /// <param name="fileName">The string to sanitize into a valid file name</param>
        /// <returns>A sanitized string safe for use as a file name on the current operating system</returns>
        /// <remarks>
        /// The method handles these cases:
        /// - Removes all invalid characters as defined by Path.GetInvalidFileNameChars()
        /// - Replaces invalid characters with underscores
        /// - Truncates filenames longer than 50 characters to prevent path length issues
        /// - Returns "unnamed" if input is null or empty
        /// </remarks>
        public static string SanitizeFileName(string fileName)
        {
            // Handle null or empty input
            if (string.IsNullOrEmpty(fileName))
                return "unnamed";
                
            // Remove invalid characters by getting all the chars that are invalid for the current OS
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            
            // Replace invalid chars with underscores
            string sanitized = Regex.Replace(fileName, invalidRegStr, "_");
            
            // Truncate if too long (prevents path too long exceptions)
            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 47) + "...";
            }
            
            return sanitized;
        }
    }
}
