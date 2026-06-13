# Clast: Newtonsoft-free, AOT-ready Google .NET packages

## Goal

Republish a subset of Google's published .NET packages so that they:

1. Drop the `Newtonsoft.Json` dependency in favour of **source-generated** `System.Text.Json`
   (reflection-based serialization is disallowed — compile-time codegen on **all** build targets).
2. Are **AOT-compatible**.
3. Add a **`net10.0`** target that is specifically validated for AOT.
4. Ship under a **`Clast.` package-id prefix**, keeping the original **namespaces and type names**.

Keeping `System.Text.Json` out of the public API is a soft ideal, not a hard requirement.

**Initial target:** `Google.Cloud.Storage.V1` and its full transitive closure.

## Closure — three repos (all checked out locally)

```
Clast.Google.Cloud.Storage.V1     (google-cloud-dotnet)  — Newtonsoft only internal (UrlSigner V4 signing)
 ├─ Clast.Google.Api.Gax.Rest      (gax-dotnet)           — no direct Newtonsoft; delegates JSON to Apis.Core
 │   └─ Clast.Google.Api.Gax       (gax-dotnet)           — Newtonsoft in 4 platform-detail files (JObject.Parse)
 └─ Clast.Google.Apis.Storage.v1   (google-api-dotnet-client, generated) — 336 [Newtonsoft.Json.JsonProperty]
     └─ Clast.Google.Apis          (google-api-dotnet-client)
         └─ Clast.Google.Apis.Auth (google-api-dotnet-client) — ~12 files use Newtonsoft directly
             └─ Clast.Google.Apis.Core (google-api-dotnet-client)   — Newtonsoft ROOTED HERE
```

Repo locations:
- `C:\src\GitHub\google-api-dotnet-client` — Apis.Core / Apis / Apis.Auth / generated Storage.v1
- `C:\src\GitHub\gax-dotnet` — Google.Api.Gax / Google.Api.Gax.Rest
- `C:\src\GitHub\google-cloud-dotnet` — Google.Cloud.Storage.V1

**Seven Clast packages** result:
`Clast.Google.Apis.Core`, `Clast.Google.Apis`, `Clast.Google.Apis.Auth`,
`Clast.Google.Apis.Storage.v1`, `Clast.Google.Api.Gax`, `Clast.Google.Api.Gax.Rest`,
`Clast.Google.Cloud.Storage.V1`.

## Conventions / decisions (2026-06-12)

- **Namespaces & type names:** unchanged (`Google.Apis.*`, `Google.Api.Gax.*`, `Google.Cloud.Storage.V1`).
- **Assembly file names:** renamed to `Clast.*` (set `<AssemblyName>` explicitly). Rationale: avoid
  shipping assemblies that look like they came from Google. Consumers must recompile — not a binary drop-in,
  by choice.
- **PackageId:** explicit `<PackageId>Clast.*</PackageId>` on every shipped project (today PackageId
  defaults to the project name, so this must be added).
- **Strong naming:** new `Clast.snk` (one key for all three repos), **not** the original Google keys.
  Update every `[InternalsVisibleTo(...PublicKey=...)]` to the new key.
- **Generated code:** transform the checked-in `Google.Apis.Storage.v1.cs` (one-shot script) rather than
  forking the external `gapic-generator-csharp`. Revisit the generator only if scaling to many clients.
- **System.Text.Json:** version 9.x on all TFMs (netstandard2.0, net462, net6.0/net10.0).
- **AOT enforcement:** on the `net10.0` target set `<IsAotCompatible>true</IsAotCompatible>`.
  `TreatWarningsAsErrors` is already on, so IL2xxx/IL3xxx analyzer findings will fail the build.

## Keystone design — STJ source-gen serializer in `Google.Apis.Core`

The current `IJsonSerializer` is reflection-shaped (`Deserialize(string, Type)`, `Serialize(object)`).
The public `ISerializer`/`IJsonSerializer` interfaces do **not** expose Newtonsoft types, so they survive
unchanged. The implementation is replaced:

- New `SystemTextJsonSerializer : IJsonSerializer`. Holds `JsonSerializerOptions` whose
  `TypeInfoResolver` is a **chain** built with `JsonTypeInfoResolver.Combine(...)` over the registered
  source-generated `JsonSerializerContext`s.
- **Per-assembly contexts:** Core, Auth, and every generated client declare their own `partial`
  `JsonSerializerContext` with `[JsonSerializable(typeof(Model))]` for their model types, and **self-register**
  into the chain via a `[ModuleInitializer]`. Keeps metadata local to each assembly; AOT-safe.
- Non-generic `Deserialize(input, Type)` resolves via `options.GetTypeInfo(type)` — metadata lookup,
  not reflection serialization.

### Custom behaviors to reproduce

| Newtonsoft behavior | Where it lives today | STJ replacement |
|---|---|---|
| RFC3339 `DateTime` | `RFC3339DateTimeConverter` (`Json/NewtonsoftJsonSerializer.cs`) + `Utilities.ConvertToRFC3339` | `JsonConverter<DateTime>` + `JsonConverter<DateTime?>` registered in options |
| `NullValueHandling.Ignore` | `CreateDefaultSettings()` | `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` |
| `MetadataPropertyHandling.Ignore` | `CreateDefaultSettings()` | STJ default ignores `$id`/`$ref` unless `ReferenceHandler` set — no-op |
| `JsonExplicitNullAttribute` / `JsonExplicitNull` sentinel | `ExplicitNullConverter`, `Json/JsonExplicitNull*.cs` | **Trickiest.** Contract-customization modifier (STJ 8+ `IJsonTypeInfoResolver` modifier) to force null emission on flagged members; available on netstandard2.0/net462 via the STJ package |
| `IDirectResponseSchema.ETag` | runtime `is`-check in `BaseClientService.DeserializeResponse<T>` | Unchanged — not a serialization concern |
| `StandardResponse<T>` legacy error parsing | `Util/StandardResponse.cs`, `Responses/HttpResponseMessageExtensions.cs` | Same shape, `[JsonPropertyName]`, in a context |

### Key files (this repo)

- `Src/Support/Google.Apis.Core/ISerializer.cs` — interfaces (keep)
- `Src/Support/Google.Apis.Core/Json/NewtonsoftJsonSerializer.cs` — replace
- `Src/Support/Google.Apis.Core/Json/JsonExplicitNull*.cs` — port behavior
- `Src/Support/Google.Apis.Core/Util/StandardResponse.cs`, `Util/Utilities.cs` — port `[JsonProperty]` + `JsonConvert` calls
- `Src/Support/Google.Apis/Services/BaseClientService.cs` (`DeserializeResponse<T>`, ~380-440)
- `Src/Support/Google.Apis/Responses/HttpResponseMessageExtensions.cs` — error parsing
- `Src/Support/Google.Apis.Auth/**` — ~12 files using `[JsonProperty]`, `JObject`, `NewtonsoftJsonSerializer.Instance`

## Per-repo work

### google-api-dotnet-client
- Replace Core serializer (above). Drop `Newtonsoft.Json` PackageReference from `Google.Apis.Core.csproj`;
  add `System.Text.Json` 9.x.
- Port Auth's direct Newtonsoft usage (`JsonCredentialParameters`, `JsonWebSignature`, `GoogleJsonWebSignature`,
  `SignedTokenVerification` JObject, AWS external-account credential JSON, `TokenResponse`, `ClientSecrets`, …).
- Add `net10.0` to `Google.Apis.Core`, `Google.Apis`, `Google.Apis.Auth` (TFMs are per-project).
- Transform generated `Src/Generated/Google.Apis.Storage.v1/Google.Apis.Storage.v1.cs`:
  `[Newtonsoft.Json.JsonProperty("x")]` → `[System.Text.Json.Serialization.JsonPropertyName("x")]`,
  add a generated `JsonSerializerContext` + module-initializer registration.
- Packaging: per shipped project add `<PackageId>Clast.*</PackageId>`, `<AssemblyName>Clast.*</AssemblyName>`,
  point inter-package dependencies at the `Clast.*` ids, switch `.snk` to `Clast.snk`, fix `[InternalsVisibleTo]`.

### gax-dotnet
- Replace `JObject.Parse` in `GcePlatformDetails.cs`, `GkePlatformDetails.cs`, `CloudRunPlatformDetails.cs`,
  `CloudRunJobPlatformDetails.cs` with `JsonDocument`/`JsonNode` (these are public `TryLoad` factories; signatures
  take `string`, so the change is internal).
- Repoint `Google.Api.Gax.Rest` to `Clast.Google.Apis.Auth`; add `net10.0`; Clast prefix + rename + `Clast.snk`.
- Produces `Clast.Google.Api.Gax`, `Clast.Google.Api.Gax.Rest` (other Gax packages out of scope).

### google-cloud-dotnet
- Replace internal `JsonTextWriter`/`JsonWriter` usage in `UrlSigner.V4Signer.cs`,
  `UrlSigner.PostPolicy*.cs` with `Utf8JsonWriter` (all internal — `IPostPolicyCondition` is internal).
- Repoint to `Clast.Google.Api.Gax.Rest` + `Clast.Google.Apis.Storage.v1`; add `net10.0`;
  Clast prefix + rename + `Clast.snk`.
- Produces `Clast.Google.Cloud.Storage.V1`.

## Phases

1. **Core serializer (riskiest first).** STJ source-gen serializer + converters + registration in
   `Google.Apis.Core`; port Auth; add `net10.0`. **Acceptance:** existing Newtonsoft serializer/Auth tests
   ported and green; no Newtonsoft reference remains in Core/Auth.
2. **Storage generated client.** Transform `Storage.v1.cs` + emit its context. **Acceptance:** Storage models
   round-trip through the new serializer; builds on all TFMs.
3. **Clast packaging (this repo).** PackageId/AssemblyName/`Clast.snk`/IVT/dependency rewiring for
   Core/Apis/Auth/Storage.v1. **Acceptance:** 4 `Clast.*` packages pack; a scratch consumer restores & builds.
4. **gax-dotnet.** Port 4 platform files; repoint to Clast.Apis.Auth; net10; Clast packaging.
   **Acceptance:** 2 packages pack; Gax tests green.
5. **google-cloud-dotnet.** Port UrlSigner; repoint to Clast deps; net10; Clast packaging.
   **Acceptance:** `Clast.Google.Cloud.Storage.V1` packs; Storage.V1 unit tests green.
6. **AOT acceptance gate.** Console app referencing `Clast.Google.Cloud.Storage.V1`, published with
   `PublishAot=true`. **Acceptance:** zero IL2xxx/IL3xxx warnings; runs against the Storage emulator/real API
   (basic upload/download/list round-trip).

## Behavioral changes

Every observable Newtonsoft→STJ behavior difference is logged in **`BEHAVIORAL-CHANGES.md`**, each with a
detection signature and a compatibility path, so a later regression can be attributed to a specific change and
(if needed) drive a preprocessing/shim. Add an entry there whenever a new divergence is found.

## Risks / watch-list

- **`JsonExplicitNull` sentinel** is the single trickiest behavior — STJ has no direct equivalent; needs a
  contract modifier. Validate early in Phase 1.
- **STJ contract customization on net462/netstandard2.0** depends on the STJ 9.x package surface (works, but
  pulls a few transitive runtime packages on old TFMs).
- **Source-gen coverage gaps** → runtime `NotSupportedException`. Every serializable type must be in a
  context; the resolver chain must include all relevant assemblies' contexts before first use.
- **`[InternalsVisibleTo]` with public keys** must be updated to `Clast.snk`'s key, or internals-access breaks.
- **Generated-code transform** is a one-shot for Storage; re-running discovery generation would reintroduce
  Newtonsoft attributes until/unless the generator is changed.

## Out of scope (for now)

- Forking `gapic-generator-csharp` (the generator).
- Other Gax packages (`.Grpc`, `.Testing`, `CommonProtos`) and other `Google.Cloud.*` libraries.
- gRPC/protobuf path — Storage.V1 is pure REST.
