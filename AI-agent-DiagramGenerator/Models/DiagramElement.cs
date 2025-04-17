namespace DiagramGenerator.Models
{
    public class DiagramElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = string.Empty;
        public ElementType Type { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
        public List<string> ConnectedToIds { get; set; } = new();
        public List<DiagramElement> Children { get; set; } = new();

        public DiagramElement(string label, ElementType type)
        {
            Label = label;
            Type = type;
        }
    }

    public enum ElementType
    {
        Node,
        Edge,
        Group,
        Process,
        Decision,
        Start,
        End,
        Database,
        Document,
        SubProcess
    }
}
