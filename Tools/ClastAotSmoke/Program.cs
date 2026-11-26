// Clast Native-AOT smoke test: exercises each republished client's serialization and request building in a
// natively compiled binary, where a type missing from the source-generated metadata throws instead of silently
// falling back to reflection. Exit code 0 = all checks passed. Add cases here when porting a new client.

using Google.Apis.Auth.OAuth2;
using Google.Apis.Json;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using BigqueryData = Google.Apis.Bigquery.v2.Data;
using GmailData = Google.Apis.Gmail.v1.Data;
using StorageData = Google.Apis.Storage.v1.Data;

internal static class Program
{
    private static readonly SystemTextJsonSerializer Serializer = SystemTextJsonSerializer.Instance;
    private static int s_failures;
    private static int s_passed;

    private static int Main()
    {
        if (JsonSerializer.IsReflectionEnabledByDefault)
        {
            Console.WriteLine("WARNING: reflection-based serialization is enabled; this run does not prove AOT safety.");
        }

        Storage();
        Bigquery();
        Gmail();
        Auth();

        Console.WriteLine($"{s_passed} passed, {s_failures} failed");
        return s_failures == 0 ? 0 : 1;
    }

    private static void Check(string name, Action check)
    {
        try
        {
            check();
            s_passed++;
            Console.WriteLine($"PASS  {name}");
        }
        catch (Exception e)
        {
            s_failures++;
            Console.WriteLine($"FAIL  {name}: {e.GetType().Name}: {e.Message}");
        }
    }

    private static void Expect<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{what}: expected '{expected}', got '{actual}'");
        }
    }

    private static void ExpectContains(string haystack, string needle)
    {
        if (!haystack.Contains(needle, StringComparison.Ordinal))
        {
            throw new Exception($"expected '{needle}' in '{haystack}'");
        }
    }

    private static string RequestBody(System.Net.Http.HttpRequestMessage request) =>
        request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

    private static BaseClientService.Initializer NoGzip => new BaseClientService.Initializer { GZipEnabled = false };

    private static void Storage()
    {
        Check("storage: Objects list response (quoted ulong size, long generation, metadata map)", () =>
        {
            var objects = Serializer.Deserialize<StorageData.Objects>(@"{
                ""kind"": ""storage#objects"",
                ""items"": [{ ""bucket"": ""b"", ""name"": ""a/b.txt"", ""size"": ""18446744073709551615"",
                              ""generation"": ""1732600000000001"", ""contentType"": ""text/plain"",
                              ""updated"": ""2026-11-26T12:00:00.000Z"", ""metadata"": { ""k"": ""v"" } }] }");
            var item = objects.Items[0];
            Expect(ulong.MaxValue, item.Size, "size");
            Expect(1732600000000001L, item.Generation, "generation");
            Expect("v", item.Metadata["k"], "metadata");
            Expect("2026-11-26T12:00:00.000Z", item.UpdatedRaw, "updated");
        });

        Check("storage: objects.list URI", () =>
        {
            var request = new Google.Apis.Storage.v1.StorageService().Objects.List("my-bucket");
            request.Delimiter = "/";
            request.MaxResults = 5;
            var uri = request.CreateRequest().RequestUri.ToString();
            ExpectContains(uri, "https://storage.googleapis.com/storage/v1/b/my-bucket/o?");
            ExpectContains(uri, "delimiter=%2F");
            ExpectContains(uri, "maxResults=5");
        });

        Check("storage: objects.patch body", () =>
        {
            var service = new Google.Apis.Storage.v1.StorageService(NoGzip);
            var body = new StorageData.Object { ContentType = "text/plain", Metadata = new Dictionary<string, string> { ["k"] = "v" } };
            var json = RequestBody(service.Objects.Patch(body, "b", "o").CreateRequest());
            Expect("{\"contentType\":\"text/plain\",\"metadata\":{\"k\":\"v\"}}", json, "body");
        });
    }

    private static void Bigquery()
    {
        Check("bigquery: QueryResponse with object-typed cell values", () =>
        {
            var response = Serializer.Deserialize<BigqueryData.QueryResponse>(@"{
                ""kind"": ""bigquery#queryResponse"", ""jobComplete"": true, ""totalRows"": ""2"",
                ""schema"": { ""fields"": [{ ""name"": ""x"", ""type"": ""INTEGER"" }] },
                ""rows"": [{ ""f"": [{ ""v"": ""1"" }] }, { ""f"": [{ ""v"": null }] }] }");
            Expect(true, response.JobComplete, "jobComplete");
            Expect("x", response.Schema.Fields[0].Name, "field name");
            Expect("1", response.Rows[0].F[0].V?.ToString(), "cell value");
        });

        Check("bigquery: tabledata.insertAll body with object-typed row values", () =>
        {
            var service = new Google.Apis.Bigquery.v2.BigqueryService(NoGzip);
            var body = new BigqueryData.TableDataInsertAllRequest
            {
                Rows = new List<BigqueryData.TableDataInsertAllRequest.RowsData>
                {
                    new BigqueryData.TableDataInsertAllRequest.RowsData
                    {
                        InsertId = "1",
                        Json = new Dictionary<string, object> { ["s"] = "text", ["n"] = 42L, ["b"] = true },
                    },
                },
            };
            var request = service.Tabledata.InsertAll(body, "p", "d", "t").CreateRequest();
            ExpectContains(request.RequestUri.ToString(), "/projects/p/datasets/d/tables/t/insertAll");
            Expect("{\"rows\":[{\"insertId\":\"1\",\"json\":{\"s\":\"text\",\"n\":42,\"b\":true}}]}", RequestBody(request), "body");
        });

        Check("bigquery: jobs.getQueryResults URI", () =>
        {
            var request = new Google.Apis.Bigquery.v2.BigqueryService().Jobs.GetQueryResults("p", "j");
            request.MaxResults = 10;
            request.Location = "EU";
            var uri = request.CreateRequest().RequestUri.ToString();
            ExpectContains(uri, "/projects/p/queries/j?");
            ExpectContains(uri, "maxResults=10");
            ExpectContains(uri, "location=EU");
        });
    }

    private static void Gmail()
    {
        Check("gmail: Message (quoted ulong historyId, nested parts) round-trip", () =>
        {
            var message = Serializer.Deserialize<GmailData.Message>(@"{
                ""id"": ""m"", ""historyId"": ""18446744073709551615"", ""internalDate"": ""1732600000000"",
                ""labelIds"": [""INBOX""],
                ""payload"": { ""mimeType"": ""multipart/alternative"", ""headers"": [{ ""name"": ""Subject"", ""value"": ""Hi"" }],
                               ""parts"": [{ ""mimeType"": ""text/plain"", ""body"": { ""size"": 5, ""data"": ""SGVsbG8"" } }] } }");
            Expect(ulong.MaxValue, message.HistoryId, "historyId");
            Expect(1732600000000L, message.InternalDate, "internalDate");
            var copy = Serializer.Deserialize<GmailData.Message>(Serializer.Serialize(message));
            Expect("SGVsbG8", copy.Payload.Parts[0].Body.Data, "part body");
            Expect("Hi", copy.Payload.Headers[0].Value, "header");
        });

        Check("gmail: history.list URI (ulong + repeated enum)", () =>
        {
            var request = new Google.Apis.Gmail.v1.GmailService().Users.History.List("me");
            request.StartHistoryId = ulong.MaxValue;
            request.HistoryTypesList = new[]
            {
                Google.Apis.Gmail.v1.UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded,
                Google.Apis.Gmail.v1.UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.LabelRemoved,
            };
            var uri = request.CreateRequest().RequestUri.ToString();
            ExpectContains(uri, "startHistoryId=18446744073709551615");
            ExpectContains(uri, "historyTypes=messageAdded&historyTypes=labelRemoved");
        });

        Check("gmail: messages.get URI (enum wire value) and messages.send body", () =>
        {
            var service = new Google.Apis.Gmail.v1.GmailService(NoGzip);
            var get = service.Users.Messages.Get("me", "abc");
            get.Format = Google.Apis.Gmail.v1.UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
            Expect("https://gmail.googleapis.com/gmail/v1/users/me/messages/abc?format=metadata",
                get.CreateRequest().RequestUri.ToString(), "get URI");
            var send = service.Users.Messages.Send(new GmailData.Message { Raw = "cmF3" }, "me").CreateRequest();
            Expect("{\"raw\":\"cmF3\"}", RequestBody(send), "send body");
        });
    }

    private static void Auth()
    {
        Check("auth: service-account credential from JSON", () =>
        {
            using var rsa = RSA.Create(2048);
            var key = JsonSerializer.Serialize(rsa.ExportPkcs8PrivateKeyPem(), SmokeJsonContext.Default.String);
            var json = "{\"type\":\"service_account\",\"project_id\":\"p\",\"private_key_id\":\"k\"," +
                       $"\"private_key\":{key},\"client_email\":\"sa@p.iam.gserviceaccount.com\",\"client_id\":\"1\"," +
                       "\"token_uri\":\"https://oauth2.googleapis.com/token\"}";
            var credential = CredentialFactory.FromJson<ServiceAccountCredential>(json);
            Expect("sa@p.iam.gserviceaccount.com", credential.Id, "client_email");
            Expect("p", credential.ProjectId, "project_id");
        });
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
internal partial class SmokeJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
