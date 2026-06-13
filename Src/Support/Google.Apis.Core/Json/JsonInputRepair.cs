/*
Copyright 2025 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Globalization;
using System.Text;

namespace Google.Apis.Json
{
    /// <summary>
    /// Best-effort repair of inputs that Newtonsoft.Json accepted but System.Text.Json rejects, used only as a
    /// fallback after a strict parse fails. See BC-004 in BEHAVIORAL-CHANGES.md.
    /// </summary>
    internal static class JsonInputRepair
    {
        /// <summary>
        /// Returns a copy of <paramref name="input"/> in which raw control characters (&lt; U+0020) that appear
        /// inside JSON string literals are escaped, making the text valid per RFC 8259. Returns <c>null</c> if there
        /// were no such characters (so the caller can surface the original parse error unchanged). Structural
        /// whitespace outside strings, and already-escaped sequences, are left untouched.
        /// </summary>
        public static string EscapeUnescapedControlCharacters(string input)
        {
            StringBuilder builder = null;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (!inString)
                {
                    if (c == '"')
                    {
                        inString = true;
                    }
                    builder?.Append(c);
                    continue;
                }

                if (escaped)
                {
                    // This character is the body of an escape sequence (e.g. the 'n' in \n); pass it through.
                    escaped = false;
                    builder?.Append(c);
                }
                else if (c == '\\')
                {
                    escaped = true;
                    builder?.Append(c);
                }
                else if (c == '"')
                {
                    inString = false;
                    builder?.Append(c);
                }
                else if (c < ' ')
                {
                    // A raw control character inside a string: this is what Newtonsoft tolerated. Escape it.
                    if (builder is null)
                    {
                        builder = new StringBuilder(input.Length + 16);
                        builder.Append(input, 0, i);
                    }
                    AppendEscaped(builder, c);
                }
                else
                {
                    builder?.Append(c);
                }
            }

            return builder?.ToString();
        }

        private static void AppendEscaped(StringBuilder builder, char c)
        {
            switch (c)
            {
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                default: builder.Append("\\u").Append(((int) c).ToString("x4", CultureInfo.InvariantCulture)); break;
            }
        }
    }
}
