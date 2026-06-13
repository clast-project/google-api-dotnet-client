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

using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Google.Apis.Auth.Json
{
    /// <summary>
    /// Source-generated <see cref="System.Text.Json"/> metadata for the serializable types in Google.Apis.Auth.
    /// </summary>
    // NumberHandling=AllowReadingFromString: read int64/uint64 etc. from JSON strings (Google APIs emit them as
    // strings; Newtonsoft coerced). See BEHAVIORAL-CHANGES.md BC-018.
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
    [JsonSerializable(typeof(TokenResponse))]
    [JsonSerializable(typeof(TokenErrorResponse))]
    [JsonSerializable(typeof(JsonCredentialParameters))]
    [JsonSerializable(typeof(GoogleClientSecrets))]
    [JsonSerializable(typeof(ClientSecrets))]
    [JsonSerializable(typeof(IamSignBlobRequest))]
    [JsonSerializable(typeof(IamAccessTokenRequest))]
    [JsonSerializable(typeof(IamOIdCTokenRequest))]
    [JsonSerializable(typeof(IamSignBlobResponse))]
    [JsonSerializable(typeof(JsonWebSignature.Header), TypeInfoPropertyName = "JsonWebSignatureHeader")]
    [JsonSerializable(typeof(JsonWebSignature.Payload), TypeInfoPropertyName = "JsonWebSignaturePayload")]
    [JsonSerializable(typeof(GoogleJsonWebSignature.Header), TypeInfoPropertyName = "GoogleJsonWebSignatureHeader")]
    [JsonSerializable(typeof(GoogleJsonWebSignature.Payload), TypeInfoPropertyName = "GoogleJsonWebSignaturePayload")]
    [JsonSerializable(typeof(AwsExternalAccountCredential.AwsSignedSubjectToken))]
    [JsonSerializable(typeof(AwsExternalAccountCredential.AwsSecurityCredentials.AwsSecurityCredentialsResponse))]
    [JsonSerializable(typeof(StsRequestOptions))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(List<string>))]
    internal partial class GoogleApisAuthJsonContext : JsonSerializerContext
    {
    }

#pragma warning disable CA2255 // ModuleInitializer is intentional: this library self-registers its JSON context.
    internal static class GoogleApisAuthJsonModule
    {
        [ModuleInitializer]
        internal static void Initialize() =>
            SystemTextJsonSerializer.RegisterTypeInfoResolver(GoogleApisAuthJsonContext.Default);
    }
#pragma warning restore CA2255
}

namespace Google.Apis.Auth
{
    /// <summary>
    /// Converts the JWT <c>aud</c> claim, which may be either a single string or an array of strings, to/from the
    /// <see cref="object"/>-typed <see cref="JsonWebToken.Payload.Audience"/> property. Reproduces the Newtonsoft
    /// behavior of materializing a <see cref="string"/> or a <see cref="List{String}"/> (rather than a
    /// <c>JsonElement</c>), which <see cref="JsonWebToken.Payload.AudienceAsList"/> relies on.
    /// </summary>
    internal sealed class AudienceJsonConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.StartArray:
                    var list = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(reader.GetString());
                    }
                    return list;
                default:
                    throw new JsonException($"Unexpected token '{reader.TokenType}' for the audience claim.");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string single:
                    writer.WriteStringValue(single);
                    break;
                case IEnumerable<string> many:
                    writer.WriteStartArray();
                    foreach (var item in many)
                    {
                        writer.WriteStringValue(item);
                    }
                    writer.WriteEndArray();
                    break;
                default:
                    throw new JsonException($"Unsupported audience value type '{value.GetType()}'.");
            }
        }
    }
}

namespace Google.Apis.Auth.OAuth2.Requests
{
    /// <summary>
    /// The options payload for an STS token-exchange request, serialized as JSON. Replaces an anonymous type so that
    /// it can be handled by the source-generated serializer.
    /// </summary>
    internal sealed class StsRequestOptions
    {
        [JsonPropertyName("userProject")]
        public string UserProject { get; set; }
    }
}
