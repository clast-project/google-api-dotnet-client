/*
Copyright 2026 Google LLC

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

using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Json;
using Google.Apis.Services;
using System.Collections.Generic;
using Xunit;
using static Google.Apis.Gmail.v1.UsersResource.HistoryResource.ListRequest;
using static Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest;

namespace Google.Apis.Tests.Apis.Services
{
    /// <summary>
    /// Clast: runtime coverage for the ported Gmail.v1 client. Gmail is the first republished client with
    /// <c>ulong</c> fields (<c>historyId</c>) and has no downstream wrapper whose tests would exercise it.
    /// </summary>
    public class GmailClientTest
    {
        private static readonly SystemTextJsonSerializer Serializer = SystemTextJsonSerializer.Instance;

        private const string MessageJson = @"{
            ""id"": ""18c0ffee"",
            ""threadId"": ""18c0ffee"",
            ""historyId"": ""18446744073709551615"",
            ""internalDate"": ""1732600000000"",
            ""labelIds"": [""INBOX"", ""UNREAD""],
            ""sizeEstimate"": 1234,
            ""snippet"": ""Hello"",
            ""payload"": {
                ""partId"": """",
                ""mimeType"": ""multipart/alternative"",
                ""headers"": [{ ""name"": ""Subject"", ""value"": ""Hi"" }],
                ""body"": { ""size"": 0 },
                ""parts"": [
                    { ""partId"": ""0"", ""mimeType"": ""text/plain"", ""body"": { ""size"": 5, ""data"": ""SGVsbG8"" } }
                ]
            }
        }";

        [Fact]
        public void Message_QuotedInt64FieldsDeserialize()
        {
            var message = Serializer.Deserialize<Message>(MessageJson);
            // Above long.MaxValue: must not be routed through a signed 64-bit type anywhere.
            Assert.Equal(ulong.MaxValue, message.HistoryId);
            Assert.Equal(1732600000000L, message.InternalDate);
            Assert.Equal(1234, message.SizeEstimate);
            Assert.Equal(new[] { "INBOX", "UNREAD" }, message.LabelIds);
        }

        [Fact]
        public void Message_NestedPartsDeserialize()
        {
            var message = Serializer.Deserialize<Message>(MessageJson);
            Assert.Equal("multipart/alternative", message.Payload.MimeType);
            var header = Assert.Single(message.Payload.Headers);
            Assert.Equal("Subject", header.Name);
            Assert.Equal("Hi", header.Value);
            var part = Assert.Single(message.Payload.Parts);
            Assert.Equal("text/plain", part.MimeType);
            Assert.Equal("SGVsbG8", part.Body.Data);
            Assert.Equal(5, part.Body.Size);
        }

        [Fact]
        public void Message_RoundTrips()
        {
            var original = Serializer.Deserialize<Message>(MessageJson);
            var json = Serializer.Serialize(original);
            var copy = Serializer.Deserialize<Message>(json);

            Assert.Equal(original.HistoryId, copy.HistoryId);
            Assert.Equal(original.InternalDate, copy.InternalDate);
            Assert.Equal(original.LabelIds, copy.LabelIds);
            Assert.Equal(original.Payload.Headers[0].Value, copy.Payload.Headers[0].Value);
            Assert.Equal(original.Payload.Parts[0].Body.Data, copy.Payload.Parts[0].Body.Data);
            // Null properties are omitted, not written as null.
            Assert.DoesNotContain("\"raw\"", json);
            Assert.DoesNotContain("null", json);
        }

        [Fact]
        public void ListMessagesResponse_Deserializes()
        {
            var response = Serializer.Deserialize<ListMessagesResponse>(
                @"{ ""messages"": [{ ""id"": ""a"", ""threadId"": ""t"" }, { ""id"": ""b"", ""threadId"": ""t"" }],
                    ""nextPageToken"": ""next"", ""resultSizeEstimate"": 2 }");
            Assert.Equal(new[] { "a", "b" }, new[] { response.Messages[0].Id, response.Messages[1].Id });
            Assert.Equal("next", response.NextPageToken);
        }

        [Fact]
        public void MessagesList_RepeatableStringAndQueryParameters()
        {
            var request = new GmailService().Users.Messages.List("me");
            request.LabelIds = new[] { "INBOX", "UNREAD" };
            request.Q = "from:someone";
            request.MaxResults = 10;
            var uri = request.CreateRequest().RequestUri.ToString();
            Assert.StartsWith("https://gmail.googleapis.com/gmail/v1/users/me/messages?", uri);
            Assert.Contains("labelIds=INBOX&labelIds=UNREAD", uri);
            Assert.Contains("maxResults=10", uri);
            Assert.Contains("q=from%3Asomeone", uri);
        }

        [Fact]
        public void MessagesGet_EnumQueryParameterUsesWireValue()
        {
            var request = new GmailService().Users.Messages.Get("me", "abc");
            request.Format = FormatEnum.Metadata;
            var uri = request.CreateRequest().RequestUri;
            Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/abc?format=metadata", uri.ToString());
        }

        [Fact]
        public void HistoryList_UlongAndRepeatedEnumQueryParameters()
        {
            var request = new GmailService().Users.History.List("me");
            request.StartHistoryId = ulong.MaxValue;
            request.HistoryTypesList = new[] { HistoryTypesEnum.MessageAdded, HistoryTypesEnum.LabelRemoved };
            var uri = request.CreateRequest().RequestUri.ToString();
            Assert.Contains("startHistoryId=18446744073709551615", uri);
            Assert.Contains("historyTypes=messageAdded&historyTypes=labelRemoved", uri);
        }

        [Fact]
        public void MessagesSend_BodySerializes()
        {
            var service = new GmailService(new BaseClientService.Initializer { GZipEnabled = false });
            var body = new Message { Raw = "cmF3", LabelIds = new List<string> { "SENT" } };
            var httpRequest = service.Users.Messages.Send(body, "me").CreateRequest();
            Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/send", httpRequest.RequestUri.ToString());
            var json = httpRequest.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Equal("{\"labelIds\":[\"SENT\"],\"raw\":\"cmF3\"}", json);
        }
    }
}
