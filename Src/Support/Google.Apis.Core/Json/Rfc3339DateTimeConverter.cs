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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Google.Apis.Json
{
    /// <summary>
    /// A <see cref="System.Text.Json"/> converter which writes <see cref="DateTime"/> values honoring RFC 3339,
    /// so that the serialized date is accepted by Google services. This is the System.Text.Json equivalent of the
    /// behavior previously provided by the Newtonsoft-based serializer.
    /// </summary>
    /// <remarks>
    /// On read, the standard System.Text.Json <see cref="DateTime"/> parsing is used, which accepts RFC 3339
    /// (a profile of ISO 8601). System.Text.Json automatically applies this converter to <see cref="Nullable{DateTime}"/>
    /// properties as well.
    /// </remarks>
    public sealed class Rfc3339DateTimeConverter : JsonConverter<DateTime>
    {
        /// <inheritdoc/>
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTime();

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Utilities.ConvertToRFC3339(value));
    }
}
