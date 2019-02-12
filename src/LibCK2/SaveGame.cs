/*
MIT License

Copyright (c) 2019 scorpdx

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LibCK2
{
    /// <summary>
    /// A model of a Crusader Kings 2 saved game
    /// </summary>
    public class SaveGame
    {
        private const string SavePathSuffix = @"Paradox Interactive\Crusader Kings II\save games";
        private const string CK2Header = "CK2txt";

        private static readonly Regex r_nesting = new Regex(@"^(?:[^{}]|(?<Open>{)|(?<Content-Open>}))+?(?(Open)(?!))$", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex r_valuePair = new Regex(@"(?<Identifier>\w+)=(?<Value>[^{}\n]*)", RegexOptions.Compiled);
        private static readonly Regex r_dataList = new Regex(@"^\s*(?:(\S+)\s?)*$", RegexOptions.Compiled | RegexOptions.Multiline);

        public static string SaveGameLocation
        {
            get
            {
                string MyDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(MyDocuments, SavePathSuffix);
            }
        }

        public IReadOnlyDictionary<string, IReadOnlyCollection<object>> GameState { get; }

        public SaveGame(string text)
        {
            GameState = Parse(text);
        }

        private MultiValueDictionary<string, object> Parse(string text)
        {
            if (!text.StartsWith(CK2Header))
            {
                throw new InvalidOperationException($"Not a {CK2Header} file");
            }

            var matches = r_nesting.Matches(text).OfType<Match>();
            var Tree = new MultiValueDictionary<string, object>();
            TopLevelLoop(Tree, matches.Skip(1)); //skip ck2txt header

            return Tree;
        }

        private void TopLevelLoop(MultiValueDictionary<string, object> cur, IEnumerable<Match> matches)
        {
            Lazy<MultiValueDictionary<string, object>> CreateLazyDepth(string searchSpace)
            {
                return new Lazy<MultiValueDictionary<string, object>>(() =>
                {
                    var nested = new MultiValueDictionary<string, object>();
                    TopLevelLoop(nested, r_nesting.Matches(searchSpace).OfType<Match>());

                    return nested;
                });
            }

            string subNestId = null;
            foreach (Match match in matches)
            {
                if (string.IsNullOrWhiteSpace(match.Value)) continue;

                bool isNested = match.Groups["Content"].Success;

                //always try to match a pair
                var vp = r_valuePair.Match(match.Value);
                var id = vp.Groups["Identifier"].Value;
                var value = vp.Groups["Value"].Value;
                if (!vp.Success && !isNested)
                {
                    var dl = r_dataList.Match(match.Value);
                    if (dl.Success)
                    {
                        cur.Add(id, dl.Groups[1].Captures.OfType<Capture>().Select(c => c.Value).ToList());
                    }
                    else throw new InvalidOperationException("Unknown field");
                }
                else
                {
                    bool hasValue = vp.Groups["Value"].Success && !string.IsNullOrEmpty(value);
                    if (isNested)
                    {
                        //split-level
                        if (subNestId != null)
                        {
                            var nc = match.Groups["Content"].Value;
                            cur.Add(subNestId, CreateLazyDepth(nc));
                            subNestId = null;
                        }
                        //same-level
                        else
                        {
                            var nc = match.Groups["Content"].Value;
                            cur.Add(id, CreateLazyDepth(nc));
                        }
                    }
                    else if (vp.Success)
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            cur.Add(id, value);
                        }
                        else
                        {
                            if (subNestId != null) throw new InvalidOperationException("Nested content was not expanded before reaching next field");

                            subNestId = id;
                        }
                    }
                }
            }
        }
    }
}
