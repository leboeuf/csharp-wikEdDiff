using System;
using System.Linq;
using System.Collections.Generic;
using WikEdDiff.Model;
using System.Text.RegularExpressions;

namespace WikEdDiff
{
    public class WikEdDiff
    {
        /// <summary>
        /// Old text version object with text and token list
        /// </summary>
        private WikEdDiffText _oldText;

        /// <summary>
        /// New text version object with text and token list
        /// </summary>
        private WikEdDiffText _newText;

        public WikEdDiffConfiguration Configuration { get; set; }

        /// <summary>
        /// Symbols table for whole text at all refinement levels.
        /// </summary>
        public SymbolsTable Symbols { get; set; } = new SymbolsTable();

        /// <summary>
        /// Linked region borders downwards, [new index, old index].
        /// </summary>
        public List<Tuple<int, int>> BordersDown { get; set; } = new List<Tuple<int, int>>();

        /// <summary>
        /// Linked region borders upwards, [new index, old index].
        /// </summary>
        public List<Tuple<int, int>> BordersUp { get; set; } = new List<Tuple<int, int>>();

        /// <summary>
        /// Diff fragment list for markup, abstraction layer for customization.
        /// </summary>
        public List<Fragment> Fragments { get; set; } = new List<Fragment>();

        /// <summary>
        /// Html code of diff.
        /// </summary>
        public string Html { get; internal set; }

        /// <summary>
        /// Block data (consecutive text tokens) in new text order.
        /// </summary>
        public List<Block> Blocks { get; set; } = new List<Block>();

        /// <summary>
        /// Block sections with no block move crosses outside a section.
        /// </summary>
        public List<Section> Sections { get; set; } = new List<Section>();

        /// <summary>
        /// Section blocks that are consecutive in old text order.
        /// </summary>
        public List<Model.Group> Groups { get; set; } = new List<Model.Group>();

        /// <summary>
        /// Maximal detected word count of all linked blocks.
        /// </summary>
        public int MaxWords { get; set; }

        /// <summary>
        /// Main diff method.
        /// </summary>
        /// <param name="oldString">Old text version</param>
        /// <param name="newString">New text version</param>
        /// <returns>Html code of diff</returns>
        public string Diff(string oldString, string newString)
        {
            // Strip trailing newline
            if (Configuration.StripTrailingNewline)
            {
                if (newString.EndsWith("\n") && oldString.EndsWith("\n"))
                {
                    newString = newString.Substring(0, newString.Length - 1);
                    oldString = oldString.Substring(0, oldString.Length - 1);
                }
            }

            // Load version strings into WikEdDiffText objects
            _newText = new WikEdDiffText(newString, this);
            _oldText = new WikEdDiffText(oldString, this);

            // Trap trivial changes: no change
            if (_newText.Text == _oldText.Text)
            {
                // TODO: line 956
                //this.html =
                //    Configuration.htmlCode.containerStart +
                //    Configuration.htmlCode.noChangeStart +
                //    this.htmlEscape(Configuration.msg['wiked-diff-empty']) +
                //    Configuration.htmlCode.noChangeEnd +
                //    Configuration.htmlCode.containerEnd;
                return Html;
            }

            // Trap trivial changes: old text deleted
            if (string.IsNullOrEmpty(_oldText.Text) ||
                (_oldText.Text == "\n" && _newText.Text.EndsWith("\n")))
            {
                // TODO: line 972
                //this.html =
                //Configuration.htmlCode.containerStart +
                //Configuration.htmlCode.fragmentStart +
                //Configuration.htmlCode.insertStart +
                //this.htmlEscape(this.newText.text) +
                //Configuration.htmlCode.insertEnd +
                //Configuration.htmlCode.fragmentEnd +
                //Configuration.htmlCode.containerEnd;
                return Html;
            }

            // Trap trivial changes: old text deleted
            if (string.IsNullOrEmpty(_newText.Text) ||
                (_newText.Text == "\n" && _oldText.Text.EndsWith("\n")))
            {
                // TODO: line 990
                //this.html =
                //Configuration.htmlCode.containerStart +
                //Configuration.htmlCode.fragmentStart +
                //Configuration.htmlCode.deleteStart +
                //this.htmlEscape(this.oldText.text) +
                //Configuration.htmlCode.deleteEnd +
                //Configuration.htmlCode.fragmentEnd +
                //Configuration.htmlCode.containerEnd;
                return Html;
            }

            // Split new and old text into paragraps
            _newText.SplitText("paragraph");
            _oldText.SplitText("paragraph");

            // Calculate diff
            CalculateDiff("line");

            // Refine different paragraphs into lines
            _newText.SplitRefine("line");
            _oldText.SplitRefine("line");

            // Calculate refined diff
            CalculateDiff("line");

            // Refine different lines into sentences
            _newText.SplitRefine("sentence");
            _oldText.SplitRefine("sentence");

            // Calculate refined diff
            CalculateDiff("sentence");

            // Refine different lines into chunks
            _newText.SplitRefine("chunk");
            _oldText.SplitRefine("chunk");

            // Calculate refined diff
            CalculateDiff("chunk");

            // Refine different lines into words
            _newText.SplitRefine("word");
            _oldText.SplitRefine("word");

            // Calculate refined diff information with recursion for unresolved gaps
            CalculateDiff("word", true);

            // Slide gaps
            SlideGaps(_newText, _oldText);
            SlideGaps(_oldText, _newText);

            // Split tokens into chars
            if (Configuration.CharDiff)
            {
                // Split tokens into chars in selected unresolved gaps
                SplitRefineChars();

                // Calculate refined diff information with recursion for unresolved gaps
                CalculateDiff("character", true);

                // Slide gaps
                SlideGaps(_newText, _oldText);
                SlideGaps(_oldText, _newText);
            }

            // Free memory
            // TODO: line 1102
            //this.symbols = undefined;
            //this.bordersDown = undefined;
            //this.bordersUp = undefined;
            //this.newText.words = undefined;
            //this.oldText.words = undefined;

            // Enumerate token lists
            _newText.EnumerateTokens();
            _oldText.EnumerateTokens();

            // Detect moved blocks
            DetectBlocks();

            // Free memory
            _newText.Tokens.Clear();
            _oldText.Tokens.Clear();

            // Assemble blocks into fragment table
            GetDiffFragments();

            // Free memory
            // TODO: line 1129
            //this.blocks = undefined;
            //this.groups = undefined;
            //this.sections = undefined;

            // Clipping
            if (Configuration.FullDiff)
            {
                ClipDiffFragments();
            }

            // Create html formatted diff code from diff fragments
            //GetDiffHtml(); // TODO

            // No change
            if (string.IsNullOrEmpty(Html))
            {
                // TODO: line 1177
                //Html = Configuration.htmlCode.containerStart +
                //Configuration.htmlCode.noChangeStart +
                //this.htmlEscape(Configuration.msg['wiked-diff-empty']) +
                //Configuration.htmlCode.noChangeEnd +
                //Configuration.htmlCode.containerEnd;
            }

            return Html;
        }

        /// <summary>
        /// Split tokens into chars in the following unresolved regions (gaps):
        ///   - One token became connected or separated by space or dash (or any token)
        ///   - Same number of tokens in gap and strong similarity of all tokens:
        ///     - Addition or deletion of flanking strings in tokens
        ///     - Addition or deletion of internal string in tokens
        ///     - Same length and at least 50 % identity
        ///     - Same start or end, same text longer than different text
        /// Identical tokens including space separators will be linked,
        ///   resulting in word-wise char-level diffs
        /// </summary>
        private void SplitRefineChars()
        {
            // Find corresponding gaps.

            // Cycle through new text tokens list
            var gaps = new List<Gap>();
            int? gap = null;
            var i = _newText.First;
            var j = _oldText.First;

            while (i != null)
            {
                // Get token links
                var newLink = _newText.Tokens[i.Value].Link;
                int? oldLink = null;
                if (j != null)
                {
                    oldLink = _oldText.Tokens[j.Value].Link;
                }

                // Start of gap in new and old
                if (gap == null && newLink == null && oldLink == null)
                {
                    gap = gaps.Count;
                    gaps.Add(new Gap
                    {
                        NewFirst = i,
					    NewLast = i,
					    NewTokens = 1,
					    OldFirst = j,
					    OldLast = j,
					    //OldTokens = null,
					    //CharSplit = null
                    });
                }

                // Count chars and tokens in gap
                else if (gap != null && newLink == null)
                {
                    gaps[gap.Value].NewLast = i;
                    gaps[gap.Value].NewTokens++;
                }

                // Gap ended
                else if (gap != null && newLink != null)
                {
                    gap = null;
                }

                // Next list elements
                if (newLink != null)
                {
                    j = _oldText.Tokens[newLink.Value].Next;
                }
                i = _newText.Tokens[i.Value].Next;
            }

            // Cycle through gaps and add old text gap data
            var gapsLength = gaps.Count;
            for (gap = 0; gap < gapsLength; gap++)
            {
                // Cycle through old text tokens list
                var jj = gaps[gap.Value].OldFirst;
                while (
                    jj != null &&
                    _oldText.Tokens[jj.Value] != null &&
                    _oldText.Tokens[jj.Value].Link == null
                )
                {

                    // Count old chars and tokens in gap
                    gaps[gap.Value].OldLast = jj;
                    gaps[gap.Value].OldTokens++;

                    jj = _oldText.Tokens[jj.Value].Next;
                }
            }

            // Select gaps of identical token number and strong similarity of all tokens.
            gapsLength = gaps.Count;
            for (gap = 0; gap < gapsLength; gap++)
            {
                var charSplit = true;

                // Not same gap length
                if (gaps[gap.Value].NewTokens != gaps[gap.Value].OldTokens)
                {
                    // One word became separated by space, dash, or any string
                    if (gaps[gap.Value].NewTokens == 1 && gaps[gap.Value].OldTokens == 3)
                    {
                        var token = _newText.Tokens[gaps[gap.Value].NewFirst.Value].TokenString;
                        var tokenFirst = _oldText.Tokens[gaps[gap.Value].OldFirst.Value].TokenString;
                        var tokenLast = _oldText.Tokens[gaps[gap.Value].OldLast.Value].TokenString;
                        if (
                            token.IndexOf(tokenFirst) != 0 ||
                            token.IndexOf(tokenLast) != token.Length - tokenLast.Length
                        )
                        {
                            continue;
                        }
                    }
                    else if (gaps[gap.Value].OldTokens == 1 && gaps[gap.Value].NewTokens == 3)
                    {
                        var token = _oldText.Tokens[gaps[gap.Value].OldFirst.Value].TokenString;
                        var tokenFirst = _newText.Tokens[gaps[gap.Value].NewFirst.Value].TokenString;
                        var tokenLast = _newText.Tokens[gaps[gap.Value].NewLast.Value].TokenString;
                        if (
                            token.IndexOf(tokenFirst) != 0 ||
                            token.IndexOf(tokenLast) != token.Length - tokenLast.Length
                        )
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                    gaps[gap.Value].CharSplit = true;
                }

                // Cycle through new text tokens list and set charSplit
                else
                {
                    var ii = gaps[gap.Value].NewFirst;
                    var jj = gaps[gap.Value].OldFirst;

                    while (ii != null)
                    {
                        var newToken = _newText.Tokens[ii.Value].TokenString;
                        var oldToken = _oldText.Tokens[jj.Value].TokenString;

                        // Get shorter and longer token
                        string shorterToken;
                        string longerToken;
                        if (newToken.Length < oldToken.Length)
                        {
                            shorterToken = newToken;
                            longerToken = oldToken;
                        }
                        else
                        {
                            shorterToken = oldToken;
                            longerToken = newToken;
                        }

                        // Not same token length
                        if (newToken.Length != oldToken.Length)
                        {
                            // Test for addition or deletion of internal string in tokens

                            // Find number of identical chars from left
                            var left = 0;
                            while (left < shorterToken.Length)
                            {
                                if (newToken[left] != oldToken[left])
                                {
                                    break;
                                }
                                left++;
                            }

                            // Find number of identical chars from right
                            var right = 0;
                            while (right < shorterToken.Length)
                            {
                                if (
                                    newToken[newToken.Length - 1 - right] !=
                                    oldToken[oldToken.Length - 1 - right]
                                )
                                {
                                    break;
                                }
                                right++;
                            }

                            // No simple insertion or deletion of internal string
                            if (left + right != shorterToken.Length)
                            {
                                // Not addition or deletion of flanking strings in tokens
                                // Smaller token not part of larger token
                                if (longerToken.IndexOf(shorterToken) == -1)
                                {
                                    // Same text at start or end shorter than different text
                                    if (left < shorterToken.Length / 2 && (right < shorterToken.Length / 2))
                                    {
                                        // Do not split into chars in this gap
                                        charSplit = false;
                                        break;
                                    }
                                }
                            }
                        }

                        // Same token length
                        else if (newToken != oldToken)
                        {
                            // Tokens less than 50 % identical
                            var ident = 0;
                            var tokenLength = shorterToken.Length;
                            for (var pos = 0; pos < tokenLength; pos++)
                            {
                                if (shorterToken[pos] == longerToken[pos])
                                {
                                    ident++;
                                }
                            }
                            if (ident / shorterToken.Length < 0.49)
                            {

                                // Do not split into chars this gap
                                charSplit = false;
                                break;
                            }
                        }

                        // Next list elements
                        if (ii == gaps[gap.Value].NewLast)
                        {
                            break;
                        }
                        ii = _newText.Tokens[ii.Value].Next;
                        jj = _oldText.Tokens[jj.Value].Next;
                    }
                    gaps[gap.Value].CharSplit = charSplit;
                }
            }

            // Refine words into chars in selected gaps.
            gapsLength = gaps.Count;
            for (gap = 0; gap < gapsLength; gap++)
            {
                if (gaps[gap.Value].CharSplit == true)
                {
                    // Cycle through new text tokens list, link spaces, and split into chars
                    var ii = gaps[gap.Value].NewFirst;
                    var jj = gaps[gap.Value].OldFirst;
                    var newGapLength = ii - gaps[gap.Value].NewLast;
                    var oldGapLength = jj - gaps[gap.Value].OldLast;
                    while (ii != null || jj != null)
                    {

                        // Link identical tokens (spaces) to keep char refinement to words
                        if (
                            newGapLength == oldGapLength &&
                            _newText.Tokens[ii.Value].TokenString == _oldText.Tokens[jj.Value].TokenString
                        )
                        {
                            _newText.Tokens[ii.Value].Link = jj;
                            _oldText.Tokens[jj.Value].Link = ii;
                        }

                        // Refine words into chars
                        else
                        {
                            if (ii != null)
                            {
                                _newText.SplitText("character", ii);
                            }
                            if (jj != null)
                            {
                                _oldText.SplitText("character", jj);
                            }
                        }

                        // Next list elements
                        if (ii == gaps[gap.Value].NewLast)
                        {
                            ii = null;
                        }
                        if (jj == gaps[gap.Value].OldLast)
                        {
                            jj = null;
                        }
                        if (ii != null)
                        {
                            ii = _newText.Tokens[ii.Value].Next;
                        }
                        if (jj != null)
                        {
                            jj = _oldText.Tokens[jj.Value].Next;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculate diff information, can be called repeatedly during refining.
        /// Links corresponding tokens from old and new text.
        /// Steps:
        ///   Pass 1: parse new text into symbol table
        ///   Pass 2: parse old text into symbol table
        ///   Pass 3: connect unique matching tokens
        ///   Pass 4: connect adjacent identical tokens downwards
        ///   Pass 5: connect adjacent identical tokens upwards
        ///   Repeat with empty symbol table (against crossed-over gaps)
        ///   Recursively diff still unresolved regions downwards with empty symbol table
        ///   Recursively diff still unresolved regions upwards with empty symbol table
        /// </summary>
        /// <param name="level">Split level: 'paragraph', 'line', 'sentence', 'chunk', 'word', 'character'.</param>
        /// <param name="recurse">Enable recursion.</param>
        /// <param name="repeating">Currently repeating with empty symbol table.</param>
        /// <param name="newStart">Text object tokens indice.</param>
        /// <param name="oldStart">Text object tokens indice.</param>
        /// <param name="up"></param>
        /// <param name="recursionLevel">Recursion level.</param>
        private void CalculateDiff(string level, bool recurse = false, bool repeating = false, int? newStart = null, int? oldStart = null, bool up = false, int recursionLevel = 0)
        {
            // Set defaults
            if (newStart == null)
            {
                newStart = _newText.First;
            }
            if (oldStart == null)
            {
                oldStart = _oldText.First;
            }

            // Get object symbols table and linked region borders
            SymbolsTable symbols;
            List<Tuple<int, int>> bordersDown;
            List<Tuple<int, int>> bordersUp;
            if (recursionLevel == 0 && repeating == false)
            {
                symbols = Symbols;
                bordersDown = BordersDown;
                bordersUp = BordersUp;
            }
            else
            {
                // Create empty local symbols table and linked region borders arrays
                symbols = new SymbolsTable();
                bordersDown = new List<Tuple<int, int>>();
                bordersUp = new List<Tuple<int, int>>();
            }

            // Updated versions of linked region borders
            var bordersUpNext = new List<Tuple<int, int>>();
            var bordersDownNext = new List<Tuple<int, int>>();

            /**
             * Pass 1: parse new text into symbol table.
             */

            // Cycle through new text tokens list
            int? ind = newStart;
            while (ind != null)
            {
                if (_newText.Tokens[ind.Value].Link == null)
                {
                    // Add new entry to symbol table
                    var token = _newText.Tokens[ind.Value].TokenString;
                    if (!symbols.HashTable.ContainsKey(token))
                    {
                        symbols.HashTable[token] = symbols.Token.Count;
                        symbols.Token.Add(new Symbol
                        {
                            NewCount = 1,
                            OldCount = 0,
                            NewToken = ind,
                            OldToken = null
                        });
                    }

                    // Or update existing entry
                    else
                    {
                        // Increment token counter for new text
                        var hashToArray = symbols.HashTable[token];
                        symbols.Token[hashToArray].NewCount++;
                    }
                }

                // Stop after gap if recursing
                else if (recursionLevel > 0)
                {
                    break;
                }

                // Get next token
                if (up == false)
                {
                    ind = _newText.Tokens[ind.Value].Next;
                }
                else
                {
                    ind = _newText.Tokens[ind.Value].Prev;
                }
            }

            /**
		     * Pass 2: parse old text into symbol table.
		     */

            // Cycle through old text tokens list
            int? j = oldStart;
            while (j != null)
            {
                if (_oldText.Tokens[j.Value].Link == null)
                {
                    // Add new entry to symbol table
                    var token = _oldText.Tokens[j.Value].TokenString;
                    if (!symbols.HashTable.ContainsKey(token))
                    {
                        symbols.HashTable[token] = symbols.Token.Count;
                        symbols.Token.Add(new Symbol
                        {
                            NewCount = 0,
                            OldCount = 1,
                            NewToken = null,
                            OldToken = j
                        });
                    }

                    // Or update existing entry
                    else
                    {
                        // Increment token counter for new text
                        var hashToArray = symbols.HashTable[token];
                        symbols.Token[hashToArray].OldCount++;

                        // Add token number for old text
                        symbols.Token[hashToArray].OldToken = j;
                    }
                }

                // Stop after gap if recursing
                else if (recursionLevel > 0)
                {
                    break;
                }

                // Get next token
                if (up == false)
                {
                    j = _oldText.Tokens[j.Value].Next;
                }
                else
                {
                    j = _oldText.Tokens[j.Value].Prev;
                }
            }

            /**
		     * Pass 3: connect unique tokens.
		     */

            // Cycle through symbol array
            var symbolsLength = symbols.Token.Count;
            for (var indx = 0; indx < symbolsLength; indx++)
            {
                // Find tokens in the symbol table that occur only once in both versions
                if (symbols.Token[indx].NewCount == 1 && symbols.Token[indx].OldCount == 1)
                {
                    var newToken = symbols.Token[indx].NewToken.Value;
                    var oldToken = symbols.Token[indx].OldToken.Value;
                    var newTokenObj = _newText.Tokens[newToken];
                    var oldTokenObj = _oldText.Tokens[oldToken];

                    // Connect from new to old and from old to new
                    if (newTokenObj.Link == null)
                    {
                        // Do not use spaces as unique markers
                        if (WikEdDiffConfiguration.RegularExpressions.BlankOnlyToken.IsMatch(newTokenObj.TokenString))
                        {
                            // Link new and old tokens
                            newTokenObj.Link = oldToken;
                            oldTokenObj.Link = newToken;
                            symbols.Linked = true;

                            // Save linked region borders
                            bordersDown.Add(new Tuple<int, int>(newToken, oldToken));
                            bordersUp.Add(new Tuple<int, int>(newToken, oldToken));

                            // Check if token contains unique word
                            if (recursionLevel == 0)
                            {
                                var unique = false;
                                if (level == "character")
                                {
                                    unique = true;
                                }
                                else
                                {
                                    var token = newTokenObj.TokenString;
                                    var words = WikEdDiffConfiguration.RegularExpressions.CountWords.Matches(token)
                                        .Concat(WikEdDiffConfiguration.RegularExpressions.CountChunks.Matches(token));

                                    // Unique if longer than min block length
                                    var wordsLength = words.Length;
                                    if (wordsLength >= Configuration.BlockMinLength)
                                    {
                                        unique = true;
                                    }

                                    // Unique if it contains at least one unique word
                                    else
                                    {
                                        for (int ii = 0; ii < wordsLength; ii++)
                                        {
                                            var word = words[ii];
                                            if (_oldText.Words.ContainsKey(word) &&
                                                _oldText.Words[word] == 1 &&
                                                _newText.Words.ContainsKey(word) &&
                                                _newText.Words[word] == 1)
                                            {
                                                unique = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                // Set unique
                                if (unique == true)
                                {
                                    newTokenObj.Unique = true;
                                    oldTokenObj.Unique = true;
                                }
                            }
                        }
                    }
                }
            }

            // Continue passes only if unique tokens have been linked previously
            if (!symbols.Linked)
            {
                return;
            }

            /**
			 * Pass 4: connect adjacent identical tokens downwards.
			 */

            // Cycle through list of linked new text tokens
            var bordersLength = bordersDown.Count;
            for (var match = 0; match < bordersLength; match++)
            {
                int? ii = bordersDown[match].Item1;
                int? jj = bordersDown[match].Item2;

                // Next down
                var iMatch = ii;
                var jMatch = jj;
                ii = _newText.Tokens[ii.Value].Next;
                jj = _oldText.Tokens[jj.Value].Next;

                // Cycle through new text list gap region downwards
                while (
                    ii != null &&
                    jj != null &&
                    _newText.Tokens[ii.Value].Link == null &&
                    _oldText.Tokens[jj.Value].Link == null
                )
                {
                    // Connect if same token
                    if (_newText.Tokens[ii.Value].TokenString == _oldText.Tokens[jj.Value].TokenString)
                    {
                        _newText.Tokens[ii.Value].Link = jj;
                        _oldText.Tokens[jj.Value].Link = ii;
                    }

                    // Not a match yet, maybe in next refinement level
                    else
                    {
                        bordersDownNext.Add(new Tuple<int, int>(iMatch.Value, jMatch.Value));
                        break;
                    }

                    // Next token down
                    iMatch = ii;
					jMatch = jj;
					ii = _newText.Tokens[ii.Value].Next;
					jj = _oldText.Tokens[jj.Value].Next;
                }
            }

            /**
			 * Pass 5: connect adjacent identical tokens upwards.
			 */

            // Cycle through list of connected new text tokens
            bordersLength = bordersUp.Count;
            for (var match = 0; match < bordersLength; match++)
            {
                int? ii = bordersUp[match].Item1;
                int? jj = bordersUp[match].Item2;

                // Next up
                var iMatch = ii;
				var jMatch = jj;
				ii = _newText.Tokens[ii.Value].Prev;
				jj = _oldText.Tokens[jj.Value].Prev;

				// Cycle through new text gap region upwards
				while (
					ii != null &&
					jj != null &&
                    _newText.Tokens[ii.Value].Link == null &&
                    _oldText.Tokens[jj.Value].Link == null)
                {

					// Connect if same token
					if (_newText.Tokens[ii.Value].TokenString == _oldText.Tokens[jj.Value].TokenString)
                    {
                        _newText.Tokens[ii.Value].Link = jj;
                        _oldText.Tokens[jj.Value].Link = ii;
					}

					// Not a match yet, maybe in next refinement level
					else {
                        bordersUpNext.Add(new Tuple<int, int>(iMatch.Value, jMatch.Value));
                        break;
					}

					// Next token up
					iMatch = ii;
					jMatch = jj;
					ii = _newText.Tokens[ii.Value].Prev;
					jj = _oldText.Tokens[jj.Value].Prev;
				}
			}

            /**
			 * Connect adjacent identical tokens downwards from text start.
			 * Treat boundary as connected, stop after first connected token.
			 */

            // Only for full text diff
            if (recursionLevel == 0 && repeating == false)
            {
                // From start
                var ii = _newText.First;
                var jj = _oldText.First;
                int? iMatch = null;
                int? jMatch = null;

                // Cycle through old text tokens down
                // Connect identical tokens, stop after first connected token
                while (
                    ii != null &&
                    jj != null &&
                    _newText.Tokens[ii.Value].Link == null &&
                    _oldText.Tokens[jj.Value].Link == null &&
                    _newText.Tokens[ii.Value].TokenString == _oldText.Tokens[jj.Value].TokenString
                )
                {
                    _newText.Tokens[ii.Value].Link = jj;
                    _oldText.Tokens[jj.Value].Link = ii;
                    iMatch = ii;
                    jMatch = jj;
                    ii = _newText.Tokens[ii.Value].Next;
                    jj = _oldText.Tokens[jj.Value].Next;
                }
                if (iMatch != null)
                {
                    bordersDownNext.Add(new Tuple<int, int>(iMatch.Value, jMatch.Value));
                }

                // From end
                ii = _newText.Last;
                jj = _oldText.Last;
                iMatch = null;
                jMatch = null;

                // Cycle through old text tokens up
                // Connect identical tokens, stop after first connected token
                while (
                    ii != null &&
                    jj != null &&
                    _newText.Tokens[ii.Value].Link == null &&
                    _oldText.Tokens[jj.Value].Link == null &&
                    _newText.Tokens[ii.Value].TokenString == _oldText.Tokens[jj.Value].TokenString
                )
                {
                    _newText.Tokens[ii.Value].Link = jj;
                    _oldText.Tokens[jj.Value].Link = ii;
                    iMatch = ii;
                    jMatch = jj;
                    ii = _newText.Tokens[ii.Value].Prev;
                    jj = _oldText.Tokens[jj.Value].Prev;
                }
                if (iMatch != null)
                {
                    bordersUpNext.Add(new Tuple<int, int>(iMatch.Value, jMatch.Value));
                }
            }

            // Save updated linked region borders to object
            if (recursionLevel == 0 && repeating == false)
            {
                BordersDown = bordersDownNext;
                BordersUp = bordersUpNext;
            }

            // Merge local updated linked region borders into object
            else
            {
                BordersDown.AddRange(bordersDownNext);
                BordersUp.AddRange(bordersUpNext);
            }

            /**
			 * Repeat once with empty symbol table to link hidden unresolved common tokens in cross-overs.
			 * ("and" in "and this a and b that" -> "and this a and b that")
			 */

            if (repeating == false && Configuration.RepeatedDiff == true)
            {
                var repeat = true;
                CalculateDiff(level, recurse, repeat, newStart, oldStart, up, recursionLevel);
            }

            /**
			 * Refine by recursively diffing not linked regions with new symbol table.
			 * At word and character level only.
			 * Helps against gaps caused by addition of common tokens around sequences of common tokens.
			 */

            if (
                recurse == true &&
                Configuration.RepeatedDiff == true &&
                recursionLevel < Configuration.RecursionMax
            )
            {
                /**
				 * Recursively diff gap downwards.
				 */

                // Cycle through list of linked region borders
                bordersLength = bordersDownNext.Count;
                for (var match = 0; match < bordersLength; match++)
                {
                    int? ii = bordersDownNext[match].Item1;
                    int? jj = bordersDownNext[match].Item2;

                    // Next token down
                    ii = _newText.Tokens[ii.Value].Next;
                    jj = _oldText.Tokens[jj.Value].Next;

                    // Start recursion at first gap token pair
                    if (
                        ii != null &&
                        jj != null &&
                        _newText.Tokens[ii.Value].Link == null &&
                        _oldText.Tokens[jj.Value].Link == null
                    )
                    {
                        var repeat = false;
                        var dirUp = false;
                        CalculateDiff(level, recurse, repeat, ii.Value, jj.Value, dirUp, recursionLevel + 1);
                    }
                }

                /**
				 * Recursively diff gap upwards.
				 */

                // Cycle through list of linked region borders
                bordersLength = bordersUpNext.Count;
                for (var match = 0; match < bordersLength; match++)
                {
                    int? ii = bordersUpNext[match].Item1;
                    int? jj = bordersUpNext[match].Item2;

                    // Next token up
                    ii = _newText.Tokens[ii.Value].Prev;
                    jj = _oldText.Tokens[jj.Value].Prev;

                    // Start recursion at first gap token pair
                    if (
                        ii != null &&
                        jj != null &&
                        _newText.Tokens[ii.Value].Link == null &&
                        _oldText.Tokens[jj.Value].Link == null
                    )
                    {
                        var repeat = false;
                        var dirUp = true;
                        CalculateDiff(level, recurse, repeat, ii.Value, jj.Value, dirUp, recursionLevel + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Main method for processing raw diff data, extracting deleted, inserted, and moved blocks.
        /// 
        /// Scheme of blocks, sections, and groups (old block numbers):
        ///   Old:      1    2 3D4   5E6    7   8 9 10  11
        ///             |    ‾/-/_    X     |    >|<     |
        ///   New:      1  I 3D4 2  E6 5  N 7  10 9  8  11
        ///   Section:       0 0 0   1 1       2 2  2
        ///   Group:    0 10 111 2  33 4 11 5   6 7  8   9
        ///   Fixed:    .    +++ -  ++ -    .   . -  -   +
        ///   Type:     =  . =-= =  -= =  . =   = =  =   =
        /// </summary>
        private void DetectBlocks()
        {
            // Collect identical corresponding ('=') blocks from old text and sort by new text
            GetSameBlocks();

            // Collect independent block sections with no block move crosses outside a section
            GetSections();

            // Find groups of continuous old text blocks
            GetGroups();

            // Set longest sequence of increasing groups in sections as fixed (not moved)
            SetFixed();

            // Convert groups to insertions/deletions if maximum block length is too short
            // Only for more complex texts that actually have blocks of minimum block length
            var unlinkCount = 0;
            if (
                Configuration.UnlinkBlocks == true &&
                Configuration.BlockMinLength > 0 &&
                MaxWords >= Configuration.BlockMinLength
            )
            {
                // Repeat as long as unlinking is possible
                var unlinked = true;
                while (unlinked == true && unlinkCount < Configuration.UnlinkMax)
                {

                    // Convert '=' to '+'/'-' pairs
                    unlinked = UnlinkBlocks();

                    // Start over after conversion
                    if (unlinked == true)
                    {
                        unlinkCount++;
                        SlideGaps(_newText, _oldText);
                        SlideGaps(_oldText, _newText);

                        // Repeat block detection from start
                        MaxWords = 0;
                        GetSameBlocks();
                        GetSections();
                        GetGroups();
                        SetFixed();
                    }
                }
            }

            // Collect deletion ('-') blocks from old text
            GetDelBlocks();

            // Position '-' blocks into new text order
            PositionDelBlocks();

            // Collect insertion ('+') blocks from new text
            GetInsBlocks();

            // Set group numbers of '+' blocks
            SetInsGroups();

            // Mark original positions of moved groups
            InsertMarks();
        }

        /// <summary>
        /// Move gaps with ambiguous identical fronts to last newline border or otherwise last word border.
        /// </summary>
        private void SlideGaps(WikEdDiffText text, WikEdDiffText textLinked)
        {
            var regExpSlideBorder = WikEdDiffConfiguration.RegularExpressions.SlideBorder;
            var regExpSlideStop = WikEdDiffConfiguration.RegularExpressions.SlideStop;

            // Cycle through tokens list
            var i = text.First;
            int? gapStart = null;
            while (i != null)
            {

                // Remember gap start
                if (gapStart == null && text.Tokens[i.Value].Link == null)
                {
                    gapStart = i;
                }

                // Find gap end
                else if (gapStart != null && text.Tokens[i.Value].Link != null)
                {
                    var gapFront = gapStart;
                    var gapBack = text.Tokens[i.Value].Prev;

                    // Slide down as deep as possible
                    var front = gapFront;
                    var back = text.Tokens[gapBack.Value].Next;
                    if (
                        front != null &&
                        back != null &&
                        text.Tokens[front.Value].Link == null &&
                        text.Tokens[back.Value].Link != null &&
                        text.Tokens[front.Value].TokenString == text.Tokens[back.Value].TokenString
                    )
                    {
                        text.Tokens[front.Value].Link = text.Tokens[back.Value].Link;
                        textLinked.Tokens[text.Tokens[front.Value].Link.Value].Link = front;
                        text.Tokens[back.Value].Link = null;

                        gapFront = text.Tokens[gapFront.Value].Next;
                        gapBack = text.Tokens[gapBack.Value].Next;

                        front = text.Tokens[front.Value].Next;
                        back = text.Tokens[back.Value].Next;
                    }

                    // Test slide up, remember last line break or word border
                    front = text.Tokens[gapFront.Value].Prev;
                    back = gapBack;
                    var gapFrontBlankTest = regExpSlideBorder.IsMatch(text.Tokens[gapFront.Value].TokenString);
                    var frontStop = front;
                    if (text.Tokens[back.Value].Link == null)
                    {
                        while (
                            front != null &&
                            back != null &&
                            text.Tokens[front.Value].Link != null &&
                            text.Tokens[front.Value].TokenString == text.Tokens[back.Value].TokenString
                        )
                        {
                            if (front != null)
                            {

                                // Stop at line break
                                if (regExpSlideStop.IsMatch(text.Tokens[front.Value].TokenString) == true)
                                {
                                    frontStop = front;
                                    break;
                                }

                                // Stop at first word border (blank/word or word/blank)
                                if (
                                    regExpSlideBorder.IsMatch(text.Tokens[front.Value].TokenString) != gapFrontBlankTest)
                                {
                                    frontStop = front;
                                }
                            }
                            front = text.Tokens[front.Value].Prev;
                            back = text.Tokens[back.Value].Prev;
                        }
                    }

                    // Actually slide up to stop
                    front = text.Tokens[gapFront.Value].Prev;
                    back = gapBack;
                    while (
                        front != null &&
                        back != null &&
                        front != frontStop &&
                        text.Tokens[front.Value].Link != null &&
                        text.Tokens[back.Value].Link == null &&
                        text.Tokens[front.Value].TokenString == text.Tokens[back.Value].TokenString
                    )
                    {
                        text.Tokens[back.Value].Link = text.Tokens[front.Value].Link;
                        textLinked.Tokens[text.Tokens[back.Value].Link.Value].Link = back;
                        text.Tokens[front.Value].Link = null;

                        front = text.Tokens[front.Value].Prev;
                        back = text.Tokens[back.Value].Prev;
                    }
                    gapStart = null;
                }
                i = text.Tokens[i.Value].Next;
            }
        }

        /// <summary>
        /// Convert matching '=' blocks in groups into insertion/deletion ('+'/'-') pairs
        /// if too short and too common.
        /// Prevents fragmented diffs for very different versions.
        /// </summary>
        /// <returns>True if text tokens were unlinked.</returns>
        private bool UnlinkBlocks()
        {
            // Cycle through groups
            var unlinked = false;
            var groupsLength = Groups.Count;
            for (var group = 0; group < groupsLength; group++)
            {
                var blockStart = Groups[group].BlockStart;
                var blockEnd = Groups[group].BlockEnd;

                // Unlink whole group if no block is at least blockMinLength words long and unique
                if (Groups[group].MaxWords < Configuration.BlockMinLength && Groups[group].Unique == false)
                {
                    for (var block = blockStart; block <= blockEnd; block++)
                    {
                        if (Blocks[block].Type == "=")
                        {
                            UnlinkSingleBlock(Blocks[block]);
                            unlinked = true;
                        }
                    }
                }

                // Otherwise unlink block flanks
                else
                {

                    // Unlink blocks from start
                    for (var block = blockStart; block <= blockEnd; block++)
                    {
                        if (Blocks[block].Type == "=")
                        {

                            // Stop unlinking if more than one word or a unique word
                            if (Blocks[block].Words > 1 || Blocks[block].Unique == true)
                            {
                                break;
                            }
                            UnlinkSingleBlock(Blocks[block]);
                            unlinked = true;
                            blockStart = block;
                        }
                    }

                    // Unlink blocks from end
                    for (var block = blockEnd; block > blockStart; block--)
                    {
                        if (Blocks[block].Type == "=")
                        {

                            // Stop unlinking if more than one word or a unique word
                            if (
                                Blocks[block].Words > 1 ||
                                (Blocks[block].Words == 1 && Blocks[block].Unique == true)
                            )
                            {
                                break;
                            }
                            UnlinkSingleBlock(Blocks[block]);
                            unlinked = true;
                        }
                    }
                }
            }
            return unlinked;
        }

        /// <summary>
        /// Unlink text tokens of single block, convert them into into insertion/deletion ('+'/'-') pairs.
        /// </summary>
        /// <param name="block">Blocks table object</param>
        private void UnlinkSingleBlock(Block block)
        {
            // Cycle through old text
            var j = block.OldStart;
            for (var count = 0; count < block.Count; count++)
            {
                // Unlink tokens
                _newText.Tokens[_oldText.Tokens[j.Value].Link.Value].Link = null;
                _oldText.Tokens[j.Value].Link = null;
                j = _oldText.Tokens[j.Value].Next;
            }
        }

        /// <summary>
        /// Collect identical corresponding matching ('=') blocks from old text and sort by new text.
        /// </summary>
        public void GetSameBlocks()
        {
            // Clear blocks array
            Blocks.Clear();

            // Cycle through old text to find connected (linked, matched) blocks
            int? j = _oldText.First;
            int? i = null;
            while (j != null)
            {
                // Skip '-' blocks
                while (j != null && _oldText.Tokens[j.Value].Link == null)
                {
                    j = _oldText.Tokens[j.Value].Next;
                }

                // Get '=' block
                if (j != null)
                {
                    i = _oldText.Tokens[j.Value].Link;
                    var iStart = i;
                    var jStart = j;

                    // Detect matching blocks ('=')
                    var count = 0;
                    var unique = false;
                    var text = "";
                    while (i != null && j != null && _oldText.Tokens[j.Value].Link == i)
                    {
                        text += _oldText.Tokens[j.Value].TokenString;
                        count++;
                        if (_newText.Tokens[i.Value].Unique == true)
                        {
                            unique = true;
                        }
                        i = _newText.Tokens[i.Value].Next;
                        j = _oldText.Tokens[j.Value].Next;
                    }

                    // Save old text '=' block
                    Blocks.Add(new Block
                    {
                        OldBlock = Blocks.Count,
                        NewBlock = null,
                        OldNumber = _oldText.Tokens[jStart.Value].Number,
                        NewNumber = _newText.Tokens[iStart.Value].Number,
                        OldStart = jStart,
                        Count = count,
                        Unique = unique,
                        Words = WordCount(text),
                        Chars = text.Length,
                        Type = "=",
                        Section = null,
                        Group = null,
                        Fixed = null,
                        //Moved = null,
                        Text = text
                    });
                }
            }

            // Sort blocks by new text token number
            Blocks = Blocks.OrderBy(b => b.NewNumber).ToList(); // TODO: validate sort order (line 2241)

            // Number blocks in new text order
            var blocksLength = Blocks.Count;
            for (var block = 0; block < blocksLength; block++)
            {
                Blocks[block].NewBlock = block;
            }
        }

        /// <summary>
        /// Collect independent block sections with no block move crosses
        /// outside a section for per-section determination of non-moving fixed groups.
        /// </summary>
        private void GetSections()
        {
            Sections.Clear();

            // Cycle through blocks
            var blocksLength = Blocks.Count;
            for (var block = 0; block < blocksLength; block++)
            {
                var sectionStart = block;
                var sectionEnd = block;

                var oldMax = Blocks[sectionStart].OldNumber;
                var sectionOldMax = oldMax;

                // Check right
                for (var j = sectionStart + 1; j < blocksLength; j++)
                {

                    // Check for crossing over to the left
                    if (Blocks[j].OldNumber > oldMax)
                    {
                        oldMax = Blocks[j].OldNumber;
                    }
                    else if (Blocks[j].OldNumber < sectionOldMax)
                    {
                        sectionEnd = j;
                        sectionOldMax = oldMax;
                    }
                }

                // Save crossing sections
                if (sectionEnd > sectionStart)
                {

                    // Save section to block
                    for (var i = sectionStart; i <= sectionEnd; i++)
                    {
                        Blocks[i].Section = Sections.Count;
                    }

                    // Save section
                    Sections.Add(new Section
                    {
                        BlockStart = sectionStart,
                        BlockEnd = sectionEnd
                    });

                    block = sectionEnd;
                }
            }
        }

        /// <summary>
        /// Find groups of continuous old text blocks.
        /// </summary>
        private void GetGroups()
        {
            // Clear groups
            Groups.Clear();

            // Cycle through blocks
            var blocksLength = Blocks.Count;
            for (var block = 0; block < blocksLength; block++)
            {
                var groupStart = block;
                var groupEnd = block;
                var oldBlock = Blocks[groupStart].OldBlock;

                // Get word and char count of block
                int? words = WordCount(Blocks[block].Text);
                int? maxWords = words;
                var unique = Blocks[block].Unique;
                var chars = Blocks[block].Chars;

                // Check right
                for (var i = groupEnd + 1; i < blocksLength; i++)
                {
                    // Check for crossing over to the left
                    if (Blocks[i].OldBlock != oldBlock + 1)
                    {
                        break;
                    }
                    oldBlock = Blocks[i].OldBlock;

                    // Get word and char count of block
                    if (Blocks[i].Words > maxWords)
                    {
                        maxWords = Blocks[i].Words;
                    }
                    if (Blocks[i].Unique == true)
                    {
                        unique = true;
                    }
                    words += Blocks[i].Words;
                    chars += Blocks[i].Chars;
                    groupEnd = i;
                }

                // Save crossing group
                if (groupEnd >= groupStart)
                {
                    // Set groups outside sections as fixed
                    var isFixed = false;
                    if (Blocks[groupStart].Section == null)
                    {
                        isFixed = true;
                    }

                    // Save group to block
                    for (var i = groupStart; i <= groupEnd; i++)
                    {
                        Blocks[i].Group = Groups.Count;
                        Blocks[i].Fixed = isFixed;
                    }

                    Groups.Add(new Model.Group
                    {
                        OldNumber = Blocks[groupStart].OldNumber,
                        BlockStart = groupStart,
                        BlockEnd = groupEnd,
                        Unique = unique,
                        MaxWords = maxWords.Value,
                        Words = words,
                        Chars = chars,
                        Fixed = isFixed,
                        MovedFrom = null,
                        Color = null
                    });

                    block = groupEnd;

                    // Set global word count of longest linked block
                    if (maxWords > MaxWords)
                    {
                        MaxWords = maxWords.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Set longest sequence of increasing groups in sections as fixed (not moved).
        /// </summary>
        private void SetFixed()
        {
            // Cycle through sections
            var sectionsLength = Sections.Count;
            for (var section = 0; section < sectionsLength; section++)
            {
                var blockStart = Sections[section].BlockStart;
                var blockEnd = Sections[section].BlockEnd;

                var groupStart = Blocks[blockStart].Group;
                var groupEnd = Blocks[blockEnd].Group;

                // Recusively find path of groups in increasing old group order with longest char length
                var cache = new List<Path>();
                var maxChars = 0;
                List<int> maxPath = null;

                // Start at each group of section
                for (var i = groupStart; i <= groupEnd; i++)
                {
                    var pathObj = FindMaxPath(i.Value, groupEnd.Value, cache);
                    if (pathObj.Chars > maxChars)
                    {
                        maxPath = pathObj.Paths;
                        maxChars = pathObj.Chars;
                    }
                }

                // Mark fixed groups
                var maxPathLength = maxPath.Count;
                for (var i = 0; i < maxPathLength; i++)
                {
                    var group = maxPath[i];
                    Groups[group].Fixed = true;

                    // Mark fixed blocks
                    for (var block = Groups[group].BlockStart; block <= Groups[group].BlockEnd; block++)
                    {
                        Blocks[block].Fixed = true;
                    }
                }
            }
        }

        /// <summary>
        /// Collect deletion ('-') blocks from old text.
        /// </summary>
        private void GetDelBlocks()
        {
            // Cycle through old text to find connected (linked, matched) blocks
            var j = _oldText.First;
            int? i = null;
            while (j != null)
            {
                // Collect '-' blocks
                var oldStart = j;
                var count = 0;
                var text = "";
                while (j != null && _oldText.Tokens[j.Value].Link == null)
                {
                    count++;
                    text += _oldText.Tokens[j.Value].TokenString;
                    j = _oldText.Tokens[j.Value].Next;
                }

                // Save old text '-' block
                if (count != 0)
                {
                    Blocks.Add(new Block
                    {
                        OldBlock = null,
					    NewBlock = null,
					    OldNumber = _oldText.Tokens[oldStart.Value].Number,
					    NewNumber = null,
					    OldStart = oldStart,
					    Count = count,
					    Unique = false,
					    Words = null,
					    Chars = text.Length,
					    Type = "-",
					    Section = null,
					    Group = null,
					    Fixed = null,
					    //Moved = null,
					    Text = text
    
                    });
                }

                // Skip '=' blocks
                if (j != null)
                {
                    i = _oldText.Tokens[j.Value].Link;
                    while (i != null && j != null && _oldText.Tokens[j.Value].Link == i)
                    {
                        i = _newText.Tokens[i.Value].Next;
                        j = _oldText.Tokens[j.Value].Next;
                    }
                }
            }
        }

        /// <summary>
        /// Position deletion '-' blocks into new text order.
        /// Deletion blocks move with fixed reference:
        ///    Old:          1 D 2      1 D 2
        ///                 /     \    /   \ \
        ///    New:        1 D     2  1     D 2
        ///    Fixed:      *                  *
        ///    newNumber:  1 1              2 2
        /// 
        ///  Marks '|' and deletions '-' get newNumber of reference block
        ///  and are sorted around it by old text number.
        /// </summary>
        private void PositionDelBlocks()
        {
            // Sort shallow copy of blocks by oldNumber
            var blocksOld = Blocks; // TODO: copy list instead of modifying original
            blocksOld.OrderBy(b => b.OldNumber).ToList(); // TODO: validate sort order (line 2715)

            // Cycle through blocks in old text order
            var blocksOldLength = blocksOld.Count;
            for (var block = 0; block < blocksOldLength; block++)
            {
                var delBlock = blocksOld[block];

                // '-' block only
                if (delBlock.Type != "-")
                {
                    continue;
                }

                // Find fixed '=' reference block from original block position to position '-' block
                // Similar to position marks '|' code

                // Get old text prev block
                int? prevBlockNumber = null;
                Block prevBlock = null;
                if (block > 0)
                {
                    prevBlockNumber = blocksOld[block - 1].NewBlock;
                    prevBlock = Blocks[prevBlockNumber.Value];
                }

                // Get old text next block
                int? nextBlockNumber = null;
                Block nextBlock = null;
                if (block < blocksOld.Count - 1)
                {
                    nextBlockNumber = blocksOld[block + 1].NewBlock;
                    nextBlock = Blocks[nextBlockNumber.Value];
                }

                // Move after prev block if fixed
                Block refBlock = null;
                if (prevBlock != null && prevBlock.Type == "=" && prevBlock.Fixed == true)
                {
                    refBlock = prevBlock;
                }

                // Move before next block if fixed
                else if (nextBlock != null && nextBlock.Type == "=" && nextBlock.Fixed == true)
                {
                    refBlock = nextBlock;
                }

                // Move after prev block if not start of group
                else if (
                    prevBlock != null &&
                    prevBlock.Type == "=" &&
                    prevBlockNumber != Groups[prevBlock.Group.Value].BlockEnd)
                {
                    refBlock = prevBlock;
                }

                // Move before next block if not start of group
                else if (
                    nextBlock != null &&
                    nextBlock.Type == "=" &&
                    nextBlockNumber != Groups[nextBlock.Group.Value].BlockStart)
                {
                    refBlock = nextBlock;
                }

                // Move after closest previous fixed block
                else
                {
                    for (var fixedBlock = block; fixedBlock >= 0; fixedBlock--)
                    {
                        if (blocksOld[fixedBlock].Type == "=" && blocksOld[fixedBlock].Fixed == true)
                        {
                            refBlock = blocksOld[fixedBlock];
                            break;
                        }
                    }
                }

                // Move before first block
                if (refBlock == null)
                {
                    delBlock.NewNumber = -1;
                }

                // Update '-' block data
                else
                {
                    delBlock.NewNumber = refBlock.NewNumber;
                    delBlock.Section = refBlock.Section;
                    delBlock.Group = refBlock.Group;
                    delBlock.Fixed = refBlock.Fixed;
                }
            }

		    // Sort '-' blocks in and update groups
		    SortBlocks();
        }

        /// <summary>
        /// Collect insertion ('+') blocks from new text.
        /// </summary>
        private void GetInsBlocks()
        {
            // Cycle through new text to find insertion blocks
            var i = _newText.First;
            while (i != null)
            {
                // Jump over linked (matched) block
                while (i != null && _newText.Tokens[i.Value].Link != null)
                {
                    i = _newText.Tokens[i.Value].Next;
                }

                // Detect insertion blocks ('+')
                if (i != null)
                {
                    var iStart = i;
                    var count = 0;
                    var text = "";
                    while (i != null && _newText.Tokens[i.Value].Link == null)
                    {
                        count++;
                        text += _newText.Tokens[i.Value].TokenString;
                        i = _newText.Tokens[i.Value].Next;
                    }

                    // Save new text '+' block
                    Blocks.Add(new Block
                    {
                        OldBlock = null,
					    NewBlock = null,
					    OldNumber = null,
					    NewNumber = _newText.Tokens[iStart.Value].Number,
					    OldStart = null,
					    Count = count,
					    Unique = false,
					    Words = null,
					    Chars = text.Length,
					    Type = "+",
					    Section = null,
					    Group = null,
					    Fixed = null,
					    //Moved = null,
					    Text = text
                    });
                }
            }

            // Sort '+' blocks in and update groups
            SortBlocks();
        }

        /// <summary>
        /// Set group numbers of insertion '+' blocks.
        /// </summary>
        private void SetInsGroups()
        {
            // Set group numbers of '+' blocks inside existing groups
            var groupsLength = Groups.Count;
            for (var group = 0; group < groupsLength; group++)
            {
                var isFixed = Groups[group].Fixed;
                for (var block = Groups[group].BlockStart; block <= Groups[group].BlockEnd; block++)
                {
                    if (Blocks[block].Group == null)
                    {
                        Blocks[block].Group = group;
                        Blocks[block].Fixed = isFixed;
                    }
                }
            }

            // Add remaining '+' blocks to new groups

            // Cycle through blocks
            var blocksLength = Blocks.Count;
            for (var block = 0; block < blocksLength; block++)
            {

                // Skip existing groups
                if (Blocks[block].Group == null)
                {
                    Blocks[block].Group = Groups.Count;

                    // Save new single-block group
                    Groups.Add(new Model.Group
                    {
                        OldNumber = Blocks[block].OldNumber,
                        BlockStart = block,
                        BlockEnd = block,
                        Unique = Blocks[block].Unique,
                        MaxWords = Blocks[block].Words.Value,
                        Words = Blocks[block].Words,
                        Chars = Blocks[block].Chars,
                        Fixed = Blocks[block].Fixed,
                        MovedFrom = null,
                        Color = null
                    });
                }
            }
        }

        /// <summary>
        /// Mark original positions of moved groups.
        /// Scheme: moved block marks at original positions relative to fixed groups:
        ///   Groups:    3       7
        ///           1 <|       |     (no next smaller fixed)
        ///           5  |<      |
        ///              |>  5   |
        ///              |   5  <|
        ///              |      >|   5
        ///              |       |>  9 (no next larger fixed)
        ///   Fixed:     *       *
        ///
        /// Mark direction: groups.movedGroup.blockStart < groups.group.blockStart
        /// Group side:     groups.movedGroup.oldNumber < groups.group.oldNumber
        ///
        /// Marks '|' and deletions '-' get newNumber of reference block
        /// and are sorted around it by old text number.
        /// </summary>
        private void InsertMarks()
        {
            var color = 1;

            // Make shallow copy of blocks
            var blocksOld = Blocks.ToList(); // TODO: make copy instead of modifying the original list

            // Enumerate copy
            var blocksOldLength = blocksOld.Count;
            for (var i = 0; i < blocksOldLength; i++)
            {
                blocksOld[i].Number = i;
            }

            // Sort copy by oldNumber
            blocksOld.OrderBy(b => b.NewNumber).ThenBy(b => b.OldNumber); // TODO: Validate sort (line 3015)

            // Create lookup table: original to sorted
            var lookupSorted = new List<int>();
            for (var i = 0; i < blocksOldLength; i++)
            {
                lookupSorted[blocksOld[i].Number] = i;
            }

            // Cycle through groups (moved group)
            var groupsLength = Groups.Count;
            for (var moved = 0; moved < groupsLength; moved++)
            {
                var movedGroup = Groups[moved];
                if (movedGroup.Fixed != false)
                {
                    continue;
                }
                var movedOldNumber = movedGroup.OldNumber;

                // Find fixed '=' reference block from original block position to position '|' block
                // Similar to position deletions '-' code

                // Get old text prev block
                Block prevBlock = null;
                var block = lookupSorted[movedGroup.BlockStart];
                if (block > 0)
                {
                    prevBlock = blocksOld[block - 1];
                }

                // Get old text next block
                Block nextBlock = null;
                block = lookupSorted[movedGroup.BlockEnd];
                if (block < blocksOld.Count - 1)
                {
                    nextBlock = blocksOld[block + 1];
                }

                // Move after prev block if fixed
                Block refBlock = null;
                if (prevBlock != null && prevBlock.Type == "=" && prevBlock.Fixed == true)
                {
                    refBlock = prevBlock;
                }

                // Move before next block if fixed
                else if (nextBlock != null && nextBlock.Type == "=" && nextBlock.Fixed == true)
                {
                    refBlock = nextBlock;
                }

                // Find closest fixed block to the left
                else
                {
                    for (var iFixed = lookupSorted[movedGroup.BlockStart] - 1; iFixed >= 0; iFixed--)
                    {
                        if (blocksOld[iFixed].Type == "=" && blocksOld[iFixed].Fixed == true)
                        {
                            refBlock = blocksOld[iFixed];
                            break;
                        }
                    }
                }

                // Get position of new mark block
                int? newNumber;
                int? markGroup;

                // No smaller fixed block, moved right from before first block
                if (refBlock == null)
                {
                    newNumber = -1;
                    markGroup = Groups.Count;

                    // Save new single-mark-block group
                    Groups.Add(new Model.Group
                    {
                        OldNumber = 0,
                        BlockStart = Blocks.Count,
                        BlockEnd = Blocks.Count,
                        Unique = false,
                        MaxWords = null,
                        Words = null,
                        Chars = 0,
                        Fixed = null,
                        MovedFrom = null,
                        Color = null
                    });
                }
                else
                {
                    newNumber = refBlock.NewNumber;
                    markGroup = refBlock.Group;
                }

                // Insert '|' block
                Blocks.Add(new Block
                {
                    OldBlock =  null,
                    NewBlock =  null,
                    OldNumber = movedOldNumber,
                    NewNumber = newNumber,
                    OldStart =  null,
                    Count = null,
                    Unique = null,
                    Words = null,
                    Chars = 0,
                    Type = "|",
                    Section = null,
                    Group = markGroup,
                    Fixed = true,
                    Moved = moved,
                    Text = ""
                });

                // Set group color
                movedGroup.Color = color;
                movedGroup.MovedFrom = markGroup;
                color++;
            }
        }

        /// <summary>
        /// Sort blocks by new text token number and update groups.
        /// </summary>
        private void SortBlocks()
        {
            // Sort by newNumber, then by old number
            Blocks.OrderBy(b => b.NewNumber).ThenBy(b => b.OldNumber); // TODO: Validate sort (line 2887)

            // Cycle through blocks and update groups with new block numbers
            int? group = null;
            var blocksLength = Blocks.Count;
            for (var block = 0; block < blocksLength; block++)
            {
                var blockGroup = Blocks[block].Group;
                if (blockGroup != null)
                {
                    if (blockGroup != group)
                    {
                        group = Blocks[block].Group;
                        Groups[group.Value].BlockStart = block;
                        Groups[group.Value].OldNumber = Blocks[block].OldNumber;
                    }
                    Groups[blockGroup.Value].BlockEnd = block;
                }
            }
        }

        /// <summary>
        /// Recusively find path of groups in increasing old group order with longest char length.
        /// </summary>
        /// <param name="start">Path start group.</param>
        /// <param name="groupEnd">Path last group.</param>
        /// <param name="cache">Cache object, contains returnObj for start.</param>
        /// <returns></returns>
        private Path FindMaxPath(int start, int groupEnd, List<Path> cache)
        {
            // Find longest sub-path
            var maxChars = 0;
            var oldNumber = Groups[start].OldNumber;
            var returnObj = new Path();
		    for (var i = start + 1; i <= groupEnd; i ++)
            {
			    // Only in increasing old group order
			    if (Groups[i].OldNumber < oldNumber )
                {
				    continue;
			    }

                // Get longest sub-path from cache (deep copy)
                Path pathObj;
			    if (cache[i] != null)
                {
				    pathObj = new Path
                    {
                        Paths = cache[i].Paths,
                        Chars = cache[i].Chars
                    };
			    }

			    // Get longest sub-path by recursion
			    else
                {
				    pathObj = FindMaxPath(i, groupEnd, cache);
                }

			    // Select longest sub-path
			    if (pathObj.Chars > maxChars)
                {
				    maxChars = pathObj.Chars;
				    returnObj = pathObj;
			    }
		    }

		    // Add current start to path
		    returnObj.Paths.Insert(0, start);
		    returnObj.Chars += Groups[start].Chars;

		    // Save path to cache (deep copy)
		    if (cache.Count < start)
            {
			    cache.Add(new Path
                {
                    Paths = returnObj.Paths,
                    Chars = returnObj.Chars
                });
		    }

		    return returnObj;
        }

        /// <summary>
        /// Count real words in text.
        /// </summary>
        /// <param name="text">Text for word counting.</param>
        /// <returns>Number of words in text.</returns>
        private int WordCount(string text)
        {
            return WikEdDiffConfiguration.RegularExpressions.CountWords.Matches(text).Count;
        }

        /// <summary>
        /// Collect diff fragment list for markup, create abstraction layer for customized diffs.
        /// Adds the following fagment types:
        ///   '=', '-', '+'   same, deletion, insertion
        ///   '<', '>'        mark left, mark right
        ///   '(<', '(>', ')' block start and end
        ///   '[', ']'        fragment start and end
        ///   '{', '}'        container start and end
        /// </summary>
        private void GetDiffFragments()
        {
            // Make shallow copy of groups and sort by blockStart
            var groupsSort = Groups; // TODO: make copy instead of modifying original list
            groupsSort.OrderBy(g => g.BlockStart); // TODO: Validate order (Line 3160)

            // Cycle through groups
            var groupsSortLength = groupsSort.Count;
            for (var group = 0; group < groupsSortLength; group++)
            {
                var blockStart = groupsSort[group].BlockStart;
                var blockEnd = groupsSort[group].BlockEnd;

                // Add moved block start
                var color = groupsSort[group].Color;
                if (color != null)
                {
                    string type;
                    if (groupsSort[group].MovedFrom < Blocks[blockStart].Group)
                    {
                        type = "(<";
                    }
                    else
                    {
                        type = "(>";
                    }
                    Fragments.Add(new Fragment
                    {
                        Text = "",
					    Type = type,
					    Color = color
                    });
                }

                // Cycle through blocks
                for (var block = blockStart; block <= blockEnd; block++)
                {
                    var type = Blocks[block].Type;

                    // Add '=' unchanged text and moved block
                    if (type == "=" || type == "-" || type == "+")
                    {
                        Fragments.Add(new Fragment
                        {
                            Text = Blocks[block].Text,
                            Type = type,
                            Color = color
                        });
                    }

				    // Add '<' and '>' marks
				    else if (type == "|")
                    {
					    var movedGroup = Groups[Blocks[block].Moved];

                        // Get mark text
                        var markText = "";
					    for (var movedBlock = movedGroup.BlockStart; movedBlock <= movedGroup.BlockEnd; movedBlock ++)
                        {
						    if (Blocks[movedBlock].Type == "=" || Blocks[movedBlock].Type == "-" )
                            {
							    markText += Blocks[movedBlock].Text;
						    }
                        }

                        // Get mark direction
                        string markType;
					    if (movedGroup.BlockStart < blockStart )
                        {
						    markType = "<";
					    }
					    else {
						    markType = ">";
					    }

					    // Add mark
					    Fragments.Add(new Fragment
                        {
						    Text =  markText,
						    Type =  markType,
						    Color = movedGroup.Color
					    });
				    }
                }

                // Add moved block end.
                if (color != null)
                {
                    Fragments.Add(new Fragment
                    {
                        Text = "",
                        Type = " )",
                        Color = color
                    });
                }
            }

            // Cycle through fragments, join consecutive fragments of same type (i.e. '-' blocks)
            var fragmentsLength = Fragments.Count;
            for (var fragment = 1; fragment < fragmentsLength; fragment++)
            {
                // Check if joinable
                if (
                    Fragments[fragment].Type == Fragments[fragment - 1].Type &&
                    Fragments[fragment].Color == Fragments[fragment - 1].Color &&
                    Fragments[fragment].Text != "" && Fragments[fragment - 1].Text != ""
                )
                {

                    // Join and splice
                    Fragments[fragment - 1].Text += Fragments[fragment].Text;
                    Fragments.RemoveRange(fragment, 1);
                    fragment--;
                }
            }

            // Enclose in containers
            Fragments.Insert(0, new Fragment { Text = "", Type = "[" });
            Fragments.Insert(0, new Fragment { Text = "", Type = "{" });
            Fragments.Add(new Fragment { Text = "", Type = "]" });
            Fragments.Add(new Fragment { Text = "", Type = "}" });
        }


        /// <summary>
        /// Clip unchanged sections from unmoved block text.
        /// Adds the following fagment types:
        ///   '~', ' ~', '~ ' omission indicators
        ///   '[', ']', ','   fragment start and end, fragment separator
        /// </summary>
        private void ClipDiffFragments()
        {
            // Skip if only one fragment in containers, no change
            if (Fragments.Count == 5)
            {
                return;
            }

            // Min length for clipping right
            var minRight = Configuration.ClipHeadingRight;
            if (Configuration.ClipParagraphRightMin < minRight)
            {
                minRight = Configuration.ClipParagraphRightMin;
            }
            if (Configuration.ClipLineRightMin < minRight)
            {
                minRight = Configuration.ClipLineRightMin;
            }
            if (Configuration.ClipBlankRightMin < minRight)
            {
                minRight = Configuration.ClipBlankRightMin;
            }
            if (Configuration.ClipCharsRight < minRight)
            {
                minRight = Configuration.ClipCharsRight;
            }

            // Min length for clipping left
            var minLeft = Configuration.ClipHeadingLeft;
            if (Configuration.ClipParagraphLeftMin < minLeft)
            {
                minLeft = Configuration.ClipParagraphLeftMin;
            }
            if (Configuration.ClipLineLeftMin < minLeft)
            {
                minLeft = Configuration.ClipLineLeftMin;
            }
            if (Configuration.ClipBlankLeftMin < minLeft)
            {
                minLeft = Configuration.ClipBlankLeftMin;
            }
            if (Configuration.ClipCharsLeft < minLeft)
            {
                minLeft = Configuration.ClipCharsLeft;
            }

            // Cycle through fragments
            var fragmentsLength = Fragments.Count;
            for (var fragment = 0; fragment < fragmentsLength; fragment++)
            {
                // Skip if not an unmoved and unchanged block
                var type = Fragments[fragment].Type;
                var color = Fragments[fragment].Color;
                if (type != "=" || color != null)
                {
                    continue;
                }

                // Skip if too short for clipping
                var text = Fragments[fragment].Text;
                var textLength = text.Length;
                if (textLength < minRight && textLength < minLeft)
                {
                    continue;
                }

                // Get line positions including start and end
                var lines = new List<int>();
                var lastIndex = 0;
                var regExpMatches = WikEdDiffConfiguration.RegularExpressions.ClipLine.Matches(text);
                for (int i = 0; i < regExpMatches.Count; i++)
                {
                    var regExpMatch = regExpMatches[i];
                    lines.Add(regExpMatch.Index);
                    lastIndex += regExpMatch.Value.Length;
                }
                if (lines[0] != 0)
                {
                    lines.Insert(0, 0);
                }
                if (lastIndex != textLength)
                {
                    lines.Add(textLength);
                }

                // Get heading positions
                var headings = new List<int>();
                var headingsEnd = new List<int>();
                regExpMatches = WikEdDiffConfiguration.RegularExpressions.ClipHeading.Matches(text);
                for (int i = 0; i < regExpMatches.Count; i++)
                {
                    var regExpMatch = regExpMatches[i];
                    headings.Add(regExpMatch.Index);
                    headingsEnd.Add(regExpMatch.Index + regExpMatch.Length);
                }

                // Get paragraph positions including start and end
                var paragraphs = new List<int>();
                lastIndex = 0;
                regExpMatches = WikEdDiffConfiguration.RegularExpressions.ClipParagraph.Matches(text);
                for (int i = 0; i < regExpMatches.Count; i++)
                {
                    var regExpMatch = regExpMatches[i];
                    paragraphs.Add(regExpMatch.Index);
                    lastIndex += regExpMatch.Value.Length;
                }
                if (paragraphs[0] != 0)
                {
                    paragraphs.Insert(0, 0);
                }
                if (lastIndex != textLength)
                {
                    paragraphs.Add(textLength);
                }

                // Determine ranges to keep on left and right side
                int? rangeRight = null;
                int? rangeLeft = null;
                var rangeRightType = "";
                var rangeLeftType = "";

                // Find clip pos from left, skip for first non-container block
                if (fragment != 2)
                {
                    // Maximum lines to search from left
                    var rangeLeftMax = textLength;
                    if (Configuration.ClipLinesLeftMax < lines.Count)
                    {
                        rangeLeftMax = lines[Configuration.ClipLinesLeftMax];
                    }

                    // Find first heading from left
                    if (rangeLeft == null)
                    {
                        var headingsLength = headingsEnd.Count;
                        for (var j = 0; j < headingsLength; j++)
                        {
                            if (headingsEnd[j] > Configuration.ClipHeadingLeft || headingsEnd[j] > rangeLeftMax)
                            {
                                break;
                            }
                            rangeLeft = headingsEnd[j];
                            rangeLeftType = "heading";
                            break;
                        }
                    }

                    // Find first paragraph from left
                    if (rangeLeft == null)
                    {
                        var paragraphsLength = paragraphs.Count;
                        for (var j = 0; j < paragraphsLength; j++)
                        {
                            if (
                                paragraphs[j] > Configuration.ClipParagraphLeftMax ||
                                paragraphs[j] > rangeLeftMax
                            )
                            {
                                break;
                            }
                            if (paragraphs[j] > Configuration.ClipParagraphLeftMin)
                            {
                                rangeLeft = paragraphs[j];
                                rangeLeftType = "paragraph";
                                break;
                            }
                        }
                    }

                    // Find first line break from left
                    if (rangeLeft == null)
                    {
                        var linesLength = lines.Count;
                        for (var j = 0; j < linesLength; j++)
                        {
                            if (lines[j] > Configuration.ClipLineLeftMax || lines[j] > rangeLeftMax)
                            {
                                break;
                            }
                            if (lines[j] > Configuration.ClipLineLeftMin)
                            {
                                rangeLeft = lines[j];
                                rangeLeftType = "line";
                                break;
                            }
                        }
                    }

                    // Find first blank from left
                    if (rangeLeft == null)
                    {
                        //WikEdDiffConfiguration.RegularExpressions.ClipBlank.lastIndex = Configuration.ClipBlankLeftMin; // TODO: line 3436
                        //if ((regExpMatch = Configuration.regExp.clipBlank.exec(text)) != null)
                        //{
                        //    if (
                        //        regExpMatch.index < Configuration.ClipBlankLeftMax &&
                        //        regExpMatch.index < rangeLeftMax
                        //    )
                        //    {
                        //        rangeLeft = regExpMatch.index;
                        //        rangeLeftType = "blank";
                        //    }
                        //}
                    }

                    // Fixed number of chars from left
                    if (rangeLeft == null)
                    {
                        if (Configuration.ClipCharsLeft < rangeLeftMax)
                        {
                            rangeLeft = Configuration.ClipCharsLeft;
                            rangeLeftType = "chars";
                        }
                    }

                    // Fixed number of lines from left
                    if (rangeLeft == null)
                    {
                        rangeLeft = rangeLeftMax;
                        rangeLeftType = "fixed";
                    }
                }

                // Find clip pos from right, skip for last non-container block
                if (fragment != Fragments.Count - 3)
                {
                    // Maximum lines to search from right
                    var rangeRightMin = 0;
                    if (lines.Count >= Configuration.ClipLinesRightMax)
                    {
                        rangeRightMin = lines[lines.Count - Configuration.ClipLinesRightMax];
                    }

                    // Find last heading from right
                    if (rangeRight == null)
                    {
                        for (var j = headings.Count - 1; j >= 0; j--)
                        {
                            if (headings[j] < textLength - Configuration.ClipHeadingRight ||
                                headings[j] < rangeRightMin)
                            {
                                break;
                            }
                            rangeRight = headings[j];
                            rangeRightType = "heading";
                            break;
                        }
                    }

                    // Find last paragraph from right
                    if (rangeRight == null)
                    {
                        for (var j = paragraphs.Count - 1; j >= 0; j--)
                        {
                            if (
                                paragraphs[j] < textLength - Configuration.ClipParagraphRightMax ||
                                paragraphs[j] < rangeRightMin
                            )
                            {
                                break;
                            }
                            if (paragraphs[j] < textLength - Configuration.ClipParagraphRightMin)
                            {
                                rangeRight = paragraphs[j];
                                rangeRightType = "paragraph";
                                break;
                            }
                        }
                    }

                    // Find last line break from right
                    if (rangeRight == null)
                    {
                        for (var j = lines.Count - 1; j >= 0; j--)
                        {
                            if (
                                lines[j] < textLength - Configuration.ClipLineRightMax ||
                                lines[j] < rangeRightMin
                            )
                            {
                                break;
                            }
                            if (lines[j] < textLength - Configuration.ClipLineRightMin)
                            {
                                rangeRight = lines[j];
                                rangeRightType = "line";
                                break;
                            }
                        }
                    }

                    // Find last blank from right
                    if (rangeRight == null)
                    {
                        var startPos = textLength - Configuration.ClipBlankRightMax;
                        if (startPos < rangeRightMin)
                        {
                            startPos = rangeRightMin;
                        }
                        //Configuration.regExp.clipBlank.lastIndex = startPos; // TODO (Line 3526)
                        //var lastPos = null;
                        //while ((regExpMatch = Configuration.regExp.clipBlank.exec(text)) != null)
                        //{
                        //    if (regExpMatch.index > textLength - Configuration.ClipBlankRightMin)
                        //    {
                        //        if (lastPos !== null)
                        //        {
                        //            rangeRight = lastPos;
                        //            rangeRightType = "blank";
                        //        }
                        //        break;
                        //    }
                        //    lastPos = regExpMatch.index;
                        //}
                    }

                    // Fixed number of chars from right
                    if (rangeRight == null)
                    {
                        if (textLength - Configuration.ClipCharsRight > rangeRightMin)
                        {
                            rangeRight = textLength - Configuration.ClipCharsRight;
                            rangeRightType = "chars";
                        }
                    }

                    // Fixed number of lines from right
                    if (rangeRight == null)
                    {
                        rangeRight = rangeRightMin;
                        rangeRightType = "fixed";
                    }
                }

                // Check if we skip clipping if ranges are close together
                if (rangeLeft != null && rangeRight != null)
                {
                    // Skip if overlapping ranges
                    if (rangeLeft > rangeRight)
                    {
                        continue;
                    }

                    // Skip if chars too close
                    var skipChars = rangeRight - rangeLeft;
                    if (skipChars < Configuration.ClipSkipChars)
                    {
                        continue;
                    }

                    // Skip if lines too close
                    var skipLines = 0;
                    var linesLength = lines.Count;
                    for (var j = 0; j < linesLength; j++)
                    {
                        if (lines[j] > rangeRight || skipLines > Configuration.ClipSkipLines)
                        {
                            break;
                        }
                        if (lines[j] > rangeLeft)
                        {
                            skipLines++;
                        }
                    }
                    if (skipLines < Configuration.ClipSkipLines)
                    {
                        continue;
                    }
                }

                // Skip if nothing to clip
                if (rangeLeft == null && rangeRight == null)
                {
                    continue;
                }

                // Split left text
                string textLeft = null;
                string omittedLeft = null;
                if (rangeLeft != null)
                {
                    textLeft = text.Substring(0, rangeLeft.Value);

                    // Remove trailing empty lines
                    textLeft = WikEdDiffConfiguration.RegularExpressions.ClipTrimNewLinesLeft.Replace(textLeft, "");

                    // Get omission indicators, remove trailing blanks
                    if (rangeLeftType == "chars")
                    {
                        omittedLeft = "~";
                        textLeft = WikEdDiffConfiguration.RegularExpressions.ClipTrimBlanksLeft.Replace(textLeft, "");
                    }
                    else if (rangeLeftType == "blank")
                    {
                        omittedLeft = " ~";
                        textLeft = WikEdDiffConfiguration.RegularExpressions.ClipTrimBlanksLeft.Replace(textLeft, "");
                    }
                }

                // Split right text
                string textRight = null;
                string omittedRight = null;
                if (rangeRight != null)
                {
                    textRight = text.Substring(rangeRight.Value);

                    // Remove leading empty lines
                    textRight = WikEdDiffConfiguration.RegularExpressions.ClipTrimNewLinesRight.Replace(textRight, "");

                    // Get omission indicators, remove leading blanks
                    if (rangeRightType == "chars")
                    {
                        omittedRight = "~";
                        textRight = WikEdDiffConfiguration.RegularExpressions.ClipTrimBlanksRight.Replace(textRight, "");
                    }
                    else if (rangeRightType == "blank")
                    {
                        omittedRight = "~ ";
                        textRight = WikEdDiffConfiguration.RegularExpressions.ClipTrimBlanksRight.Replace(textRight, "");
                    }
                }

                // Remove split element
                Fragments.RemoveAt(fragment);
                fragmentsLength--;

                // Add left text to fragments list
                if (rangeLeft != null)
                {
                    Fragments.Insert(fragment++, new Fragment { Text = textLeft, Type = "=" });
                    fragmentsLength++;
                    if (omittedLeft != null)
                    {
                        Fragments.Insert(fragment++, new Fragment { Text = "", Type = omittedLeft });
                        fragmentsLength++;
                    }
                }

			    // Add fragment container and separator to list
			    if (rangeLeft != null && rangeRight != null )
                {
                    Fragments.Insert(fragment++, new Fragment { Text = "", Type = "]" });
                    Fragments.Insert(fragment++, new Fragment { Text = "", Type = "," });
                    Fragments.Insert(fragment++, new Fragment { Text = "", Type = "[" });
				    fragmentsLength += 3;
			    }

			    // Add right text to fragments list
			    if (rangeRight != null )
                {
				    if (omittedRight != null )
                    {
                        Fragments.Insert(fragment++, new Fragment { Text = "", Type = omittedRight });
					    fragmentsLength++;
                    }
                    Fragments.Insert(fragment++, new Fragment { Text = textRight, Type = "=" });
				    fragmentsLength++;
			    }
            }
        }
    }

    public static class MatchCollectionExtensions
    {
        /// <summary>
        /// Concatenates two MatchCollections.
        /// </summary>
        public static string[] Concat(this MatchCollection collection1, MatchCollection collection2)
        {
            return collection1
                .OfType<Match>()
                .Select(m => m.Groups[0].Value)
                .Concat(collection2
                    .OfType<Match>()
                    .Select(m => m.Groups[0].Value)
                ).ToArray();
        }
    }
}
