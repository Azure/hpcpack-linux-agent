using System.Text.RegularExpressions;

namespace NodeAgent.Utils
{
    public static class StringExtensions
    {
        private static readonly char ASTERISK = '*';

        /* Note
         * 
         * Check if a string matches a pattern string with asterisk, case-sensitive.
         * The Regex in C# is not used here because asterisk means zero or more occurrences of the preceding element.
         * But in this case, asterisk is a wildcard that can match any characters.
         * e.g. "abcd" matches "abcd", "a*", "ab*", "*b*", "**b*", "*", but not "Abcd", "a*c", "*bc"
         */
        public static bool AsteriskMatch(this string str, string patternStr)
        {
            if (string.IsNullOrEmpty(str))
            {
                throw new ArgumentNullException(nameof(str));
            }
            if (string.IsNullOrEmpty(patternStr))
            {
                throw new ArgumentNullException(nameof(patternStr));
            }
            if (!patternStr.Contains(ASTERISK))
            {
                return str.Equals(patternStr);
            }

            var first = true;
            var pos = 0;
            var patterns = patternStr.Split(ASTERISK);
            var last = patterns.Last();
            for (int i = 0; i < patterns.Length - 1; i++)
            {
                var p = patterns[i];
                pos = str.IndexOf(p, pos);
                if ((first && pos != 0) || pos == -1)
                {
                    return false;
                }
                pos += p.Length;
                first = false;
            }

            if (patternStr.Last() == ASTERISK)
            {
                pos = str.IndexOf(last, pos);
                return first ? pos == 0 : pos != -1;
            }

            var lastpos = str.LastIndexOf(last);
            return lastpos != -1 && lastpos >= pos && lastpos + last.Length == str.Length;
        }

        /* Note
         * 
         * Retrieve the float value from a string. Return 0.0 if no float value is found.
         * e.g. "abc 123.45 def" returns 123.45, "abc 100 def" returns 100.0, "N/A" returns 0.0
         */
        public static float RemoveMeasurement(this string str)
        {
            var regex = new Regex(@"\d+(\.\d+)?");
            var match = regex.Match(str);
            if (match.Success)
            {
                return float.TryParse(match.Value, out float result) ? result : 0.0f;
            }
            else
            {
                return 0.0f;
            }
        }
    }
}
