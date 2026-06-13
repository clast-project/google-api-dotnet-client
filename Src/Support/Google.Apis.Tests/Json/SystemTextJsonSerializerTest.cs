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
using Google.Apis.Requests;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xunit;

namespace Google.Apis.Tests.Json
{
    public class SystemTextJsonSerializerTest
    {
        static SystemTextJsonSerializerTest() =>
            SystemTextJsonSerializer.RegisterTypeInfoResolver(TestJsonContext.Default);

        private static readonly SystemTextJsonSerializer Serializer = SystemTextJsonSerializer.Instance;

        public class ListHolder
        {
            [JsonPropertyName("List")]
            public IList<string> List { get; set; }
        }

        public class DataWithTwoEtags
        {
            [JsonPropertyName("a_field")] public string AField { get; set; }
            [JsonPropertyName("etag")] public string ETag { get; set; }
            [JsonPropertyName("ETag")] public string ETag__ { get; set; }
        }

        public class WithDate
        {
            [JsonPropertyName("when")] public DateTime When { get; set; }
        }

        [Fact]
        public void ImplicitNullElided() =>
            Assert.Equal("{}", Serializer.Serialize(new ListHolder { List = null }));

        [Fact]
        public void ExplicitNullPresent() =>
            Assert.Equal("{\"List\":null}", Serializer.Serialize(new ListHolder { List = JsonExplicitNull.ForIList<string>() }));

        [Fact]
        public void RealListRoundTrips()
        {
            var json = Serializer.Serialize(new ListHolder { List = new List<string> { "x", "y" } });
            Assert.Equal("{\"List\":[\"x\",\"y\"]}", json);
            var back = Serializer.Deserialize<ListHolder>(json);
            Assert.Equal(new[] { "x", "y" }, back.List);
        }

        [Fact]
        public void SerializesTwoETags() =>
            Assert.Equal(
                "{\"a_field\":\"a value\",\"etag\":\"lowercase\",\"ETag\":\"no-lowercase\"}",
                Serializer.Serialize(new DataWithTwoEtags { AField = "a value", ETag = "lowercase", ETag__ = "no-lowercase" }));

        [Fact]
        public void DeserializesTwoETags()
        {
            var data = Serializer.Deserialize<DataWithTwoEtags>(
                "{\"a_field\":\"a value\",\"etag\":\"lowercase\",\"ETag\":\"no-lowercase\"}");
            Assert.Equal("a value", data.AField);
            Assert.Equal("lowercase", data.ETag);
            Assert.Equal("no-lowercase", data.ETag__);
        }

        [Fact]
        public void Rfc3339DateTimeSerialized() =>
            Assert.Equal(
                "{\"when\":\"2017-05-03T16:38:00.000Z\"}",
                Serializer.Serialize(new WithDate { When = new DateTime(2017, 5, 3, 16, 38, 0, DateTimeKind.Utc) }));

        [Fact]
        public void Rfc3339DateTimeDeserialized()
        {
            var value = Serializer.Deserialize<WithDate>("{\"when\":\"2017-05-03T16:38:00Z\"}");
            Assert.Equal(new DateTime(2017, 5, 3, 16, 38, 0, DateTimeKind.Utc), value.When.ToUniversalTime());
        }

        /// <summary>
        /// Newtonsoft matched property names case-insensitively; System.Text.Json is case-sensitive, so the
        /// attribute-less error types now carry explicit names. This verifies Google's camelCase error JSON still binds.
        /// </summary>
        [Fact]
        public void RequestErrorBindsCamelCase()
        {
            var error = Serializer.Deserialize<RequestError>(
                "{\"code\":403,\"message\":\"denied\",\"errors\":[{\"domain\":\"global\",\"reason\":\"forbidden\",\"message\":\"no\"}]}");
            Assert.Equal(403, error.Code);
            Assert.Equal("denied", error.Message);
            Assert.Single(error.Errors);
            Assert.Equal("global", error.Errors[0].Domain);
            Assert.Equal("forbidden", error.Errors[0].Reason);
        }
    }

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(SystemTextJsonSerializerTest.ListHolder))]
    [JsonSerializable(typeof(SystemTextJsonSerializerTest.DataWithTwoEtags))]
    [JsonSerializable(typeof(SystemTextJsonSerializerTest.WithDate))]
    [JsonSerializable(typeof(IList<string>))]
    internal partial class TestJsonContext : JsonSerializerContext
    {
    }
}
