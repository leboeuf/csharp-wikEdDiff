namespace WikEdDiff.Model
{
    /// <summary>
    /// Block data (consecutive text tokens) in new text order.
    /// </summary>
    public class Block
    {
        public int? OldBlock { get; set; }
        public int? NewBlock { get; set; }
        public int? OldNumber { get; set; }
        public int? NewNumber { get; set; }
        public int? OldStart { get; set; }
        public int? Count { get; set; }
        public bool? Unique { get; set; }
        public int? Words { get; set; }
        public int Chars { get; set; }
        public string Type { get; set; }
        public int? Section { get; set; }
        public int? Group { get; set; }
        public bool? Fixed { get; set; }
        public int Moved { get; set; }
        public string Text { get; set; }
        public int Number { get; set; }
    }
}
