using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AgileDesign.Utilities
{
    public static class StringExtensions
    {
        /// <summary>
        ///   Unlike [stringInstance].Normalize() this method
        ///   removes all duplicated white spaces 
        ///   both at ends and within a string value
        /// </summary>
        public static string NormalizedSpaces(this string value)
        {
            if (value == null)
            {
                return null;
            }

            return Regex.Replace(value, @"\s+", " ").Trim();
        }

        public static string Right
            (
                this string value,
                int length
            )
        {
            if (value == null)
            {
                return null;
            }

            return value.Substring(value.Length - length, length);
        }

        public static string Left
            (
                this string value,
                int length
            )
        {
            if (value == null)
            {
                return null;
            }
            if(value.Length <= length)
            {
                return value;
            }
            return value.Substring(0, length);
        }

        public static string ConvertToString(this byte[] input)
        {
            return string.Join("", input.Select(b => b.ToString("x2")));
        }

        public static bool IsMatch(this string input, string pattern)
        {
            return Regex.IsMatch(input, pattern);
        }

        public static string ToDisplayString(this IDictionary input)
        {
            Contract.Requires<ArgumentNullException>(input != null);

            var mappings = new StringBuilder();
            foreach (var key in input.Keys)
            {
                mappings.AppendFormat("{0}={1}", key, input[key]);
                mappings.AppendLine();
            }
            return mappings.ToString();
        }

    }
}