namespace WikEdDiff.Model
{
    public class Group : Block
    {
        public int BlockStart { get; set; }
        public int BlockEnd { get; set; }
        public int? MaxWords { get; set; }
        public int? MovedFrom { get; set; }
        public int? Color { get; set; }
    }
}
