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

using Google.Apis.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Apis.Json
{
    /// <summary>
    /// An <see cref="IJsonSerializer"/> implemented on top of <see cref="System.Text.Json"/> using
    /// source-generated metadata (no reflection-based serialization), so it is compatible with trimming and
    /// ahead-of-time (AOT) compilation.
    /// </summary>
    /// <remarks>
    /// Each assembly that contains serializable types contributes a source-generated
    /// <see cref="JsonSerializerContext"/> via <see cref="RegisterTypeInfoResolver(IJsonTypeInfoResolver)"/>
    /// (typically from a module initializer). The serializer resolves type metadata across all registered
    /// contexts, applies the RFC 3339 date handling and the explicit-null behavior, and ignores <c>null</c>
    /// values when writing (matching the previous Newtonsoft-based default).
    /// </remarks>
    public sealed class SystemTextJsonSerializer : IJsonSerializer
    {
        private static readonly object _registrationLock = new object();
        // Snapshot array, swapped under _registrationLock; read without locking on the serialization hot path.
        private static volatile IJsonTypeInfoResolver[] _resolvers = Array.Empty<IJsonTypeInfoResolver>();

        private static readonly JsonSerializerOptions _options = CreateOptions();

        /// <summary>The default instance of the serializer.</summary>
        public static SystemTextJsonSerializer Instance { get; } = new SystemTextJsonSerializer();

        static SystemTextJsonSerializer()
        {
            // Core's own serializable types (errors, standard responses, etc.).
            RegisterTypeInfoResolver(GoogleApisCoreJsonContext.Default);
        }

        private SystemTextJsonSerializer()
        {
        }

        /// <summary>
        /// Registers a source-generated <see cref="IJsonTypeInfoResolver"/> (typically a
        /// <see cref="JsonSerializerContext"/>) so that the types it describes can be serialized and deserialized.
        /// Intended to be called once per assembly, usually from a module initializer. Registration is idempotent
        /// and thread-safe, and may occur after the serializer has first been used.
        /// </summary>
        /// <param name="resolver">The resolver to register. Must not be null.</param>
        public static void RegisterTypeInfoResolver(IJsonTypeInfoResolver resolver)
        {
            resolver.ThrowIfNull(nameof(resolver));
            lock (_registrationLock)
            {
                foreach (var existing in _resolvers)
                {
                    if (ReferenceEquals(existing, resolver))
                    {
                        return;
                    }
                }
                var updated = new IJsonTypeInfoResolver[_resolvers.Length + 1];
                Array.Copy(_resolvers, updated, _resolvers.Length);
                updated[_resolvers.Length] = resolver;
                _resolvers = updated;
            }
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                // Match Newtonsoft's minimal escaping (e.g. '"' -> '\"', and don't \uXXXX-escape <, >, &, ').
                // Appropriate for API JSON payloads (not HTML-embedded), and keeps output byte-compatible.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                // Newtonsoft accepted these leniencies on read; keep them for drop-in compatibility.
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                // Google APIs serialize int64/uint64 (etc.) as JSON strings; Newtonsoft coerced string<->number.
                // Read numbers from strings to restore that (writing stays numeric). See BC-018.
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
            };
            options.Converters.Add(new Rfc3339DateTimeConverter());
            // The combining resolver reads the live registration snapshot, so contexts registered after these
            // options are first used are still picked up for types not yet resolved.
            options.TypeInfoResolver = new CombiningResolver().WithAddedModifier(ExplicitNullJsonModifier.Modify);
            return options;
        }

        /// <inheritdoc/>
        public string Format => "json";

        /// <inheritdoc/>
        public void Serialize(object obj, Stream target)
        {
            obj ??= string.Empty;
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, obj, _options.GetTypeInfo(obj.GetType()));
        }

        /// <inheritdoc/>
        public string Serialize(object obj)
        {
            obj ??= string.Empty;
            return JsonSerializer.Serialize(obj, _options.GetTypeInfo(obj.GetType()));
        }

        /// <inheritdoc/>
        public T Deserialize<T>(string input) =>
            string.IsNullOrEmpty(input) ? default : (T) Deserialize(input, typeof(T));

        /// <inheritdoc/>
        public object Deserialize(string input, Type type)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }
            var typeInfo = _options.GetTypeInfo(type);
            try
            {
                return JsonSerializer.Deserialize(input, typeInfo);
            }
            catch (JsonException)
            {
                // System.Text.Json is stricter than Newtonsoft about raw control characters inside strings
                // (BC-004). As a best-effort compatibility fallback, repair such input and retry once. The repair
                // returns null when there is nothing to fix, in which case the original error is genuine.
                var repaired = JsonInputRepair.EscapeUnescapedControlCharacters(input);
                if (repaired is null)
                {
                    throw;
                }
                return JsonSerializer.Deserialize(repaired, typeInfo);
            }
        }

        /// <inheritdoc/>
        public T Deserialize<T>(Stream input)
        {
            // Buffer to a string so the same lenient-input repair as the string overload applies. Response bodies
            // are already read to a string before deserialization, so this does not regress the hot path.
            using var reader = new StreamReader(input);
            return (T) Deserialize(reader.ReadToEnd(), typeof(T));
        }

        /// <summary>
        /// Deserializes the given stream, reading from it asynchronously and observing the given cancellation token.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="input">The stream to read from.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The deserialized object.</returns>
        public async Task<T> DeserializeAsync<T>(Stream input, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(input);
            string json = await reader.ReadToEndAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return (T) Deserialize(json, typeof(T));
        }

        /// <summary>
        /// An <see cref="IJsonTypeInfoResolver"/> that consults every registered resolver in turn. It reads the
        /// live snapshot so that contexts registered after the owning options are first used are still honored.
        /// </summary>
        private sealed class CombiningResolver : IJsonTypeInfoResolver
        {
            public JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
            {
                foreach (var resolver in _resolvers)
                {
                    var typeInfo = resolver.GetTypeInfo(type, options);
                    if (typeInfo is not null)
                    {
                        return typeInfo;
                    }
                }
                return null;
            }
        }
    }
}
