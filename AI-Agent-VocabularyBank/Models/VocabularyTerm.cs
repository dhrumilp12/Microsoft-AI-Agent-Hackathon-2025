namespace VocabularyBank.Models
{
    /// <summary>
    /// Represents a vocabulary term with its definition and related information.
    /// </summary>
    public class VocabularyTerm
    {
        /// <summary>
        /// The vocabulary term or word.
        /// </summary>
        public string Term { get; set; }
        
        /// <summary>
        /// The definition of the term based on its context.
        /// </summary>
        public string Definition { get; set; }
        
        /// <summary>
        /// An example sentence showing the term used in context.
        /// </summary>
        public string Example { get; set; }
        
        /// <summary>
        /// Additional context information about how the term is used in the specific subject.
        /// </summary>
        public string Context { get; set; }
        
        /// <summary>
        /// The number of times this term appears in the source text.
        /// </summary>
        public int Occurrences { get; set; }
    }
}
