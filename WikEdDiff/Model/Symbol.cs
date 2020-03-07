namespace WikEdDiff.Model
{
    /// <summary>
    /// Object that holds token counters and pointers.
    /// </summary>
    public class Symbol
    {
        /// <summary>
        /// New text token counter (NC).
        /// </summary>
        public int NewCount { get; set; }

        /// <summary>
        /// Old text token counter (OC).
        /// </summary>
        public int OldCount { get; set; }

        /// <summary>
        /// Token index in _newText.tokens.
        /// </summary>
        public int? NewToken { get; set; }

        /// <summary>
        /// Token index in _oldText.tokens.
        /// </summary>
        public int? OldToken { get; set; }
    }
}
