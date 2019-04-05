using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LibCK2.Parsing
{
    internal static class AsyncEnumerableExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> asyncEnumerable)
        {
            var ret = new List<T>();
            await foreach(var val in asyncEnumerable)
            {
                ret.Add(val);
            }
            return ret;
        }
    }
}
