using System;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace DiagramGenerator.Constants
{
    /// <summary>
    /// Contains all prompt templates used in the application for AI interactions.
    /// This centralized approach makes it easier to:
    /// 1. Maintain consistent prompting strategies
    /// 2. Update prompts without changing service code
    /// 3. Version control prompt changes separately from logic
    /// </summary>
    public static class PromptConstants
    {
        private static readonly string[] AllowedDiagramTypes = { "mindmap", "flowchart", "sequence" };

        /// <summary>
        /// System prompt that instructs the AI to act as an educational concept extraction specialist.
        /// Sets the AI's role to assist students in understanding lecture content.
        /// </summary>
        public const string ConceptExtractionSystemPrompt = 
            "You are an AI assistant designed to help students learn by extracting key concepts and their relationships from lecture transcripts. Your goal is to simplify complex ideas into clear, concise, and student-friendly explanations suitable for classroom use. Do not process or include any personal or sensitive information in your outputs.";
        
        /// <summary>
        /// System prompt that instructs the AI to act as an educational diagram generation specialist.
        /// Sets the AI's role to create visual aids for student learning.
        /// </summary>
        public const string DiagramGenerationSystemPrompt = 
            "You are an AI assistant that creates clear and engaging Mermaid diagrams to help students visualize educational concepts in an interactive and easy-to-understand way. Do not include any personal or sensitive information in the diagrams.";
        
        /// <summary>
        /// System prompt that instructs the AI to act as an educational concept expansion specialist.
        /// Sets the AI's role to provide detailed breakdowns for deeper student understanding.
        /// </summary>    
        public const string ConceptExpansionSystemPrompt = 
            "You are an AI assistant that expands educational concepts into detailed, student-friendly Mermaid diagrams, breaking down complex ideas into sub-concepts, examples, and applications. Do not include any personal or sensitive information in the diagrams.";
            
        /// <summary>
        /// Sanitizes input to prevent prompt injection by removing or escaping dangerous characters.
        /// </summary>
        private static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Remove or escape quotes, backticks, newlines, and other dangerous characters
            return Regex.Replace(input, @"[`'""]", "").Replace("\n", " ").Replace("\r", " ").Trim();
        }

        /// <summary>
        /// Redacts potential PHI from the input text to comply with HIPAA.
        /// </summary>
        private static string RedactPHI(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Simple regex patterns for common PHI (extend as needed)
            var patterns = new[]
            {
                @"(\b\d{3}-\d{2}-\d{4}\b)", // SSN
                @"(\b[A-Za-z]+\s[A-Za-z]+\b)", // Full names (basic)
                @"(\b\d{4}-\d{2}-\d{2}\b)", // Dates (YYYY-MM-DD)
                @"(\b\d{10}\b)" // Phone numbers (basic)
            };
            var redacted = input;
            foreach (var pattern in patterns)
            {
                redacted = Regex.Replace(redacted, pattern, "[REDACTED]");
            }
            return redacted;
        }

        /// <summary>
        /// Validates JSON input to ensure it is well-formed.
        /// </summary>
        private static bool IsValidJson(string json)
        {
            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates diagram type against allowed values.
        /// </summary>
        private static bool IsValidDiagramType(string diagramType)
        {
            return AllowedDiagramTypes.Contains(diagramType, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a prompt that instructs the AI to extract concepts from a transcript for student use.
        /// </summary>
        /// <param name="transcript">The lecture transcript to analyze</param>
        /// <returns>A formatted prompt that directs the AI to extract concepts in JSON format</returns>
        public static string GetConceptExtractionPrompt(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
                throw new ArgumentException("Transcript cannot be empty.");

            // Sanitize and redact PHI
            var sanitizedTranscript = SanitizeInput(RedactPHI(transcript));

            return @$"
You are helping students understand a lecture by extracting the most important concepts, their relationships, and their hierarchy from the following transcript. Focus on clarity and relevance for learners. Do not include any personal or sensitive information in your output.

Format your response as a JSON array of concept nodes with the following structure:
[
  {{
    ""id"": ""unique_id"",
    ""name"": ""concept_name"",
    ""description"": ""short, student-friendly explanation (1-2 sentences)"",
    ""relationships"": [
      {{ ""type"": ""relationship_type (e.g., 'depends_on', 'part_of')"", ""target"": ""target_concept_id"" }}
    ],
    ""importance"": 1-5 (5 being most critical for understanding)
  }}
]

Rules:
1. Limit to 15 key concepts to avoid overwhelming students.
2. Use simple, clear language in descriptions (appropriate for high school or early college level).
3. Ensure relationships are logical and support visual learning.
4. Validate JSON syntax and ensure it is complete.
5. Prioritize concepts that aid comprehension of the lecture's core ideas.
6. Do not include any personal or sensitive information, such as names, dates, or identifiers.

Transcript:
{sanitizedTranscript}
";
        }
        
        /// <summary>
        /// Creates a prompt that instructs the AI to generate a Mermaid diagram for student visualization.
        /// </summary>
        /// <param name="conceptsJson">JSON representation of concept nodes</param>
        /// <param name="diagramType">The type of diagram to generate (mindmap, flowchart, sequence, etc.)</param>
        /// <returns>A formatted prompt that directs the AI to create a structured diagram</returns>
        public static string GetDiagramGenerationPrompt(string conceptsJson, string diagramType)
        {
            if (string.IsNullOrWhiteSpace(conceptsJson))
                throw new ArgumentException("Concepts JSON cannot be empty.");
            if (!IsValidJson(conceptsJson))
                throw new ArgumentException("Invalid JSON format for concepts.");
            if (string.IsNullOrWhiteSpace(diagramType) || !IsValidDiagramType(diagramType))
                throw new ArgumentException($"Invalid diagram type. Allowed types: {string.Join(", ", AllowedDiagramTypes)}");

            // Sanitize inputs
            var sanitizedConceptsJson = SanitizeInput(conceptsJson);
            var sanitizedDiagramType = SanitizeInput(diagramType);
												   
									  
													
																  

            return @$"
Create a Mermaid {sanitizedDiagramType} diagram to help students visualize the following concepts and their relationships:
{sanitizedConceptsJson}

Rules:
1. Use proper Mermaid syntax for {sanitizedDiagramType}.
2. Organize concepts in a clear, hierarchical structure that supports student learning.
3. Include only concepts with importance >= 3 to keep the diagram focused.
4. Use simple labels and avoid jargon unless necessary.
5. Apply Mermaid style attributes (e.g., colors, bolding) to highlight key concepts for visual clarity.
6. Ensure the diagram is uncluttered and easy to read on a classroom screen or student device.
7. Do not include any personal or sensitive information in the diagram.
8. Return ONLY the Mermaid diagram syntax, starting with ```mermaid and ending with ```.
";
        }
        
        /// <summary>
        /// Creates a prompt that instructs the AI to expand a concept into a detailed diagram for students.
        /// </summary>
        /// <param name="conceptName">The name of the concept to expand</param>
        /// <param name="conceptJson">JSON representation of the concept node</param>
        /// <param name="diagramType">The type of diagram to generate (mindmap, flowchart, sequence, etc.)</param>
        /// <returns>A formatted prompt that directs the AI to create a detailed diagram</returns>
        public static string GetConceptExpansionPrompt(string conceptName, string conceptJson, string diagramType)
        {
            if (string.IsNullOrWhiteSpace(conceptName))
                throw new ArgumentException("Concept name cannot be empty.");
            if (string.IsNullOrWhiteSpace(conceptJson))
                throw new ArgumentException("Concept JSON cannot be empty.");
            if (!IsValidJson(conceptJson))
                throw new ArgumentException("Invalid JSON format for concept.");
            if (string.IsNullOrWhiteSpace(diagramType) || !IsValidDiagramType(diagramType))
                throw new ArgumentException($"Invalid diagram type. Allowed types: {string.Join(", ", AllowedDiagramTypes)}");

            // Sanitize inputs
            var sanitizedConceptName = SanitizeInput(conceptName);
            var sanitizedConceptJson = SanitizeInput(conceptJson);
            var sanitizedDiagramType = SanitizeInput(diagramType);

            return @$"
Expand the concept ""{sanitizedConceptName}"" into a detailed Mermaid {sanitizedDiagramType} diagram to help students deepen their understanding. Use the following concept information:
{sanitizedConceptJson}

Rules:
1. Break down the concept into 5-10 sub-concepts, examples, or applications relevant to classroom learning.
2. Use clear, student-friendly labels and explanations.
3. Apply Mermaid styling (e.g., colors, shapes) to differentiate key elements and enhance engagement.
4. Ensure the diagram is intuitive and supports visual learning for high school or early college students.
5. Do not include any personal or sensitive information in the diagram.
6. Return ONLY the Mermaid diagram syntax, starting with ```mermaid and ending with ```.
";
        }
    }
}
