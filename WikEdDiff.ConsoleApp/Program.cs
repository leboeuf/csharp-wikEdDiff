namespace WikEdDiff.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var originalText = @"Salt is found in greater or less quantities in almost every substance on earth, but [the waters of the sea appear to have been its first great magazine]. It is found there dissolved in certain proportions, and two purposes are thus served, namely, the preservation of that vast body of waters, which otherwise, from the innumerable objects of animal and vegetable life within it, would become an insupportable mass of corruption, and the supplying of a large proportion of the salt we require in our food, and for other purposes. The quantity of salt contained in the sea (according to the best authorities) amounts to four hundred thousand billion cubic feet, which, if piled up, would form a mass one hundred and forty miles long, as many broad, and as many high, or, otherwise disposed, would cover the whole of Europe, islands, seas, and all, to the height of the summit of Mont Blanc, which is about sixteen thousand feet in height.";
            var modifiedText = @"This is great. It is found there dissolved in certain proportions, and two purposes are thus served, namely, the preservation of that vast body of waters, which otherwise, from the innumerable objects of animal and vegetable life within it, would become an insupportable mass of corruption, and the supplying of a large proportion of the salt we require in our food, and for other purposes. Salt is found in greater or less quantities in almost every substance on earth, but [the waters of the sea seem to have been its first great]. This sentence should not impact anything. The quantity of salt contained in the sea (according to the best authorities) amounts to four hundred thousand billion cubic feet, which, if piled up, would form a mass one hundred and forty miles long, as many broad, and as many high, or, otherwise disposed, would cover the whole of Europe, islands, seas, and all, to the height of the summit of Mont Blanc, which is about sixteen thousand feet in height.";

            var wikEdDiff = new WikEdDiff
            {
                Configuration = new WikEdDiffConfiguration
                {

                }
            };

            wikEdDiff.Diff(originalText, modifiedText);
        }
    }
}
