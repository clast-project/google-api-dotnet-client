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

using Google.Apis.Auth;
using Google.Apis.Auth.Tests.OAuth2;
using Google.Apis.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : System.Attribute { }
}
#endif

namespace Google.Apis.Auth.Tests
{
    /// <summary>
    /// Source-generated metadata for the test-only types deserialized through <see cref="SystemTextJsonSerializer"/>.
    /// Demonstrates the BC-010 consumer pattern: caller-defined types (e.g. a custom JWT payload) must register
    /// their own context. The base <c>Payload</c> types are registered with explicit names to avoid the
    /// generator's duplicate-simple-name collision (SYSLIB1031).
    /// </summary>
    [JsonSerializable(typeof(AwsExternalAccountCredentialsTests.AwsSignedSubjectToken))]
    [JsonSerializable(typeof(JsonWebSignatureTests.CustomPayload))]
    [JsonSerializable(typeof(JsonWebSignature.Payload), TypeInfoPropertyName = "JsonWebSignaturePayload")]
    [JsonSerializable(typeof(JsonWebToken.Payload), TypeInfoPropertyName = "JsonWebTokenPayload")]
    internal partial class AuthTestsJsonContext : JsonSerializerContext
    {
    }

#pragma warning disable CA2255 // Module initializer is appropriate here: the test assembly self-registers its context.
    internal static class AuthTestsJsonModule
    {
        [ModuleInitializer]
        internal static void Initialize() =>
            SystemTextJsonSerializer.RegisterTypeInfoResolver(AuthTestsJsonContext.Default);
    }
#pragma warning restore CA2255
}
