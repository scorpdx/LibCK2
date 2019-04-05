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

using LibCK2.Game;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TokenTypes = LibCK2.Parsing.Scanner.TokenTypes;
using static LibCK2.SaveGame;

namespace LibCK2.Parsing
{
    internal static class CK2Parsing
    {
        //var scanner = new Scanner(new AsyncStreamPipe(stream, new System.IO.Pipelines.PipeOptions(pauseWriterThreshold: 1024 * 1024)).Input, SaveGameEncoding);

        private static readonly Regex r_tString = new Regex(@"""(.*?)""", RegexOptions.Compiled);
        private static readonly Regex r_tDate = new Regex(@"(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);
        private static readonly Regex r_tFloat = new Regex(@"(-?\d+\.\d+)", RegexOptions.Compiled);
        private static readonly Regex r_tBool = new Regex(@"\b(yes|no)\b", RegexOptions.Compiled);
        private static readonly Regex r_tInt = new Regex(@"(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex r_tSym = new Regex(@"([a-zA-Z_\-][a-zA-Z_.0-9\-]*)", RegexOptions.Compiled);
        internal static object ParseTokenType(string c)
        {
            Match m;
            if ((m = r_tString.Match(c)).Success)
            {
                return m.Groups[1].Value;
            }
            else if ((m = r_tDate.Match(c)).Success)
            {
                return GameDate.Parse(m);
            }
            else if ((m = r_tFloat.Match(c)).Success)
            {
                return float.Parse(m.Groups[1].Value);
            }
            else if ((m = r_tBool.Match(c)).Success)
            {
                return m.Groups[1].Value == "yes";
            }
            else if ((m = r_tInt.Match(c)).Success)
            {
                return int.Parse(m.Groups[1].Value);
            }
            //Some extra types:
            // few-many-none
            // unchecked
            else if ((m = r_tSym.Match(c)).Success)
            {
                return m.Groups[1].Value;
            }
            else
            {
                throw new InvalidOperationException("Unrecognized token type");
            }
        }

        internal static async IAsyncEnumerable<(string key, object value)> TokensToTypesAsync(IAsyncEnumerable<(string token, TokenTypes stoppedBy)> tokens)
        {
            await using var tokenIterator = tokens.GetAsyncEnumerator();
            (string token, TokenTypes stoppedBy) prev = default;
            while (await tokenIterator.MoveNextAsync())
            {
                (string token, TokenTypes stoppedBy) = tokenIterator.Current;
                //System.Diagnostics.Debug.Assert((prev.token != null && stoppedBy != TokenTypes.Equal) ? prev.stoppedBy == TokenTypes.Equal : true);
                switch (stoppedBy)
                {
                    case TokenTypes.Value when token == CK2Header:
                    case TokenTypes.Open:
                        var ll = new List<(string token, TokenTypes stoppedBy)>();
                        int depth = 1;
                        while (depth > 0 && await tokenIterator.MoveNextAsync())
                        {
                            switch (tokenIterator.Current.stoppedBy)
                            {
                                case TokenTypes.Open: depth++; break;
                                case TokenTypes.Close: depth--; break;
                            }
                            ll.Add(tokenIterator.Current);
                        }
                        yield return (token == CK2Header ? token : prev.token, ll);
                        //{
                        //    //var prev = parsedTokens[i - 1];
                        //    var next = parsedTokens[i + 1];
                        //    switch (next.stoppedBy)
                        //    {
                        //        case TokenTypes.Value:
                        //        case TokenTypes.End:
                        //            json.WriteStartArray(prev.token);
                        //            subitems.Push(false); //array
                        //            break;
                        //        default:
                        //            json.WriteStartObject(prev.token);
                        //            subitems.Push(true); //object
                        //            break;
                        //    }
                        //}
                        break;
                    case TokenTypes.Close:
                        //throw new InvalidOperationException("no end");
                        //yield return (2, prev.token, null);
                        //{
                        //    var isObject = subitems.Pop();
                        //    if (isObject)
                        //    {
                        //        json.WriteEndObject();
                        //    }
                        //    else
                        //    {
                        //        if (!string.IsNullOrWhiteSpace(token))
                        //        {
                        //            WriteArray(ref json, token);
                        //        }
                        //        json.WriteEndArray();
                        //    }
                        //}
                        break;
                    //case TokenTypes.Equal:
                    //    {
                    //        var key = token;
                    //        var next = parsedTokens[i + 1];
                    //        if (next.stoppedBy == TokenTypes.Value)
                    //        {
                    //            WriteObject(ref json, key, next.token);
                    //            i++;
                    //        }
                    //    }
                    //    break;
                    case TokenTypes.Equal: break;
                    //case TokenTypes.Value when string.IsNullOrEmpty(token): break;
                    case TokenTypes.Value:
                        yield return (prev.token, ParseTokenType(token));
                        break;
                    default:
                        //    if (string.IsNullOrWhiteSpace(token))
                        //    {
                        //        //
                        //    }
                        //    else if (subitems.Peek() == false) //array
                        //    {
                        //        WriteArray(ref json, token);
                        //    }
                        //    else
                        //    {
                        throw new InvalidOperationException("Unexpected token type");
                        //    }
                        //    break;
                }
                prev = (token, stoppedBy);
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
                        case float n:
                            in_json.WriteNumberValue(n);
                            break;
                        case bool b:
                            in_json.WriteBooleanValue(b);
                            break;
                        case GameDate d:
                            in_json.WriteStringValue(d.ToString());
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
                    case float n:
                        in_json.WriteNumber(key, n);
                        break;
                    case bool b:
                        in_json.WriteBoolean(key, b);
                        break;
                    case GameDate d:
                        in_json.WriteString(key, d.ToString());
                        break;
                }
            }

            Stack<bool> subitems = new Stack<bool>();
            if (parsedTokens[0].token == CK2Header)
            {
                json.WriteStartObject(CK2Header);
                subitems.Push(true);
            }
            else //throw new InvalidOperationException("Unknown header");
            {
                json.WriteStartObject("unknown");
                subitems.Push(true);
            }

            for (int i = 1; i < parsedTokens.Count; i++)
            {
                var prev = i > 1 ? parsedTokens[i - 1] : default;
                var next = i < parsedTokens.Count - 1 ? parsedTokens[i + 1] : default;
                var (token, stoppedBy) = parsedTokens[i];
                switch (stoppedBy)
                {
                    case TokenTypes.Comment when next.stoppedBy == TokenTypes.Value:
                        json.WriteCommentValue(next.token);
                        i++;
                        break;
                    case TokenTypes.Equal when next.stoppedBy == TokenTypes.Value:
                        WriteObject(ref json, token, next.token);
                        i++;
                        break;
                    case TokenTypes.Equal when next.stoppedBy == TokenTypes.Open:
                        break;
                    case TokenTypes.Open:
                        switch (next.stoppedBy)
                        {
                            case TokenTypes.Value:
                            case TokenTypes.Close:
                                json.WriteStartArray(prev.token);
                                subitems.Push(false); //array
                                break;
                            default:
                                json.WriteStartObject(prev.token);
                                subitems.Push(true); //object
                                break;
                        }
                        break;
                    case TokenTypes.Close:
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
                        break;
                    default:
                        if (string.IsNullOrWhiteSpace(token))
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
