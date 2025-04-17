namespace DiagramGenerator.Models
{
    public class Diagram
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public DiagramType Type { get; set; }
        public List<DiagramElement> Elements { get; set; } = new();
        public string RawContent { get; set; } = string.Empty;
        public string WhiteboardUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Diagram(string title, DiagramType type)
        {
            Title = title;
            Type = type;
        }
    }

    public enum DiagramType
    {
        Flowchart,
        MindMap,
        EntityRelationship,
        SequenceDiagram,
        ClassDiagram,
        Gantt,
        Generic
    }
}
