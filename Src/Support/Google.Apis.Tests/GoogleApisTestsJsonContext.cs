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

using Google.Apis.Json;
using Google.Apis.Tests.Apis.Requests;
using Google.Apis.Tests.Apis.Services;
using Google.Apis.Tests.Apis.Upload;
using Google.Apis.Util;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : System.Attribute { }
}
#endif

namespace Google.Apis.Tests
{
    /// <summary>
    /// Source-generated metadata for the test-only request/response models serialized through the pipeline.
    /// Distinct <see cref="JsonSerializableAttribute.TypeInfoPropertyName"/> values disambiguate the like-named
    /// mock types nested in different test classes.
    /// </summary>
    [JsonSerializable(typeof(BatchRequestTest.MockRequest), TypeInfoPropertyName = "BatchMockRequest")]
    [JsonSerializable(typeof(BatchRequestTest.MockResponse), TypeInfoPropertyName = "BatchMockResponse")]
    [JsonSerializable(typeof(ClientServiceRequestTest.MockRequest), TypeInfoPropertyName = "ClientServiceMockRequest")]
    [JsonSerializable(typeof(ClientServiceRequestTest.MockResponse), TypeInfoPropertyName = "ClientServiceMockResponse")]
    [JsonSerializable(typeof(BaseClientServiceTest.MockJsonSchema))]
    [JsonSerializable(typeof(StandardResponse<BaseClientServiceTest.MockJsonSchema>))]
    [JsonSerializable(typeof(ResumableUploadTest.TestRequest))]
    [JsonSerializable(typeof(ResumableUploadTest.TestResponse))]
    internal partial class GoogleApisTestsJsonContext : JsonSerializerContext
    {
    }

#pragma warning disable CA2255 // Module initializer: the test assembly self-registers its context.
    internal static class GoogleApisTestsJsonModule
    {
        [ModuleInitializer]
        internal static void Initialize() =>
            SystemTextJsonSerializer.RegisterTypeInfoResolver(GoogleApisTestsJsonContext.Default);
    }
#pragma warning restore CA2255
}
