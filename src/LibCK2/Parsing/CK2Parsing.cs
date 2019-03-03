//Includes code from: taw/paradox-tools
//Original license reproduced below
//  https://github.com/taw/paradox-tools/blob/master/LICENSE.md @ 2019-03-02
/*
    MIT License

    Copyright (c) 2014-2019 Tomasz Wegrzanowski

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

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static LibCK2.SaveGame;

namespace LibCK2.Parsing
{
    internal static class CK2Parsing
    {
        public enum TokenTypes
        {
            Value,
            Equal,
            Start,
            End
        }

        internal static async Task<List<(string token, TokenTypes stoppedBy)>> ParseTokensAsync(Stream stream)
        {
            var tokens = new List<(string token, TokenTypes stoppedBy)>();

            var scanner = new Scanner(new AsyncStreamPipe(stream).Input, SaveGameEncoding);
            var stopBytes = SaveGameEncoding.GetBytes("\r\n\t{}=");

            await foreach (var (token, stoppedBy) in scanner.ReadTokensAsync(stopBytes))
            {
                switch ((char)stoppedBy)
                {
                    case '{':
                        tokens.Add((token, TokenTypes.Start));
                        break;
                    case '}':
                        tokens.Add((token, TokenTypes.End));
                        break;
                    case '=':
                        tokens.Add((token, TokenTypes.Equal));
                        break;
                    case '\r':
                    case '\n':
                    case '\t':
                    default:
                        tokens.Add((token, TokenTypes.Value));
                        break;
                }
            }

            return tokens;
        }

        internal static object ParseTokenType(string c)
        {
            bool scan(string input, string pattern, out Match match)
            {
                match = Regex.Match(input, pattern);
                return match.Success;
            }

            Match m;
            if (scan(c, @"""(.*?)""", out m))
            {
                return m.Groups[1].Value;
            }
            else if (scan(c, @"(\d+)\.(\d+)\.(\d+)", out m))
            {
                var year = m.Groups[1].Value;
                var month = m.Groups[2].Value;
                var day = m.Groups[3].Value;
                return new DateTime(int.Parse(year), int.Parse(month), int.Parse(day));
            }
            else if (scan(c, @"(-?\d+\.\d+)", out m))
            {
                return double.Parse(m.Groups[1].Value);
            }
            else if (scan(c, @"\b(yes|no)\b", out m))
            {
                return m.Groups[1].Value == "yes";
            }
            else if (scan(c, @"(-?\d+)", out m))
            {
                return int.Parse(m.Groups[1].Value);
            }
            //Some extra types:
            // few-many-none
            // unchecked
            else if (scan(c, @"[a-zA-Z_\-][a-zA-Z_.0-9\-]*", out _))
            {
                return c;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        internal static void TokensToJson(IReadOnlyList<(string token, TokenTypes stoppedBy)> parsedTokens, IBufferWriter<byte> writer)
        {
            var json = new Utf8JsonWriter(writer);
            json.WriteStartObject();

            void WriteArray(ref Utf8JsonWriter in_json, string value)
            {
                foreach (var element in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    switch (ParseTokenType(element))
                    {
                        case string s:
                            in_json.WriteStringValue(s);
                            break;
                        case int n:
                            in_json.WriteNumberValue(n);
                            break;
                        case float f:
                            in_json.WriteNumberValue(f);
                            break;
                        case bool b:
                            in_json.WriteBooleanValue(b);
                            break;
                        case DateTime d:
                            in_json.WriteStringValue($"{d.Year}.{d.Month}.{d.Day}");
                            break;
                    }
                }
            }

            void WriteObject(ref Utf8JsonWriter in_json, string key, string value)
            {
                switch (ParseTokenType(value))
                {
                    case string s:
                        in_json.WriteString(key, s);
                        break;
                    case int n:
                        in_json.WriteNumber(key, n);
                        break;
                    case float f:
                        in_json.WriteNumber(key, f);
                        break;
                    case bool b:
                        in_json.WriteBoolean(key, b);
                        break;
                    case DateTime d:
                        in_json.WriteString(key, $"{d.Year}.{d.Month}.{d.Day}");
                        break;
                }
            }

            Stack<bool> subitems = new Stack<bool>();
            for (int i = 0; i < parsedTokens.Count; i++)
            {
                var (token, stoppedBy) = parsedTokens[i];
                switch (stoppedBy)
                {
                    case TokenTypes.Equal:
                        {
                            var key = token;
                            var next = parsedTokens[i + 1];
                            if (next.stoppedBy == TokenTypes.Value)
                            {
                                WriteObject(ref json, key, next.token);
                                i++;
                            }
                        }
                        break;
                    case TokenTypes.Start:
                        {
                            var prev = parsedTokens[i - 1];
                            var next = parsedTokens[i + 1];
                            switch (next.stoppedBy)
                            {
                                case TokenTypes.Value:
                                case TokenTypes.End:
                                    json.WriteStartArray(prev.token);
                                    subitems.Push(false); //array
                                    break;
                                default:
                                    json.WriteStartObject(prev.token);
                                    subitems.Push(true); //object
                                    break;
                            }
                        }
                        break;
                    case TokenTypes.End:
                        {
                            var isObject = subitems.Pop();
                            if (isObject)
                            {
                                json.WriteEndObject();
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(token))
                                {
                                    WriteArray(ref json, token);
                                }
                                json.WriteEndArray();
                            }
                        }
                        break;
                    default:
                        if (i == 0 && token == CK2Header)
                        {
                            json.WriteStartObject(token);
                            subitems.Push(true);
                        }
                        else if (string.IsNullOrWhiteSpace(token))
                        {
                            //
                        }
                        else if (subitems.Peek() == false) //array
                        {
                            WriteArray(ref json, token);
                        }
                        else
                        {
                            throw new InvalidOperationException("Unexpected token type");
                        }
                        break;
                }
            }

            if (subitems.Count > 0)
                throw new InvalidOperationException("Parse finished with incomplete subitems");

            json.WriteEndObject();
            json.Flush(isFinalBlock: true);
        }

    }
}
