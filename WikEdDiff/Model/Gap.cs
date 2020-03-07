namespace WikEdDiff.Model
{
    public class Gap
    {
        public int? NewFirst { get; set; }
        public int? NewLast { get; set; }
        public int NewTokens { get; set; }
        public int? OldFirst { get; set; }
        public int? OldLast { get; set; }
        public int OldTokens { get; set; }
        public bool CharSplit { get; set; }
    }
}
