using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHARED.Common
{
    public static class RandomString
    {
        public static string Generate(int size)
        {
            var random = new Random();
            var chars = "$@#.ABCDEFGHIJKLMNOPQRSTUVWXYAabcdefghijklmnopqrstuvwxyz123456789.-_&";

            return new string(Enumerable.Repeat(chars, size)
                .Select((s) => s[random.Next(s.Length)]).ToArray() );
        }
    }
}
