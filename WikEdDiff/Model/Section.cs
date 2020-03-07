namespace WikEdDiff.Model
{
    public class Section
    {
        /// <summary>
        /// First block in section.
        /// </summary>
        public int BlockStart { get; set; }

        /// <summary>
        /// Last block in section.
        /// </summary>
        public int BlockEnd { get; set; }
    }
}
