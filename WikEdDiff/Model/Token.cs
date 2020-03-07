namespace WikEdDiff.Model
{
    public class Token
    {
        /// <summary>
        /// Previous list item.
        /// </summary>
        public int? Prev { get; set; }

        /// <summary>
        /// Next list item.
        /// </summary>
        public int? Next { get; set; }

        /// <summary>
        /// Index of corresponding token in new or old text (OA and NA).
        /// </summary>
        public int? Link { get; set; }

        /// <summary>
        /// List enumeration number.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Token is unique word in text.
        /// </summary>
        public bool Unique { get; set; }

        /// <summary>
        /// Token string.
        /// </summary>
        public string TokenString { get; set; }
    }
}
