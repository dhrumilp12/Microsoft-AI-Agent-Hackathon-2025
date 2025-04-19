using System;

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
        /// <summary>
        /// System prompt that instructs the AI to act as a concept extraction specialist.
        /// Sets the AI's role and behavior context for transcript analysis.
        /// </summary>
        public const string ConceptExtractionSystemPrompt = 
            "You are an AI assistant that extracts key concepts and their relationships from lecture transcripts for diagram generation.";
        
        /// <summary>
        /// System prompt that instructs the AI to act as a diagram generation specialist.
        /// Sets the AI's role and behavior context for creating visual representations.
        /// </summary>
        public const string DiagramGenerationSystemPrompt = 
            "You are an AI assistant that generates Mermaid diagrams from educational concepts.";
        
        /// <summary>
        /// System prompt that instructs the AI to act as a concept expansion specialist.
        /// Sets the AI's role and behavior context for detailed concept breakdown.
        /// </summary>    
        public const string ConceptExpansionSystemPrompt = 
            "You are an AI assistant that generates detailed Mermaid diagrams for educational concepts.";
            
        /// <summary>
        /// Creates a prompt that instructs the AI to extract concepts from a transcript.
        /// </summary>
        /// <param name="transcript">The lecture transcript to analyze</param>
        /// <returns>A formatted prompt that directs the AI to extract concepts in JSON format</returns>
        public static string GetConceptExtractionPrompt(string transcript) => $@"
Extract the main concepts, their relationships, and hierarchy from the following lecture transcript.
Format your response as a JSON array of concept nodes with the following structure:
[
  {{
    ""id"": ""unique_id"",
    ""name"": ""concept_name"",
    ""description"": ""brief_description"",
    ""relationships"": [
      {{ ""type"": ""relationship_type"", ""target"": ""target_concept_id"" }}
    ],
    ""importance"": 1-5
  }}
]
Ensure the concepts form a coherent structure that could be visualized as a diagram.
Make sure the JSON is valid, complete, and properly closed.
Limit your response to important concepts only, maximum 20 concepts.

Transcript:
{transcript}
";
        
        /// <summary>
        /// Creates a prompt that instructs the AI to generate a Mermaid diagram from concept data.
        /// </summary>
        /// <param name="conceptsJson">JSON representation of concept nodes</param>
        /// <param name="diagramType">The type of diagram to generate (mindmap, flowchart, sequence, etc.)</param>
        /// <returns>A formatted prompt that directs the AI to create a structured diagram</returns>
        public static string GetDiagramGenerationPrompt(string conceptsJson, string diagramType) => $@"
Create a Mermaid {diagramType} diagram based on the following concepts and their relationships:
{conceptsJson}

Rules:
1. Use proper Mermaid syntax for {diagramType}
2. Organize concepts hierarchically based on their relationships
3. Include all important concepts (importance >= 3)
4. Make the diagram clear and readable
5. Use style attributes to highlight important nodes
6. Return ONLY the Mermaid diagram syntax without any explanations

The output should start with ```mermaid and end with ```
";
        
        /// <summary>
        /// Creates a prompt that instructs the AI to expand a specific concept into a more detailed diagram.
        /// </summary>
        /// <param name="conceptName">The name of the concept to expand</param>
        /// <param name="conceptJson">JSON representation of the concept node</param>
        /// <param name="diagramType">The type of diagram to generate (mindmap, flowchart, sequence, etc.)</param>
        /// <returns>A formatted prompt that directs the AI to create a detailed diagram focused on a single concept</returns>
        public static string GetConceptExpansionPrompt(string conceptName, string conceptJson, string diagramType) => $@"
Expand the concept ""{conceptName}"" in more detail. Based on what we know about it:
{conceptJson}

Create a detailed Mermaid {diagramType} diagram that breaks down this concept into sub-concepts, examples, applications, etc.
Use styling to highlight important elements.
Return ONLY the Mermaid diagram syntax without any explanations.

The output should start with ```mermaid and end with ```
";
    }
}
