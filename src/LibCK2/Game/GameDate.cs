using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LibCK2.Game
{
    public readonly struct GameDate
    {
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }

        public GameDate(int year, int month, int day) => (Year, Month, Day) = (year, month, day);

        public override string ToString() => $"{Year}.{Month}.{Day}";

        private static readonly Regex r_tDate = new Regex(@"(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);
        public static GameDate Parse(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var m = r_tDate.Match(value);
            if (!m.Success)
                throw new FormatException($"Value was not in the correct format");

            return Parse(m);
        }
        internal static GameDate Parse(Match m)
            => new GameDate(year: int.Parse(m.Groups[1].Value), month: int.Parse(m.Groups[2].Value), day: int.Parse(m.Groups[3].Value));
    }
}
