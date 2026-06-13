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

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Google.Apis.Json
{
    /// <summary>
    /// Reproduces the Newtonsoft "explicit null" behavior under System.Text.Json without any reflection-based
    /// serialization, so it remains compatible with trimming and ahead-of-time (AOT) compilation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The values produced by <see cref="JsonExplicitNull"/> are exposed through <see cref="IList{T}"/> and carry the
    /// <see cref="JsonExplicitNullAttribute"/> on their runtime type. System.Text.Json selects converters by the
    /// <em>declared</em> property type, so this modifier installs a single shared converter on every property whose
    /// declared type is a closed <see cref="IList{T}"/>. At write time that converter emits a literal <c>null</c> for an
    /// explicit-null sentinel and otherwise delegates to the (source-generated) type info for the actual value.
    /// </para>
    /// </remarks>
    internal static class ExplicitNullJsonModifier
    {
        /// <summary>
        /// A <see cref="JsonTypeInfo"/> modifier suitable for use with
        /// <see cref="JsonTypeInfoResolver.WithAddedModifier"/>.
        /// </summary>
        public static void Modify(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }
            foreach (JsonPropertyInfo property in typeInfo.Properties)
            {
                Type type = property.PropertyType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
                {
                    property.CustomConverter = new ExplicitNullListConverter(type);
                }
            }
        }

        /// <summary>
        /// A non-generic converter installed per-property on <see cref="IList{T}"/> members. It captures the declared
        /// list type so it can delegate to the built-in (source-generated) type info without constructing closed
        /// generic types, keeping it compatible with trimming and AOT.
        /// </summary>
        private sealed class ExplicitNullListConverter : JsonConverter<object>
        {
            private readonly Type _listType;

            public ExplicitNullListConverter(Type listType) => _listType = listType;

            public override bool CanConvert(Type typeToConvert) => typeToConvert == _listType;

            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }
                // The built-in type info for IList<T> is of Enumerable kind and so is left untouched by this
                // modifier, meaning delegating to it here introduces no re-entrancy.
                return JsonSerializer.Deserialize(ref reader, options.GetTypeInfo(_listType));
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNullValue();
                    return;
                }
                if (value.GetType().IsDefined(typeof(JsonExplicitNullAttribute), inherit: false))
                {
                    writer.WriteNullValue();
                    return;
                }
                // The value (e.g. List<T>) is assignable to the declared IList<T>, so it serializes correctly
                // through the declared type's built-in type info.
                JsonSerializer.Serialize(writer, value, options.GetTypeInfo(_listType));
            }
        }
    }
}
