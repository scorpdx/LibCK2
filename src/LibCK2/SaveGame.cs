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

using LibCK2.Parsing;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LibCK2
{
    /// <summary>
    /// A model of a Crusader Kings 2 saved game
    /// </summary>
    public class SaveGame
    {
        internal const string SavePathSuffix = @"Paradox Interactive\Crusader Kings II\save games";
        internal const string CK2Header = "CK2txt";

        public static Encoding SaveGameEncoding { get; } = CodePagesEncodingProvider.Instance.GetEncoding(1252);

        public static string SaveGameLocation
        {
            get
            {
                string MyDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(MyDocuments, SavePathSuffix);
            }
        }

        public JsonDocument GameState { get; }

        private SaveGame(JsonDocument gameState)
        {
            this.GameState = gameState;
        }

        public static async Task<SaveGame> ParseAsync(Stream stream)
        {
            var parsedTokens = CK2Parsing.ParseTokensAsync(stream);
            using (var ms = new MemoryStream(0x1000)) //default buffer size - 4KiB
            {
                using (var writer = new StreamBufferWriter(ms))
                {
                    CK2Parsing.TokensToJson(await parsedTokens, writer);
                }

                ms.Seek(0, SeekOrigin.Begin);
                return new SaveGame(await JsonDocument.ParseAsync(ms));
            }
        }
    }
}
