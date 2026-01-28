/*
* Licensed to the Apache Software Foundation (ASF) under one or more
* contributor license agreements.  See the NOTICE file distributed with
* this work for additional information regarding copyright ownership.
* The ASF licenses this file to You under the Apache License, Version 2.0
* (the "License"); you may not use this file except in compliance with
* the License.  You may obtain a copy of the License at
*
*    http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Apache.Arrow.Adbc.Drivers.Apache.Hive2.Metadata
{
    /// <summary>
    /// Utility methods for metadata pattern matching and conversion.
    /// Provides SQL pattern conversion (% and _) to regular expressions and other pattern utilities.
    /// </summary>
    public static class MetadataPatternConverter
    {
        /// <summary>
        /// Converts an SQL pattern (with % and _ wildcards) to a .NET regular expression.
        /// SQL patterns use:
        /// - % for zero or more characters
        /// - _ for exactly one character
        /// - \ as an escape character
        /// </summary>
        /// <param name="sqlPattern">The SQL pattern to convert (e.g., "test_%")</param>
        /// <param name="caseSensitive">Whether the resulting regex should be case-sensitive</param>
        /// <returns>A regular expression string matching the SQL pattern</returns>
        public static string ConvertSqlPatternToRegex(string? sqlPattern, bool caseSensitive = false)
        {
            if (string.IsNullOrEmpty(sqlPattern))
                return ".*"; // Match everything

            var regex = new StringBuilder("^");
            bool escaped = false;

            foreach (char c in sqlPattern)
            {
                if (escaped)
                {
                    // Escape special regex characters
                    regex.Append(Regex.Escape(c.ToString()));
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '%')
                {
                    regex.Append(".*");
                }
                else if (c == '_')
                {
                    regex.Append(".");
                }
                else
                {
                    // Escape special regex characters
                    regex.Append(Regex.Escape(c.ToString()));
                }
            }

            regex.Append('$');

            return regex.ToString();
        }

        /// <summary>
        /// Creates a Regex object from an SQL pattern.
        /// </summary>
        /// <param name="sqlPattern">The SQL pattern to convert</param>
        /// <param name="caseSensitive">Whether the resulting regex should be case-sensitive</param>
        /// <returns>A Regex object matching the SQL pattern</returns>
        public static Regex CreateRegexFromSqlPattern(string? sqlPattern, bool caseSensitive = false)
        {
            string regexPattern = ConvertSqlPatternToRegex(sqlPattern, caseSensitive);
            var options = RegexOptions.Compiled;
            if (!caseSensitive)
                options |= RegexOptions.IgnoreCase;

            return new Regex(regexPattern, options);
        }

        /// <summary>
        /// Determines whether a value matches an SQL pattern.
        /// </summary>
        /// <param name="value">The value to test</param>
        /// <param name="sqlPattern">The SQL pattern to match against</param>
        /// <param name="caseSensitive">Whether the match should be case-sensitive</param>
        /// <returns>True if the value matches the pattern, false otherwise</returns>
        public static bool MatchesSqlPattern(string? value, string? sqlPattern, bool caseSensitive = false)
        {
            if (string.IsNullOrEmpty(sqlPattern))
                return true; // Null or empty pattern matches everything

            if (string.IsNullOrEmpty(value))
                return false; // Null or empty value doesn't match non-empty pattern

            var regex = CreateRegexFromSqlPattern(sqlPattern, caseSensitive);
            return regex.IsMatch(value);
        }

        /// <summary>
        /// Normalizes an identifier by removing quotes if present.
        /// Handles both double quotes (") and backticks (`).
        /// </summary>
        /// <param name="identifier">The identifier to normalize</param>
        /// <returns>The identifier without surrounding quotes</returns>
        public static string? NormalizeIdentifier(string? identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return identifier;

            if ((identifier.StartsWith("\"") && identifier.EndsWith("\"")) ||
                (identifier.StartsWith("`") && identifier.EndsWith("`")))
            {
                if (identifier.Length > 2)
                    return identifier.Substring(1, identifier.Length - 2);
                return string.Empty;
            }

            return identifier;
        }

        /// <summary>
        /// Determines whether a pattern is an exact match (no wildcards).
        /// </summary>
        /// <param name="pattern">The pattern to check</param>
        /// <returns>True if the pattern contains no wildcards, false otherwise</returns>
        public static bool IsExactMatch(string? pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            return !pattern.Contains("%") && !pattern.Contains("_");
        }

        /// <summary>
        /// Escapes SQL pattern wildcards in a string to treat them as literals.
        /// Useful when you need to search for literal % or _ characters.
        /// </summary>
        /// <param name="value">The value to escape</param>
        /// <returns>The value with % and _ escaped with backslashes</returns>
        public static string? EscapeSqlWildcards(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Replace("\\", "\\\\")
                        .Replace("%", "\\%")
                        .Replace("_", "\\_");
        }
    }
}
