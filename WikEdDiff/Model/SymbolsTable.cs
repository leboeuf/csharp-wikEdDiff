using System.Collections.Generic;

namespace WikEdDiff.Model
{
    /// <summary>
    /// Symbols table for whole text at all refinement levels.
    /// </summary>
    public class SymbolsTable
    {
        /// <summary>
        /// Token list for new or old string (doubly-linked list).
        /// </summary>
        public List<Symbol> Token { get; set; } = new List<Symbol>();

        public Dictionary<string, int> HashTable { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Flag: at least one unique token pair has been linked.
        /// </summary>
        public bool Linked { get; set; }

        /// <summary>
        /// List of objects that hold token counters and pointers.
        /// </summary>
        public List<Symbol> Symbols { get; set; } = new List<Symbol>();
    }
}
