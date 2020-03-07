using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WikEdDiff.Model;

namespace WikEdDiff
{
    /// <summary>
    /// Represents a single text version (old or new one).
    /// </summary>
    public class WikEdDiffText
    {
        /// <summary>
        /// Parent object for configuration settings and debugging methods.
        /// </summary>
        public WikEdDiff Parent { get; set; }

        /// <summary>
        /// Text of this version.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Tokens list.
        /// </summary>
        public List<Token> Tokens { get; set; } = new List<Token>();

        /// <summary>
        /// First index of tokens list.
        /// </summary>
        public int? First { get; set; }

        /// <summary>
        /// Last index of tokens list.
        /// </summary>
        public int? Last { get; set; }

        /// <summary>
        /// Word counts for version text.
        /// </summary>
        public Dictionary<string, int> Words { get; set; } = new Dictionary<string, int>();

        public WikEdDiffText(string text, WikEdDiff parent)
        {
            Text = text;
            Parent = parent;
        }

        public void Initialize()
        {
            Text = Text.Replace("\r", string.Empty);

            WordParse(WikEdDiffConfiguration.RegularExpressions.CountWords);
            WordParse(WikEdDiffConfiguration.RegularExpressions.CountChunks);
        }

        /// <summary>
        /// Parse and count words and chunks for identification of unique words.
        /// </summary>
        /// <param name="regex">Regular expression for counting words.</param>
        private void WordParse(Regex regex)
        {
            var regExpMatch = regex.Matches(Text);
            var matchLength = regExpMatch.Count;
            for (var i = 0; i < matchLength; i++)
            {
                var word = regExpMatch[i];
                //if (Object.prototype.hasOwnProperty.call(Words, word) == false) // TODO: convert Object.Prototype.hasOwnProperty
                //{
                //    Words[word.Value] = 1;
                //}
                //else
                //{
                //    Words[word.Value]++;
                //}
            }
        }

        /// <summary>
        /// Split text into paragraph, line, sentence, chunk, word, or character tokens.
        /// </summary>  
        /// <param name="level">Level of splitting: paragraph, line, sentence, chunk, word, or character.</param>
        /// <param name="token">Index of token to be split, otherwise uses full text.</param>
        public void SplitText(string level, int? token = null)
        {
            int? prev = 0;
            int? next = 0;
            var current = Tokens.Count;
            var first = current;
            var text = "";

            // Split full text or specified token
            if (token != null)
            {
                text = Text;
            }
            else
            {
                prev = Tokens[token.Value].Prev;
                next = Tokens[token.Value].Next;
                text = Tokens[token.Value].TokenString;
            }

            // Split text into tokens, regExp match as separator
            var number = 0;
            var split = new List<string>();
            var lastIndex = 0;
            var regExp = WikEdDiffConfiguration.RegularExpressions.Split[level];
            var regExpMatches = regExp.Matches(text);
            for (int i = 0; i < regExpMatches.Count; i++)
            {
                var regExpMatch = regExpMatches[i];
                if (regExpMatch.Index > lastIndex)
                {
                    split.Add(Text.Substring(lastIndex, regExpMatch.Index));
                }

                split.Add(regExpMatch.Value);
                //lastIndex = regExp.lastIndex; // TODO: Line 4444
            }

            // Cycle through new tokens
            var splitLength = split.Count;
            for (var i = 0; i < splitLength; i++)
            {
                // Insert current item, link to previous
                Tokens.Add(new Token
                {
                    TokenString = split[i],
                    Prev = prev,
                    Next = null,
                    Link = null,
                    //Number = null,
                    Unique = false
                });
                number++;

                // Link previous item to current
                if (prev != null)
                {
                    Tokens[prev.Value].Next = current;
                }
                prev = current;
                current++;
            }

            // Connect last new item and existing next item
            if (number > 0 && token != null)
            {
                if (prev != null)
                {
                    Tokens[prev.Value].Next = next;
                }
                if (next != null )
                {
                    Tokens[next.Value].Prev = prev;
                }
            }

            // Set text first and last token index
            if (number > 0 )
            {
                // Initial text split
                if (token == null)
                {
                    First = 0;
                    Last = prev;
                }

                // First or last token has been split
                else
                {
                    if (token == First)
                    {
                        First = first;
                    }
                    if (token == Last)
                    {
                        Last = prev;
                    }
                }
            }
        }

        /// <summary>
        /// Split unique unmatched tokens into smaller tokens.
        /// </summary>
        /// <param name="regExp">Level of splitting: line, sentence, chunk, or word.</param>
        public void SplitRefine(string regExp)
        {
            // Cycle through tokens list
            var i = First;
            while (i != null)
            {
                // Refine unique unmatched tokens into smaller tokens
                if (Tokens[i.Value].Link == null)
                {
                    SplitText(regExp, i);
                }
                i = Tokens[i.Value].Next;
            }
        }

        /// <summary>
        /// Enumerate text token list before detecting blocks.
        /// </summary>
        public void EnumerateTokens()
        {
            // Enumerate tokens list
            var number = 0;
            var i = First;
            while (i != null)
            {
                Tokens[i.Value].Number = number;
                number++;
                i = Tokens[i.Value].Next;
            }
        }
    }
}
