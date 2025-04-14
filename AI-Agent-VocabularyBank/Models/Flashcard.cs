using System;

namespace VocabularyBank.Models
{
    /// <summary>
    /// Represents a study flashcard for a vocabulary term.
    /// </summary>
    public class Flashcard
    {
        /// <summary>
        /// The vocabulary term or word being studied.
        /// </summary>
        public string Term { get; set; }
        
        /// <summary>
        /// The definition of the term.
        /// </summary>
        public string Definition { get; set; }
        
        /// <summary>
        /// An example sentence using the term.
        /// </summary>
        public string Example { get; set; }
        
        /// <summary>
        /// Additional context about how the term is used in the specific subject.
        /// </summary>
        public string Context { get; set; }
        
        /// <summary>
        /// The date and time when the flashcard was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
