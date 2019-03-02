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
                                switch (ParseTokenType(next.token))
                                {
                                    case string s:
                                        json.WriteString(key, s);
                                        break;
                                    case int n:
                                        json.WriteNumber(key, n);
                                        break;
                                    case float f:
                                        json.WriteNumber(key, f);
                                        break;
                                    case bool b:
                                        json.WriteBoolean(key, b);
                                        break;
                                    case DateTime d:
                                        json.WriteString(key, $"{d.Year}.{d.Month}.{d.Day}");
                                        break;
                                }
                                i++;
                            }
                        }
                        break;
                    case TokenTypes.Start:
                        {
                            var prev = parsedTokens[i - 1];
                            var next = parsedTokens[i + 1];
                            if (next.stoppedBy == TokenTypes.Value)
                            {
                                json.WriteStartArray(prev.token);
                                subitems.Push(false); //array
                            }
                            else
                            {
                                json.WriteStartObject(prev.token);
                                subitems.Push(true); //object
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
                            foreach (var element in token.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                            {
                                switch (ParseTokenType(element))
                                {
                                    case string s:
                                        json.WriteStringValue(s);
                                        break;
                                    case int n:
                                        json.WriteNumberValue(n);
                                        break;
                                    case float f:
                                        json.WriteNumberValue(f);
                                        break;
                                    case bool b:
                                        json.WriteBooleanValue(b);
                                        break;
                                    case DateTime d:
                                        json.WriteStringValue($"{d.Year}.{d.Month}.{d.Day}");
                                        break;
                                }
                            }
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
