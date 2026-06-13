/*
Copyright 2026 Google Inc

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

using System;
using System.Collections.Concurrent;

namespace Google.Apis.Util
{
    /// <summary>
    /// Registry of source-generated maps from enum values to their <see cref="StringValueAttribute"/> wire strings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Historically the wire string for an enum value was discovered with reflection
    /// (<c>enumType.GetField(value.ToString()).GetCustomAttribute&lt;StringValueAttribute&gt;()</c>). That is not
    /// trim-safe: AOT trimming can drop the enum field metadata or the attribute, silently producing the wrong wire
    /// value. Instead, each generated client emits a small per-enum converter (a plain <c>switch</c>) and registers
    /// it here from a module initializer, so <see cref="Utilities.ConvertToString(object)"/> never needs reflection.
    /// </para>
    /// <para>
    /// This mirrors the JSON context registration performed by
    /// <see cref="Google.Apis.Json.SystemTextJsonSerializer.RegisterTypeInfoResolver"/>.
    /// </para>
    /// </remarks>
    public static class EnumStringValueRegistry
    {
        private static readonly ConcurrentDictionary<Type, Func<Enum, string>> s_converters =
            new ConcurrentDictionary<Type, Func<Enum, string>>();

        /// <summary>
        /// Registers a converter that returns the wire string for any value of the given enum type. The converter
        /// must reproduce the legacy behavior: return the <see cref="StringValueAttribute"/> text if the member has
        /// one, otherwise the member name (<see cref="object.ToString"/>). Intended to be called once per enum type,
        /// usually from a generated module initializer. Registration is idempotent and thread-safe.
        /// </summary>
        /// <param name="enumType">The enum type. Must not be null.</param>
        /// <param name="converter">The value-to-wire-string converter. Must not be null.</param>
        public static void Register(Type enumType, Func<Enum, string> converter)
        {
            enumType.ThrowIfNull(nameof(enumType));
            converter.ThrowIfNull(nameof(converter));
            s_converters[NormalizeKey(enumType)] = converter;
        }

        /// <summary>
        /// Attempts to convert an enum value to its wire string using a registered converter.
        /// </summary>
        /// <returns><c>true</c> if a converter was registered for the value's type; otherwise <c>false</c>.</returns>
        internal static bool TryConvert(Enum value, out string text)
        {
            if (s_converters.TryGetValue(NormalizeKey(value.GetType()), out var converter))
            {
                text = converter(value);
                return true;
            }
            text = null;
            return false;
        }

        // An enum nested in a generic type (e.g. the generated "&lt;Client&gt;BaseServiceRequest&lt;TResponse&gt;.AltEnum")
        // is, in the CLR, a distinct constructed type per enclosing type argument. We key by the generic type
        // definition so every instantiation shares one converter (and the registration can name any single
        // instantiation). Non-generic enums are their own key.
        private static Type NormalizeKey(Type enumType) =>
            enumType.IsGenericType ? enumType.GetGenericTypeDefinition() : enumType;
    }
}
