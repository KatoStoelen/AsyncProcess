#if NETSTANDARD2_0
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AsyncProcess.Internal.Extensions
{
    internal static class StringExtensions
    {
        private static readonly IReadOnlyCollection<char> s_quoteCharacters;

        static StringExtensions()
        {
            var quoteCharacters = new List<char>(2) { '"' };

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                quoteCharacters.Add('\'');
            }

            s_quoteCharacters = quoteCharacters.AsReadOnly();
        }

        public static string? AddQuotesIfContainsWhitespace(this string? @string)
        {
            if (@string == null || @string.IsQuoted() || !@string.Any(char.IsWhiteSpace))
            {
                return @string;
            }

            return $"\"{@string}\"";
        }

        private static bool IsQuoted(this string? @string)
        {
            if (@string == null || @string.Length < 2)
            {
                return false;
            }

            var firstCharacter = @string[0];
            var lastCharacter = @string[@string.Length - 1];

            return
                firstCharacter == lastCharacter &&
                s_quoteCharacters.Contains(firstCharacter);
        }
    }
}
#endif
