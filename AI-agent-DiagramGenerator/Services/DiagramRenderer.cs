using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DiagramGenerator.Services
{
    /// <summary>
    /// Service responsible for rendering diagrams and validating Mermaid syntax
    /// </summary>
    public class DiagramRenderer
    {
        private readonly ILogger<DiagramRenderer> _logger;
        
        public DiagramRenderer(ILogger<DiagramRenderer> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Generates a simple HTML file with Mermaid diagram that works reliably
        /// </summary>
        public async Task<string> RenderDiagramAsHtml(string mermaidSyntax, string title = "Generated Diagram")
        {
            // Extract just the mermaid syntax without markdown code block markers
            string cleanSyntax = ExtractAndCleanMermaidSyntax(mermaidSyntax);
            bool isSequence = cleanSyntax.TrimStart().StartsWith("sequenceDiagram");
            
            // Create a simple, reliable HTML template without complex JS
            StringBuilder html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>{title}</title>");
            html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/mermaid@8.13.0/dist/mermaid.min.js\"></script>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f0f0f0; }");
            html.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background-color: #fff; padding: 20px; border-radius: 5px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }");
            html.AppendLine("        h1 { text-align: center; color: #333; }");
            html.AppendLine("        .diagram-container { overflow: auto; margin: 20px 0; border: 1px solid #ddd; border-radius: 5px; padding: 10px; min-height: 500px; position: relative; }");
            html.AppendLine("        .mermaid { min-width: 800px; font-size: 16px !important; }"); 
            html.AppendLine("        .mermaid .label { font-size: 16px !important; font-family: 'Trebuchet MS', Arial, sans-serif !important; }");
            html.AppendLine("        .mermaid .node rect, .mermaid .node circle, .mermaid .node ellipse, .mermaid .node polygon, .mermaid .node path { fill-opacity: 0.9 !important; }");
            html.AppendLine("        .mermaid .edgeLabel { font-size: 14px !important; background-color: white !important; padding: 2px !important; }");
            html.AppendLine("        pre { background-color: #f5f5f5; padding: 10px; border-radius: 5px; overflow-x: auto; }");
            html.AppendLine("        .error-message { color: #d9534f; padding: 10px; background-color: #f9f2f2; border-radius: 5px; margin-top: 20px; display: none; }");
            html.AppendLine("        .controls { position: absolute; top: 10px; right: 10px; z-index: 100; background-color: rgba(255,255,255,0.9); padding: 8px; border-radius: 5px; display: flex; flex-wrap: wrap; box-shadow: 0 2px 4px rgba(0,0,0,0.2); }");
            html.AppendLine("        .controls button { margin: 3px 5px; padding: 5px 10px; cursor: pointer; background: #f0f0f0; border: 1px solid #ccc; border-radius: 3px; font-size: 14px; }");
            html.AppendLine("        .controls button:hover { background: #e0e0e0; }");
            html.AppendLine("        .controls button.download { background-color: #4CAF50; color: white; border: none; font-weight: bold; padding: 6px 12px; margin-left: 15px; }");
            html.AppendLine("        .controls button.download:hover { background-color: #45a049; box-shadow: 0 1px 3px rgba(0,0,0,0.2); }");
            html.AppendLine("        .zoom-level { font-size: 12px; margin: 0 10px; align-self: center; }");
            
            // Add special styling for sequence diagrams
            if (isSequence)
            {
                html.AppendLine("        /* Custom styles for sequence diagram actors */");
                html.AppendLine("        .actor { fill: #9cf !important; stroke: #333 !important; }");
                html.AppendLine("        .actor-line { stroke: gray !important; }");
                html.AppendLine("        .messageLine0 { stroke: #333 !important; }");
                html.AppendLine("        .messageLine1 { stroke: #333 !important; }");
                html.AppendLine("        .messageText { fill: #333 !important; font-size: 14px !important; }");
                html.AppendLine("        .labelBox { fill: #f96 !important; stroke: #333 !important; }");
                html.AppendLine("        .labelText { fill: white !important; font-size: 14px !important; }");
                html.AppendLine("        .noteText { fill: #333 !important; font-size: 14px !important; }");
                html.AppendLine("        .loopText { fill: #333 !important; font-size: 14px !important; }");
            }
            
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div class=\"container\">");
            html.AppendLine($"        <h1>{title}</h1>");
            html.AppendLine("        <div class=\"diagram-container\" id=\"diagram-container\">");
            html.AppendLine("            <div class=\"controls\">");
            html.AppendLine("                <button id=\"zoom-in\" title=\"Zoom In\">Zoom In</button>");
            html.AppendLine("                <button id=\"zoom-out\" title=\"Zoom Out\">Zoom Out</button>");
            html.AppendLine("                <button id=\"zoom-reset\" title=\"Reset Zoom\">Reset</button>");
            html.AppendLine("                <span class=\"zoom-level\" id=\"zoom-level\">100%</span>");
            html.AppendLine("                <button id=\"download-png\" class=\"download\" title=\"Save diagram as PNG image\">⬇️ Download PNG</button>");
            html.AppendLine("            </div>");
            html.AppendLine("            <pre class=\"mermaid\">");
            html.AppendLine(cleanSyntax);
            html.AppendLine("            </pre>");
            html.AppendLine("        </div>");
            html.AppendLine("        <div id=\"error-display\" class=\"error-message\"></div>");
            html.AppendLine("        <hr>");
            html.AppendLine("        <details>");
            html.AppendLine("            <summary>View Mermaid Source</summary>");
            html.AppendLine($"            <pre id=\"source-display\">{cleanSyntax}</pre>");
            html.AppendLine("        </details>");
            html.AppendLine("        <div class=\"version-info\" style=\"text-align: center; color: #777; margin-top: 20px;\">");
            html.AppendLine("            Using Mermaid v8.13.0 for better compatibility");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            html.AppendLine("");
            html.AppendLine("    <script>");
            html.AppendLine("        document.addEventListener('DOMContentLoaded', function() {");
            html.AppendLine("            try {");
            html.AppendLine("                // Configure Mermaid with appropriate settings");
            html.AppendLine("                mermaid.initialize({");
            html.AppendLine("                    startOnLoad: true,");
            html.AppendLine("                    theme: 'default',");
            html.AppendLine("                    securityLevel: 'loose',");
            html.AppendLine("                    logLevel: 1,");

            // Add specialized configuration for sequence diagrams
            if (isSequence)
            {
                html.AppendLine("                    sequence: {");
                html.AppendLine("                        diagramMarginX: 50,");
                html.AppendLine("                        diagramMarginY: 10,");
                html.AppendLine("                        actorMargin: 80,"); // Increased actor spacing
                html.AppendLine("                        width: 150,");
                html.AppendLine("                        height: 65,");
                html.AppendLine("                        boxMargin: 10,");
                html.AppendLine("                        boxTextMargin: 5,");
                html.AppendLine("                        noteMargin: 10,");
                html.AppendLine("                        messageMargin: 35,");
                html.AppendLine("                        mirrorActors: false,"); // Don't duplicate actors at bottom
                html.AppendLine("                        bottomMarginAdj: 10,");
                html.AppendLine("                        useMaxWidth: false"); // Prevent auto-scaling
                html.AppendLine("                    },");
            }

            html.AppendLine("                    flowchart: {");
            html.AppendLine("                        useMaxWidth: false,"); // Prevent auto-scaling
            html.AppendLine("                        htmlLabels: true,");
            html.AppendLine("                        curve: 'basis'");
            html.AppendLine("                    }");
            html.AppendLine("                });");
            html.AppendLine("");
            html.AppendLine("                // Handle errors");
            html.AppendLine("                mermaid.parseError = function(err, hash) {");
            html.AppendLine("                    document.getElementById('error-display').style.display = 'block';");
            html.AppendLine("                    document.getElementById('error-display').textContent = 'Diagram syntax error: ' + err;");
            html.AppendLine("                    console.error('Mermaid error:', err);");
            html.AppendLine("                };");
            html.AppendLine("");
            html.AppendLine("                // Add zoom functionality after rendering");
            html.AppendLine("                setTimeout(setupZoom, 1000);");
            html.AppendLine("");
            html.AppendLine("                function setupZoom() {");
            html.AppendLine("                    const container = document.getElementById('diagram-container');");
            html.AppendLine("                    const mermaidDiv = container.querySelector('.mermaid');");
            html.AppendLine("                    const svgElement = mermaidDiv.querySelector('svg');");
            html.AppendLine("                    const zoomInBtn = document.getElementById('zoom-in');");
            html.AppendLine("                    const zoomOutBtn = document.getElementById('zoom-out');");
            html.AppendLine("                    const zoomResetBtn = document.getElementById('zoom-reset');");
            html.AppendLine("                    const zoomLevelDisplay = document.getElementById('zoom-level');");
            html.AppendLine("                    const downloadPngBtn = document.getElementById('download-png');");
            html.AppendLine("");
            html.AppendLine("                    if (!svgElement) {");
            html.AppendLine("                        console.error('SVG element not found');");
            html.AppendLine("                        return;");
            html.AppendLine("                    }");
            html.AppendLine("");
            html.AppendLine("                    // Make sure the download button is visible");
            html.AppendLine("                    if (downloadPngBtn) {");
            html.AppendLine("                        downloadPngBtn.style.display = 'inline-block';");
            html.AppendLine("                    } else {");
            html.AppendLine("                        console.error('Download button not found');");
            html.AppendLine("                    }");
            html.AppendLine("");
            html.AppendLine("                    let currentZoom = 1.0;");
            html.AppendLine("");
            html.AppendLine("                    function updateZoom(zoom) {");
            html.AppendLine("                        currentZoom = zoom;");
            html.AppendLine("                        const percentage = Math.round(zoom * 100);");
            html.AppendLine("                        svgElement.style.transform = `scale(${zoom})`;");
            html.AppendLine("                        svgElement.style.transformOrigin = 'top left';");
            html.AppendLine("                        zoomLevelDisplay.textContent = `${percentage}%`;");
            html.AppendLine("                    }");
            html.AppendLine("");
            html.AppendLine("                    zoomInBtn.addEventListener('click', function() {");
            html.AppendLine("                        updateZoom(currentZoom + 0.1);");
            html.AppendLine("                    });");
            html.AppendLine("");
            html.AppendLine("                    zoomOutBtn.addEventListener('click', function() {");
            html.AppendLine("                        if (currentZoom > 0.2) updateZoom(currentZoom - 0.1);");
            html.AppendLine("                    });");
            html.AppendLine("");
            html.AppendLine("                    zoomResetBtn.addEventListener('click', function() {");
            html.AppendLine("                        updateZoom(1.0);");
            html.AppendLine("                    });");
            html.AppendLine("");
            html.AppendLine("                    // Function to download diagram as PNG image");
            html.AppendLine("                    downloadPngBtn.addEventListener('click', function() {");
            html.AppendLine("                        // Show feedback to user");
            html.AppendLine("                        downloadPngBtn.textContent = 'Preparing...';");
            html.AppendLine("                        downloadPngBtn.disabled = true;");
            html.AppendLine("");
            html.AppendLine("                        // Reset zoom for accurate image");
            html.AppendLine("                        const originalZoom = currentZoom;");
            html.AppendLine("                        updateZoom(1.0);");
            html.AppendLine("");
            html.AppendLine("                        // Use setTimeout to allow the browser to update the display");
            html.AppendLine("                        setTimeout(function() {");
            html.AppendLine("                            try {");
            html.AppendLine("                                // Create a canvas element to convert SVG to image");
            html.AppendLine("                                const canvas = document.createElement('canvas');");
            html.AppendLine("                                const ctx = canvas.getContext('2d');");
            html.AppendLine("                                const svgData = new XMLSerializer().serializeToString(svgElement);");
            html.AppendLine("");
            html.AppendLine("                                // Create an image to draw the SVG to the canvas");
            html.AppendLine("                                const img = new Image();");
            html.AppendLine("                                const svgBlob = new Blob([svgData], {type: 'image/svg+xml;charset=utf-8'});");
            html.AppendLine("                                const url = URL.createObjectURL(svgBlob);");
            html.AppendLine("");
            html.AppendLine("                                img.onload = function() {");
            html.AppendLine("                                    try {");
            html.AppendLine("                                        // Set canvas dimensions to match SVG with padding");
            html.AppendLine("                                        const padding = 20;");
            html.AppendLine("                                        canvas.width = Math.max(600, (svgElement.viewBox.baseVal.width || img.width) + padding * 2);");
            html.AppendLine("                                        canvas.height = Math.max(400, (svgElement.viewBox.baseVal.height || img.height) + padding * 2);");
            html.AppendLine("");
            html.AppendLine("                                        // Add padding for better visualization");
            html.AppendLine("                                        ctx.fillStyle = '#ffffff';");
            html.AppendLine("                                        ctx.fillRect(0, 0, canvas.width, canvas.height);");
            html.AppendLine("                                        ctx.drawImage(img, padding, padding);");
            html.AppendLine("                                        ");
            html.AppendLine("                                        // Create download link for the image");
            html.AppendLine("                                        URL.revokeObjectURL(url);");
            html.AppendLine("                                        const imgURI = canvas.toDataURL('image/png');");
            html.AppendLine("                                        const fileName = '" + title.Replace(" ", "_") + ".png';");
            html.AppendLine("                                        ");
            html.AppendLine("                                        // Create download link and trigger it");
            html.AppendLine("                                        const downloadLink = document.createElement('a');");
            html.AppendLine("                                        downloadLink.download = fileName;");
            html.AppendLine("                                        downloadLink.href = imgURI;");
            html.AppendLine("                                        document.body.appendChild(downloadLink); // Needed for Firefox");
            html.AppendLine("                                        downloadLink.click();");
            html.AppendLine("                                        document.body.removeChild(downloadLink);");
            html.AppendLine("                                        ");
            html.AppendLine("                                        // Restore original zoom");
            html.AppendLine("                                        updateZoom(originalZoom);");
            html.AppendLine("                                        ");
            html.AppendLine("                                        // Restore button state");
            html.AppendLine("                                        downloadPngBtn.textContent = '⬇️ Download PNG';");
            html.AppendLine("                                        downloadPngBtn.disabled = false;");
            html.AppendLine("                                    } catch (err) {");
            html.AppendLine("                                        console.error('Error creating PNG:', err);");
            html.AppendLine("                                        alert('Could not create image: ' + err.message);");
            html.AppendLine("                                        downloadPngBtn.textContent = '⬇️ Download PNG';");
            html.AppendLine("                                        downloadPngBtn.disabled = false;");
            html.AppendLine("                                    }");
            html.AppendLine("                                };");
            html.AppendLine("");
            html.AppendLine("                                img.onerror = function(e) {");
            html.AppendLine("                                    console.error('Image loading error:', e);");
            html.AppendLine("                                    alert('Error loading SVG for export');");
            html.AppendLine("                                    downloadPngBtn.textContent = '⬇️ Download PNG';");
            html.AppendLine("                                    downloadPngBtn.disabled = false;");
            html.AppendLine("                                }");
            html.AppendLine("");
            html.AppendLine("                                img.src = url;");
            html.AppendLine("                            } catch (e) {");
            html.AppendLine("                                console.error('Error in PNG export:', e);");
            html.AppendLine("                                alert('Error exporting diagram: ' + e.message);");
            html.AppendLine("                                downloadPngBtn.textContent = '⬇️ Download PNG';");
            html.AppendLine("                                downloadPngBtn.disabled = false;");
            html.AppendLine("                            }");
            html.AppendLine("                        }, 100);");
            html.AppendLine("                    });");
            html.AppendLine("");
            html.AppendLine("                    // Make diagram initially more readable");
            html.AppendLine("                    // If the SVG is wider than container, apply initial zoom");
            html.AppendLine("                    if (svgElement.getBoundingClientRect().width > container.clientWidth) {");
            html.AppendLine("                        const initialZoom = Math.min(1.0, container.clientWidth / svgElement.getBoundingClientRect().width * 0.9);");
            html.AppendLine("                        updateZoom(initialZoom);");
            html.AppendLine("                    }");
            html.AppendLine("");
            html.AppendLine("                    // Add wheel zoom support");
            html.AppendLine("                    container.addEventListener('wheel', function(e) {");
            html.AppendLine("                        if (e.ctrlKey) {");
            html.AppendLine("                            e.preventDefault();");
            html.AppendLine("                            const delta = e.deltaY > 0 ? -0.1 : 0.1;");
            html.AppendLine("                            const newZoom = Math.max(0.2, Math.min(3, currentZoom + delta));");
            html.AppendLine("                            updateZoom(newZoom);");
            html.AppendLine("                        }");
            html.AppendLine("                    }, { passive: false });");
            html.AppendLine("                }");
            html.AppendLine("            } catch(e) {");
            html.AppendLine("                console.error('Error initializing Mermaid:', e);");
            html.AppendLine("                document.getElementById('error-display').style.display = 'block';");
            html.AppendLine("                document.getElementById('error-display').textContent = 'Error rendering diagram: ' + e.message;");
            html.AppendLine("            }");
            html.AppendLine("        });");
            html.AppendLine("    </script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            await Task.CompletedTask;
            return html.ToString();
        }
        
        /// <summary>
        /// Extracts and cleans Mermaid syntax from markdown or raw text
        /// </summary>
        private string ExtractAndCleanMermaidSyntax(string input)
        {
            // Extract the Mermaid syntax from markdown code blocks if present
            string syntax = input;
            
            if (input.Contains("```mermaid"))
            {
                int start = input.IndexOf("```mermaid") + 10;
                int end = input.IndexOf("```", start);
                if (end > start)
                {
                    syntax = input.Substring(start, end - start).Trim();
                }
            }
            
            // Determine diagram type and apply appropriate cleaning
            bool isMindmap = syntax.TrimStart().StartsWith("mindmap");
            bool isSequence = syntax.TrimStart().StartsWith("sequenceDiagram");
            bool isFlowchart = syntax.TrimStart().Contains("flowchart") || syntax.TrimStart().StartsWith("graph");
            
            if (isMindmap)
            {
                return ConvertToGraph(syntax);
            }
            else if (isSequence)
            {
                return CleanSequenceDiagram(syntax);
            }
            else if (isFlowchart)
            {
                return CleanFlowchart(syntax);
            }
            
            // Return other diagram types with minimal changes
            return syntax;
        }
        
        /// <summary>
        /// Cleans sequence diagram syntax for compatibility with Mermaid 8.14.0
        /// </summary>
        private string CleanSequenceDiagram(string syntax)
        {
            // Basic cleaning for sequence diagrams
            StringBuilder cleanedDiagram = new StringBuilder();
            
            // Split into lines
            string[] lines = syntax.Split('\n');
            bool inClassDef = false;
            HashSet<string> entityIds = new HashSet<string>();
            
            // Start with the sequence diagram declaration
            cleanedDiagram.AppendLine("sequenceDiagram");
            
            // Add autonumber option if not already present
            if (!syntax.Contains("autonumber"))
            {
                cleanedDiagram.AppendLine("    autonumber");
            }
            
            // First pass: collect all entity IDs from participant declarations
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("participant "))
                {
                    // Extract the entity ID before "as" if present
                    var match = Regex.Match(trimmedLine, @"participant\s+(\w+)(?:\s+as\s+|$)");
                    if (match.Success)
                    {
                        entityIds.Add(match.Groups[1].Value);
                    }
                }
            }
            
            // Second pass: process and fix each line
            foreach (var line in lines.Skip(1)) // Skip the sequenceDiagram declaration line
            {
                string trimmedLine = line.Trim();
                string processedLine = line;
                
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;
                    
                // Skip class assignments and style lines which cause issues in sequence diagrams
                if (trimmedLine.StartsWith("class ") || 
                    trimmedLine.StartsWith("style ") ||
                    trimmedLine.StartsWith("classDef"))
                    continue;
                
                // Skip comment lines about styling
                if (trimmedLine.StartsWith("%%") && 
                   (trimmedLine.Contains("style") || trimmedLine.Contains("Styling")))
                    continue;
                
                // Fix sequence diagram arrow statements
                if (trimmedLine.Contains("->>") || trimmedLine.Contains("-->>"))
                {
                    // Matches patterns like "A->>B: message" or "A-->>B: message"
                    var match = Regex.Match(trimmedLine, @"(\w+)(-+>>)(\w+):\s*(.*)");
                    
                    if (match.Success)
                    {
                        string source = match.Groups[1].Value;
                        string arrow = match.Groups[2].Value;
                        string target = match.Groups[3].Value;
                        string message = match.Groups[4].Value.Trim();
                        
                        // Check if message accidentally contains entity IDs (common error)
                        foreach (var entityId in entityIds)
                        {
                            // If message starts with an entity ID, remove it
                            if (message.StartsWith(entityId, StringComparison.OrdinalIgnoreCase))
                            {
                                message = message.Substring(entityId.Length).Trim();
                                // If message starts with a separating character, remove it too
                                message = Regex.Replace(message, @"^[\s:;,-]+", "");
                                _logger.LogWarning($"Removed duplicate entity reference from message: '{entityId}'");
                            }
                        }
                        
                        // Reconstruct the line with fixed message
                        int indent = line.Length - line.TrimStart().Length;
                        string fixedLine = new string(' ', indent) + $"{source}{arrow}{target}: {message}";
                        processedLine = fixedLine;
                    }
                }
                    
                // Include all other lines (participants, messages, notes, etc.)
                cleanedDiagram.AppendLine(processedLine);
            }
            
            // Only add a note at the bottom if we don't have a note already
            if (!syntax.Contains("Note over"))
            {
                cleanedDiagram.AppendLine("    Note over DL: Styling simplified for compatibility with Mermaid 8.13.0");
            }
            
            return cleanedDiagram.ToString().TrimEnd();
        }
        
        /// <summary>
        /// Cleans flowchart syntax for compatibility with Mermaid 8.13.0
        /// </summary>
        private string CleanFlowchart(string syntax)
        {
            StringBuilder cleanedFlowchart = new StringBuilder();
            
            // Split into lines
            string[] lines = syntax.Split('\n');
            bool foundGraphDeclaration = false;
            
            // Process each line
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();
                string processedLine = lines[i];
                
                // Check for duplicate graph/flowchart declarations
                if ((trimmedLine.StartsWith("graph ") || trimmedLine.StartsWith("flowchart ")))
                {
                    // If we've already found a graph declaration, skip this line
                    if (foundGraphDeclaration)
                    {
                        _logger.LogWarning("Removing duplicate graph declaration: {line}", trimmedLine);
                        continue;
                    }
                    
                    // Mark that we've found the first declaration
                    foundGraphDeclaration = true;
                }
                
                // Fix edge syntax with labels: convert `--| |` to `-->| |` format
                if (trimmedLine.Contains("--|") && trimmedLine.Contains("|"))
                {
                    processedLine = Regex.Replace(processedLine, @"--\|([^|]*)\|", "-->|$1|");
                }
                
                // Fix class assignments with multiple nodes
                if (trimmedLine.StartsWith("class ") && trimmedLine.Contains(","))
                {
                    // Extract the class name (style name)
                    int classNameIndex = trimmedLine.LastIndexOf(' ');
                    if (classNameIndex > 6) // "class " is 6 characters
                    {
                        string className = trimmedLine.Substring(classNameIndex + 1).TrimEnd(';');
                        
                        // Extract all node IDs
                        string nodesPart = trimmedLine.Substring(6, classNameIndex - 6);
                        string[] nodeIds = nodesPart.Split(',').Select(id => id.Trim()).ToArray();
                        
                        // Create individual class assignments for each node
                        foreach (string nodeId in nodeIds)
                        {
                            if (!string.IsNullOrWhiteSpace(nodeId))
                            {
                                cleanedFlowchart.AppendLine($"class {nodeId} {className};");
                            }
                        }
                        
                        continue; // Skip adding the original line
                    }
                }
                
                // Add the line with any necessary fixes applied
                cleanedFlowchart.AppendLine(processedLine);
            }
            
            return cleanedFlowchart.ToString();
        }
        
        /// <summary>
        /// Converts a mindmap to a graph/flowchart for better compatibility
        /// </summary>
        private string ConvertToGraph(string mindmapSyntax)
        {
            StringBuilder graph = new StringBuilder("graph TD\n");
            
            // Split into lines
            string[] lines = mindmapSyntax.Split('\n');
            var nodes = new Dictionary<string, string>();
            var indentationMap = new Dictionary<int, string>(); // track parent nodes by indentation
            var relationships = new List<string>();
            
            // Skip the mindmap declaration
            int startIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "mindmap")
                {
                    startIndex = i + 1;
                    break;
                }
            }
            
            // Process nodes and build hierarchy
            for (int i = startIndex; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmedLine = line.Trim();
                
                // Skip class definitions and comments
                if (trimmedLine.StartsWith("class") || trimmedLine.StartsWith("classDef") || 
                    trimmedLine.StartsWith("%%") || string.IsNullOrWhiteSpace(trimmedLine))
                    continue;
                
                // Calculate indentation level
                int indent = line.Length - line.TrimStart().Length;
                
                // Extract node ID and text
                string nodeId = "";
                string nodeText = "";
                
                // Parse root nodes (special case)
                if (trimmedLine.StartsWith("root"))
                {
                    Match rootMatch = Regex.Match(trimmedLine, @"root\s*(\(\(.*?\)\)|\[.*?\])");
                    if (rootMatch.Success)
                    {
                        nodeId = "root";
                        nodeText = ExtractNodeText(rootMatch.Groups[1].Value);
                    }
                    else
                    {
                        nodeId = "root";
                        nodeText = "Root";
                    }
                }
                else
                {
                    // Regular node definition
                    Match nodeMatch = Regex.Match(trimmedLine, @"([A-Za-z0-9_]+)\s*(\(\(.*?\)\)|\[.*?\])");
                    if (nodeMatch.Success)
                    {
                        nodeId = nodeMatch.Groups[1].Value;
                        nodeText = ExtractNodeText(nodeMatch.Groups[2].Value);
                    }
                }
                
                // Store the node if we successfully extracted it
                if (!string.IsNullOrEmpty(nodeId))
                {
                    nodes[nodeId] = nodeText;
                    
                    // Store this node ID at current indentation level
                    indentationMap[indent] = nodeId;
                    
                    // If there's a parent at a lower indentation, create a relationship
                    for (int parentIndent = indent - 2; parentIndent >= 0; parentIndent -= 2)
                    {
                        if (indentationMap.TryGetValue(parentIndent, out string parentId))
                        {
                            relationships.Add($"{parentId} --> {nodeId}");
                            break; // Only connect to the immediate parent
                        }
                    }
                }
            }
            
            // Add node definitions
            foreach (var node in nodes)
            {
                graph.AppendLine($"    {node.Key}[\"{node.Value}\"]");
            }
            
            // Add relationships
            foreach (var rel in relationships)
            {
                graph.AppendLine($"    {rel}");
            }
            
            return graph.ToString();
        }
        
        /// <summary>
        /// Extracts node text from bracket or parenthesis notation
        /// </summary>
        private string ExtractNodeText(string nodeDefinition)
        {
            if (nodeDefinition.StartsWith("((") && nodeDefinition.EndsWith("))"))
            {
                return nodeDefinition.Substring(2, nodeDefinition.Length - 4);
            }
            else if (nodeDefinition.StartsWith("[") && nodeDefinition.EndsWith("]"))
            {
                return nodeDefinition.Substring(1, nodeDefinition.Length - 2);
            }
            return nodeDefinition;
        }
    }
}
