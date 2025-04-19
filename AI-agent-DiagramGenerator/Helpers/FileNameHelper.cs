using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DiagramGenerator.Helpers
{
    public static class FileNameHelper
    {
        /// <summary>
        /// Sanitizes a string to make it safe for use as a file name
        /// </summary>
        /// <param name="fileName">The string to sanitize</param>
        /// <returns>A sanitized string safe for use as a file name</returns>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "unnamed";
                
            // Remove invalid characters
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            
            string sanitized = Regex.Replace(fileName, invalidRegStr, "_");
            
            // Truncate if too long
            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 47) + "...";
            }
            
            return sanitized;
        }
    }
}
