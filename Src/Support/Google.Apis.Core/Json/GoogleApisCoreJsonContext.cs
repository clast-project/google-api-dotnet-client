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

using Google.Apis.Requests;
using Google.Apis.Util;
using System.Text.Json.Serialization;

namespace Google.Apis.Json
{
    /// <summary>
    /// Source-generated <see cref="System.Text.Json"/> metadata for the serializable types owned by
    /// Google.Apis.Core (server errors and the legacy standard-response envelope used for error parsing).
    /// </summary>
    // NumberHandling=AllowReadingFromString: Google APIs serialize int64/uint64 (and similar) as JSON strings;
    // Newtonsoft coerced string<->number, STJ does not by default. Read-from-string restores that (we still write
    // numbers as numbers, matching the previous behavior). See BEHAVIORAL-CHANGES.md BC-018.
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
    [JsonSerializable(typeof(RequestError))]
    [JsonSerializable(typeof(SingleError))]
    [JsonSerializable(typeof(StandardResponse<object>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(object))]
    internal partial class GoogleApisCoreJsonContext : JsonSerializerContext
    {
    }
}
